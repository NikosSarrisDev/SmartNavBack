using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("LK_Roles")]
    public class Role
    {
        [Key]
        public int RoleID { get; set; }

        public string? RoleName { get; set; }

        public string? Description { get; set; }

        public int? MaxDailyRequests { get; set; }

        public bool? CanVoteRoutes { get; set; }

        public virtual ICollection<User>? Users { get; set; }
    }
}
