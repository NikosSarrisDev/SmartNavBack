using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("Trips")]
    public class Trip
    {
        [Key]
        public int? Id { get; set; }

        public int? UserID { get; set; }

        public string? Destination { get; set; }

        public string? Departure { get; set; }

        public decimal? DistanceKM { get; set; }

        public decimal? Score { get; set; }

        public DateTime TripDate { get; set; }

        public int? VehicleID { get; set; }

        [ForeignKey("UserID")]
        public virtual User? User { get; set; }

        [ForeignKey("VehicleID")]
        public virtual Vehicle? Vehicle { get; set; }

        public virtual ICollection<Station>? Stations { get; set; }
    }
}
