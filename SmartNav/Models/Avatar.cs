using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("LK_Avatars")]
    public class Avatar
    {
        [Key]
        public int Id { get; set; }

        public string? AvatarName { get; set; }

        public string? AvatarURL { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<User>? Users { get; set; }
    }
}
