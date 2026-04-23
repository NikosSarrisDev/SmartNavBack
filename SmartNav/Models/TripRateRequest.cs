namespace SmartNav.Models
{
    public class TripRateRequest
    {
        public int UserId { get; set; }

        public int? TripId { get; set; }

        public int Score { get; set; }
    }
}
