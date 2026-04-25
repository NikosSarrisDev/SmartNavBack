using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("AdminActionLog")]
    public class AdminActionLog
    {
        [Key]
        public int Id { get; set; }

        public int AdminUserId { get; set; }

        public int? TargetUserId { get; set; }

        public string ActionType { get; set; } = string.Empty;

        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
