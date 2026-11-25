using System;
using System.ComponentModel.DataAnnotations;

namespace FailSafeWeatherMonitor.Models
{
    public class WeatherDataPoint
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string StationName { get; set; } = string.Empty;

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        public double Temperature { get; set; }

        [Required]
        public double Humidity { get; set; }

        [Required]
        public string DataSource { get; set; } = string.Empty;
    }
}