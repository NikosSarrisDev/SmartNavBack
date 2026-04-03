using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("LK_Vehicle")]
    public class Vehicle
    {
        [Key]
        public int Id { get; set; }

        public string? Code { get; set; }

        public string? Label { get; set; }

        public string? TranslationField { get; set; }

        public virtual ICollection<Trip>? Trips { get; set; }
    }
}
