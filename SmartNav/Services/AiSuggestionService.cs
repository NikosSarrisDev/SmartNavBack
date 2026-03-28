using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SmartNav.Interfaces;
using SmartNav.Models;

namespace SmartNav.Services
{
    public class AiSuggestionService : IAiSuggestionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AiSuggestionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<AiPreferenceSuggestionResult> GetSuggestedPreferenceAsync(
            IReadOnlyList<Trip> recentTrips,
            IReadOnlyCollection<string> allowedPreferenceCodes,
            CancellationToken cancellationToken = default)
        {
            var fallback = BuildFallback(recentTrips, allowedPreferenceCodes);
            var apiKey = _configuration["GeminiSettings:ApiKey"];
            var model = _configuration["GeminiSettings:Model"] ?? "gemini-2.0-flash";

            if (string.IsNullOrWhiteSpace(apiKey) || recentTrips.Count < 4 || allowedPreferenceCodes.Count == 0)
            {
                return fallback;
            }

            try
            {
                var payload = BuildGeminiPayload(recentTrips, allowedPreferenceCodes);
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return fallback;
                }

                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                var parsed = ParseGeminiSuggestion(raw);
                if (parsed == null)
                {
                    return fallback;
                }

                var suggested = parsed.SuggestedPreference.Trim();
                if (!allowedPreferenceCodes.Contains(suggested, StringComparer.OrdinalIgnoreCase))
                {
                    return fallback;
                }

                parsed.SuggestedPreference = allowedPreferenceCodes
                    .First(code => code.Equals(suggested, StringComparison.OrdinalIgnoreCase));
                parsed.Source = "ai";
                parsed.Confidence = Math.Clamp(parsed.Confidence, 0m, 1m);

                return parsed;
            }
            catch
            {
                return fallback;
            }
        }

        private static string BuildGeminiPayload(IReadOnlyList<Trip> recentTrips, IReadOnlyCollection<string> allowedPreferenceCodes)
        {
            var compactTrips = recentTrips
                .OrderByDescending(t => t.TripDate)
                .Take(15)
                .Select(t => new
                {
                    t.TripDate,
                    t.DistanceKM,
                    t.Departure,
                    t.Destination,
                    t.SuggestedPreference,
                    t.ChosenPreference
                });

            var prompt = $@"Analyze this user trip history and choose ONE preference code the user is most likely to choose next.

Allowed preference codes:
{string.Join(", ", allowedPreferenceCodes)}

Trip history JSON:
{JsonSerializer.Serialize(compactTrips)}

Return ONLY JSON:
{{""suggestedPreference"":""CODE"",""confidence"":0.0,""reason"":""short explanation""}}";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = prompt
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0,
                    topP = 0,
                    topK = 1,
                    maxOutputTokens = 180,
                    responseMimeType = "application/json"
                }
            };

            return JsonSerializer.Serialize(body);
        }

        private static AiPreferenceSuggestionResult? ParseGeminiSuggestion(string rawResponse)
        {
            using var rootDoc = JsonDocument.Parse(rawResponse);
            if (!rootDoc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var first = candidates[0];
            if (!first.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            {
                return null;
            }

            var text = parts[0].GetProperty("text").GetString() ?? string.Empty;
            text = text.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                       .Trim();

            var json = ExtractFirstJson(text);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AiPreferenceSuggestionResult>(json);
        }

        private static string? ExtractFirstJson(string text)
        {
            var start = text.IndexOf('{');
            if (start < 0) return null;

            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{') depth++;
                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text[start..(i + 1)];
                    }
                }
            }

            return null;
        }

        private static AiPreferenceSuggestionResult BuildFallback(
            IReadOnlyList<Trip> recentTrips,
            IReadOnlyCollection<string> allowedPreferenceCodes)
        {
            var preferred = recentTrips
                .Where(t => !string.IsNullOrWhiteSpace(t.ChosenPreference))
                .GroupBy(t => t.ChosenPreference!.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(preferred))
            {
                var normalized = allowedPreferenceCodes
                    .FirstOrDefault(code => code.Equals(preferred, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return new AiPreferenceSuggestionResult
                    {
                        SuggestedPreference = normalized,
                        Confidence = 0.60m,
                        Reason = "Fallback from recent chosen preference frequency.",
                        Source = "fallback"
                    };
                }
            }

            return new AiPreferenceSuggestionResult
            {
                SuggestedPreference = allowedPreferenceCodes.FirstOrDefault() ?? string.Empty,
                Confidence = 0.50m,
                Reason = "Fallback default preference.",
                Source = "fallback"
            };
        }
    }
}
