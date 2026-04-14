namespace SmartNav.Models
{
    public class FilteredPreferenceUpsertRequest
    {
        public int? Id { get; set; }

        public int? UserID { get; set; }

        public string? SelectedPreferenceCode { get; set; }

        public string? SelectedPreferencePrompt { get; set; }

        public string? VehicleSize { get; set; }

        public bool AvoidTolls { get; set; }

        public bool AvoidHighways { get; set; }

        public bool AvoidFerries { get; set; }

        public string? TrafficTimeMode { get; set; }

        public DateTime? TrafficStartDateTime { get; set; }

        public DateTime? TrafficEndDateTime { get; set; }

        public bool IncludeEvChargingStations { get; set; }

        public List<FilteredPreferenceStationRequest>? Stations { get; set; }
    }

    public class FilteredPreferenceStationRequest
    {
        public string? Street { get; set; }

        public string? Number { get; set; }

        public string? CityArea { get; set; }

        public string? PostalCode { get; set; }
    }

    public class FilteredPreferenceDeleteRequest
    {
        public int? Id { get; set; }

        public int? UserID { get; set; }
    }
}
