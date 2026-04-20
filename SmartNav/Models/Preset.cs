using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("Preset")]
    public class Preset
    {
        [Key]
        public int Id { get; set; }

        [Column("UserID")]
        public int? UserID { get; set; }

        public string? Street { get; set; }

        public string? Number { get; set; }

        public string? CityArea { get; set; }

        public string? PostalCode { get; set; }

        public int? Position { get; set; }

        [Column("Preset_Icon_Id")]
        public int? PresetIconId { get; set; }

        [ForeignKey("UserID")]
        public virtual User? User { get; set; }

        [ForeignKey("PresetIconId")]
        public virtual PresetIcon? PresetIcon { get; set; }
    }
}
