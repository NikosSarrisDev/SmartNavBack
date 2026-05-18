using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("UserSettings")]
    public class UserSettings
    {
        [Key]
        public int Id { get; set; }

        public int UserID { get; set; }

        public int AiAggressiveness { get; set; } = 3;

        public bool AlwaysShowRouteExplanation { get; set; } = true;

        public int AlternativeRoutesCount { get; set; } = 2;

        public string Theme { get; set; } = "system";

        public string MapStyle { get; set; } = "standard";

        public string DistanceUnit { get; set; } = "km";

        public string TimeFormat { get; set; } = "24h";

        public string ChipDensity { get; set; } = "comfortable";

        public bool LargeText { get; set; } = false;

        public bool HighContrast { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }

    public class UserSettingsUpsertRequest
    {
        public int UserId { get; set; }
        public int AiAggressiveness { get; set; }
        public bool AlwaysShowRouteExplanation { get; set; }
        public int AlternativeRoutesCount { get; set; }
        public string? Theme { get; set; }
        public string? MapStyle { get; set; }
        public string? DistanceUnit { get; set; }
        public string? TimeFormat { get; set; }
        public string? ChipDensity { get; set; }
        public bool LargeText { get; set; }
        public bool HighContrast { get; set; }
    }
}
