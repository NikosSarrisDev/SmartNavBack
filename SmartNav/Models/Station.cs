using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNav.Models
{
    [Table("Station")]
    public class Station
    {
        [Key]
        public int Id { get; set; }

        public int TripID { get; set; }

        public string? Street { get; set; }

        public string? Number { get; set; }

        public string? CityArea { get; set; }

        public string? PostalCode { get; set; }

        public int? Position { get; set; }

        [ForeignKey("TripID")]
        public virtual Trip? Trip { get; set; }
    }
}
