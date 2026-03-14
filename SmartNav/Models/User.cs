using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("User")]
    public class User
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool IsVerified { get; set; }
        public string? VerificationToken { get; set; }

        public int RoleId { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role? Role { get; set; }

        public int AvatarId { get; set; }

        [ForeignKey("AvatarId")]
        public virtual Avatar? Avatar { get; set; }

    }
}
