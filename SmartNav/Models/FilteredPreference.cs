using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("FilteredPreference")]
    public class FilteredPreference
    {
        [Key]
        public int Id { get; set; }

        public int? UserID { get; set; }

        public string? SelectedPreferenceCode { get; set; }

        public string? SelectedPreferencePrompt { get; set; }

        public string? MoodCode { get; set; }

        public string? VehicleSize { get; set; }

        public bool AvoidTolls { get; set; }

        public bool AvoidHighways { get; set; }

        public bool AvoidFerries { get; set; }

        public string? TrafficTimeMode { get; set; }

        public DateTime? TrafficStartDateTime { get; set; }

        public DateTime? TrafficEndDateTime { get; set; }

        public bool IncludeEvChargingStations { get; set; }

        public string? StationsJson { get; set; }

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }
}
