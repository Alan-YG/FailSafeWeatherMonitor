using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace FailSafeWeatherMonitor.Models
{
    public class WeatherDbContext : DbContext
    {
        // Konstruktör utan parametrar (används av EF Core-verktyg vid migration)
        public WeatherDbContext() { }

        // Konstruktör med options (används av Program.cs vid körning)
        public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options) { }

        public DbSet<WeatherDataPoint> WeatherDataPoints { get; set; }

        // Konfigurerar anslutningssträngen om den inte anges utifrån
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
            }
        }
    }
}