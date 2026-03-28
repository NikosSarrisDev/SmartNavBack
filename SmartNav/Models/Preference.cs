using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("LK_Preferences")]
    public class Preference
    {
        public int Id { get; set; }

        public string? Code { get; set; }

        public string? Label { get; set; }

        public string? Icon { get; set; }

        public string? TranslationField { get; set; }

        public string? Prompt { get; set; }
    }
}
