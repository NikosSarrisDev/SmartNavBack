using SmartNav.Models;

namespace SmartNav.Interfaces
{
    public interface IAiSuggestionService
    {
        Task<AiPreferenceSuggestionResult> GetSuggestedPreferenceAsync(
            IReadOnlyList<Trip> recentTrips,
            IReadOnlyCollection<string> allowedPreferenceCodes,
            CancellationToken cancellationToken = default);
    }
}
