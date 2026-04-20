using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("LK_Preset_Icon")]
    public class PresetIcon
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("icon_data")]
        public string? IconData { get; set; }

        [Column("translationField")]
        public string? TranslationField { get; set; }

        public virtual ICollection<Preset>? Presets { get; set; }
    }
}
