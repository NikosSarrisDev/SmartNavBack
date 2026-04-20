namespace SmartNav.Models
{
    public class PresetCreateRequest
    {
        public int? UserID { get; set; }

        public string? Street { get; set; }

        public string? Number { get; set; }

        public string? CityArea { get; set; }

        public string? PostalCode { get; set; }

        public int? PresetIconId { get; set; }
    }
}
