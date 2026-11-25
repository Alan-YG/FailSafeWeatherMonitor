# FailSafeWeatherMonitor

## Om projektet
Det här är min inlämningsuppgift 2 i DevSecOps-kursen. Projektet är en konsolapplikation i C# som samlar in väderdata, validerar den och sparar den i en databas med hjälp av Entity Framework Core.

Jag vill vara ärlig och säga att det här projektet har varit en av de största utmaningarna jag har haft i SQL kursen hittills. Jag har fastnat många gånger – med NuGet-paket som inte ville installeras, med migrations som inte hittade min DbContext, och med filer som inte kopierades till rätt mapp. Men jag gav inte upp, och jag lärde mig otroligt mycket på vägen.

## Vad programmet kan göra
- **Importera historisk data från CSV**: Läser filen `weather_data.csv`, validerar varje rad med `TryParse` och `CultureInfo.InvariantCulture`. Korrupta rader (t.ex. "9,8" med kommatecken, "N/A_TEMP", eller temperaturer som 999.9) förkastas och sparas i `bad_data.log`.
- **Hämta live-data från API**: Ansluter till Open-Meteo API och sparar aktuell väderdata för Göteborg. Om internet inte fungerar kraschar inte programmet – det visar bara ett felmeddelande (try-catch).
- **Visa väderstatistik**: Användaren kan välja en mätstation och se alla mätningar i en snygg tabell, plus max, min och medeltemperatur.
- **Förhindra dubbletter**: Innan en rad sparas kontrolleras om samma station och tidstämpel redan finns i databasen.

## Teknisk information
- **Språk**: C# (.NET 11.0) Det är super viktigt när du öppnar projektet och kör det!!! 
- **Databas**: SQL Server LocalDB med Entity Framework Core (Code First)
- **Konfiguration**: Anslutningssträngen ligger i `appsettings.json` 
- **UI**: Spectre.Console för meny och tabeller
- **Felloggning**: `bad_data.log` skapas automatiskt vid import

## Hur man kör projektet
1. Se till att filen `weather_data.csv` ligger i projektets rotmapp.
2. Öppna **Package Manager Console** i Visual Studio och kör:
