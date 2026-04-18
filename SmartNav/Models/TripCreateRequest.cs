namespace SmartNav.Models
{
    public class TripCreateRequest
    {
        public int? UserID { get; set; }

        public string? Destination { get; set; }

        public string? Departure { get; set; }

        public decimal? DistanceKM { get; set; }

        public decimal? Score { get; set; }

        public DateTime TripDate { get; set; }

        public int? VehicleID { get; set; }

        public string? VehicleCode { get; set; }

        public List<StationCreateRequest>? Stations { get; set; }
    }

    public class StationCreateRequest
    {
        public string? Street { get; set; }

        public string? Number { get; set; }

        public string? CityArea { get; set; }

        public string? PostalCode { get; set; }

        public int? Position { get; set; }
    }
}
