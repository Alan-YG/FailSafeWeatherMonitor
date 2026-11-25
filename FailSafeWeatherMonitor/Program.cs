using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using FailSafeWeatherMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace FailSafeWeatherMonitor
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static void Main(string[] args)
        {
            // 1. Läs in konfigurationen från appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // 2. Konfigurera databaskontexten
            var options = new DbContextOptionsBuilder<WeatherDbContext>()
                .UseSqlServer(config.GetConnectionString("DefaultConnection"))
                .Options;

            using var context = new WeatherDbContext(options);
            context.Database.EnsureCreated();

            bool kor = true;
            while (kor)
            {
                // 3. Interaktiv meny med Spectre.Console
                var val = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[green]FailSafe WeatherMonitor[/]")
                        .AddChoices("1. Importera historisk data från fil (CSV)",
                                    "2. Hämta live-data från API",
                                    "3. Visa väderstatistik",
                                    "4. Avsluta"));

                switch (val)
                {
                    case "1. Importera historisk data från fil (CSV)":
                        ImporteraFranFil(context);
                        break;
                    case "2. Hämta live-data från API":
                        HamtaLiveData(context);
                        break;
                    case "3. Visa väderstatistik":
                        VisaStatistik(context);
                        break;
                    case "4. Avsluta":
                        kor = false;
                        AnsiConsole.MarkupLine("[red]Programmet avslutas. Hej då![/]");
                        break;
                }
            }
        }

        // --- METOD 1: Importera från CSV-fil ---
        static void ImporteraFranFil(WeatherDbContext context)
        {
            string filnamn = "weather_data.csv";

            if (!File.Exists(filnamn))
            {
                AnsiConsole.MarkupLine($"[red]Fel: Hittar inte filen '{filnamn}'.[/]");
                return;
            }

            // Enligt krav: Rensa loggfilen inför varje ny import
            File.WriteAllText("bad_data.log", string.Empty);

            var rader = File.ReadAllLines(filnamn);
            int importerade = 0;
            int forkastade = 0;

            // Hoppa över första raden (rubriker)
            for (int i = 1; i < rader.Length; i++)
            {
                string rad = rader[i];
                if (string.IsNullOrWhiteSpace(rad)) continue;

                var delar = rad.Split('\t'); // Filen är tabb-separerad

                // Validering 1: Rätt antal kolumner? (Måste vara exakt 4)
                if (delar.Length != 4)
                {
                    LoggaFel(rad);
                    forkastade++;
                    continue;
                }

                string station = delar[0].Trim();
                string tidsstämpelStr = delar[1].Trim();
                string tempStr = delar[2].Trim();
                string fuktStr = delar[3].Trim();

                // Validering 2: Är stationsnamnet tomt?
                if (string.IsNullOrWhiteSpace(station))
                {
                    LoggaFel(rad);
                    forkastade++;
                    continue;
                }

                // Validering 3: Defensiv parsing med InvariantCulture (tvingar punkt som decimaltecken)
                // Detta fångar rader med kommatecken (t.ex. "9,8") och text som "N/A_TEMP"
                if (!DateTime.TryParse(tidsstämpelStr, out DateTime tidsstämpel) ||
                    !double.TryParse(tempStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double temperatur) ||
                    !double.TryParse(fuktStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double fuktighet))
                {
                    LoggaFel(rad);
                    forkastade++;
                    continue;
                }

                // Validering 4: Rimlighetskontroll (extrema mätfel, t.ex. 999.9)
                if (temperatur < -60 || temperatur > 60 || fuktighet < 0 || fuktighet > 100)
                {
                    LoggaFel(rad);
                    forkastade++;
                    continue;
                }

                // Validering 5: Förhindra dubbletter (samma station + samma tid)
                bool finnsRedan = context.WeatherDataPoints.Any(w =>
                    w.StationName == station && w.Timestamp == tidsstämpel);

                if (finnsRedan) continue;

                // Allt är giltigt, lägg till i databasen
                context.WeatherDataPoints.Add(new WeatherDataPoint
                {
                    StationName = station,
                    Timestamp = tidsstämpel,
                    Temperature = temperatur,
                    Humidity = fuktighet,
                    DataSource = "CSV-File"
                });
                importerade++;
            }

            context.SaveChanges();
            AnsiConsole.MarkupLine($"[green]Klart! {importerade} rader importerades, {forkastade} rader förkastades.[/]");
        }

        // Hjälpmetod för att skriva till bad_data.log
        static void LoggaFel(string rad)
        {
            File.AppendAllText("bad_data.log", rad + Environment.NewLine);
        }

        // --- METOD 2: Hämta live-data från API ---
        static void HamtaLiveData(WeatherDbContext context)
        {
            string url = "https://api.open-meteo.com/v1/forecast?latitude=57.6832&longitude=11.9511&hourly=temperature_2m,relative_humidity_2m";

            try
            {
                AnsiConsole.MarkupLine("[yellow]Hämtar data från Open-Meteo...[/]");
                string jsonSvar = httpClient.GetStringAsync(url).GetAwaiter().GetResult();

                using var doc = JsonDocument.Parse(jsonSvar);
                var hourly = doc.RootElement.GetProperty("hourly");

                string tid = hourly.GetProperty("time")[0].GetString();
                double temp = hourly.GetProperty("temperature_2m")[0].GetDouble();
                double fukt = hourly.GetProperty("relative_humidity_2m")[0].GetDouble();

                DateTime tidsstämpel = DateTime.Parse(tid);

                // Dubblettkontroll även för API
                bool finnsRedan = context.WeatherDataPoints.Any(w =>
                    w.StationName == "Göteborg (API)" && w.Timestamp == tidsstämpel);

                if (!finnsRedan)
                {
                    context.WeatherDataPoints.Add(new WeatherDataPoint
                    {
                        StationName = "Göteborg (API)",
                        Timestamp = tidsstämpel,
                        Temperature = temp,
                        Humidity = fukt,
                        DataSource = "API"
                    });
                    context.SaveChanges();
                    AnsiConsole.MarkupLine($"[green]Sparade: {tidsstämpel}, Temp: {temp}°C, Fukt: {fukt}%[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Denna mätning finns redan i databasen.[/]");
                }
            }
            catch (Exception ex)
            {
                // Ops-krav: Programmet får inte krascha vid nätverksfel
                AnsiConsole.MarkupLine($"[red]Nätverksfel vid API-anrop: {ex.Message}[/]");
            }
        }

        // --- METOD 3: Visa statistik ---
        static void VisaStatistik(WeatherDbContext context)
        {
            var stationer = context.WeatherDataPoints.Select(w => w.StationName).Distinct().ToList();

            if (!stationer.Any())
            {
                AnsiConsole.MarkupLine("[yellow]Ingen data i databasen än. Importera eller hämta data först.[/]");
                return;
            }

            var valdStation = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Välj en mätstation:")
                    .AddChoices(stationer));

            var mätningar = context.WeatherDataPoints
                .Where(w => w.StationName == valdStation)
                .OrderBy(w => w.Timestamp)
                .ToList();

            double maxTemp = mätningar.Max(w => w.Temperature);
            double minTemp = mätningar.Min(w => w.Temperature);
            double medelTemp = mätningar.Average(w => w.Temperature);

            var tabell = new Table();
            tabell.Title($"[bold blue]Statistik för {valdStation}[/]");
            tabell.AddColumn("Tidstämpel");
            tabell.AddColumn("Temperatur (°C)");
            tabell.AddColumn("Luftfuktighet (%)");
            tabell.AddColumn("Källa");

            foreach (var m in mätningar)
            {
                tabell.AddRow(
                    m.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                    m.Temperature.ToString("0.0", CultureInfo.InvariantCulture),
                    m.Humidity.ToString("0.0", CultureInfo.InvariantCulture),
                    m.DataSource
                );
            }

            AnsiConsole.Write(tabell);

            AnsiConsole.MarkupLine($"\n[bold]Sammanfattning:[/]");
            AnsiConsole.MarkupLine($"Högsta temp: [red]{maxTemp:F1}°C[/]");
            AnsiConsole.MarkupLine($"Lägsta temp: [blue]{minTemp:F1}°C[/]");
            AnsiConsole.MarkupLine($"Medeltemp: [green]{medelTemp:F1}°C[/]");

            AnsiConsole.WriteLine("\nTryck på valfri tangent för att fortsätta...");
            Console.ReadKey();
        }
    }
}