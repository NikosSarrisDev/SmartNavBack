namespace SmartNav.Models
{
    public class AiPreferenceSuggestionResult
    {
        public string SuggestedPreference { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Source { get; set; } = "fallback";
    }
}
