using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilteredPreferenceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public FilteredPreferenceController(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpPost("Create")]
        public async Task<ActionResult> Create([FromBody] FilteredPreferenceUpsertRequest request)
        {
            var validationError = await ValidateRequestAsync(request);
            if (validationError != null)
            {
                return validationError;
            }

            var entity = new FilteredPreference();
            MapRequestToEntity(request, entity);
            entity.AppliedAt = DateTime.UtcNow;

            _context.FilteredPreferences.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Filtered preference saved successfully.", data = entity });
        }

        [HttpPost("ListAll")]
        public async Task<ActionResult> ListAll()
        {
            var items = await _context.FilteredPreferences
                .OrderByDescending(x => x.AppliedAt)
                .ToListAsync();

            return Ok(new { data = items });
        }

        [HttpPost("GetByUser")]
        public async Task<ActionResult> GetByUser([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var items = await _context.FilteredPreferences
                .Where(x => x.UserID == request.UserId)
                .OrderByDescending(x => x.AppliedAt)
                .ToListAsync();

            return Ok(new { data = items });
        }

        [HttpPost("GetLatestByUser")]
        public async Task<ActionResult> GetLatestByUser([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var history = await _context.FilteredPreferences
                .Where(x => x.UserID == request.UserId)
                .OrderByDescending(x => x.AppliedAt)
                .Take(150)
                .ToListAsync();

            if (!history.Any())
            {
                return Ok(new { message = "No filtered preference found for this user.", data = (FilteredPreferenceResolvedResponse?)null });
            }

            var entity = await ResolveBestPreferenceAsync(history, HttpContext.RequestAborted);
            var resolved = MapEntityToResolvedResponse(entity);
            return Ok(new { message = "success", data = resolved });
        }

        [HttpPost("Update")]
        public async Task<ActionResult> Update([FromBody] FilteredPreferenceUpsertRequest request)
        {
            if (request.Id == null || request.Id <= 0)
            {
                return BadRequest(new { message = "Id is required for update." });
            }

            var entity = await _context.FilteredPreferences.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (entity == null)
            {
                return NotFound(new { message = "Filtered preference not found." });
            }

            var validationError = await ValidateRequestAsync(request);
            if (validationError != null)
            {
                return validationError;
            }

            MapRequestToEntity(request, entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Filtered preference updated successfully.", data = entity });
        }

        [HttpPost("Delete")]
        public async Task<ActionResult> Delete([FromBody] FilteredPreferenceDeleteRequest request)
        {
            if (request.Id == null || request.Id <= 0)
            {
                return BadRequest(new { message = "Id is required for delete." });
            }

            var entity = await _context.FilteredPreferences.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (entity == null)
            {
                return NotFound(new { message = "Filtered preference not found." });
            }

            if (request.UserID.HasValue && request.UserID.Value > 0 && entity.UserID != request.UserID)
            {
                return BadRequest(new { message = "Filtered preference does not belong to this user." });
            }

            _context.FilteredPreferences.Remove(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Filtered preference deleted successfully." });
        }

        private async Task<ActionResult?> ValidateRequestAsync(FilteredPreferenceUpsertRequest request)
        {
            if (request.UserID == null || request.UserID <= 0)
            {
                return BadRequest(new { message = "UserID is required." });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserID.Value);
            if (!userExists)
            {
                return BadRequest(new { message = "User not found." });
            }

            return null;
        }

        private static void MapRequestToEntity(FilteredPreferenceUpsertRequest request, FilteredPreference entity)
        {
            entity.UserID = request.UserID;
            entity.SelectedPreferenceCode = NormalizeText(request.SelectedPreferenceCode);
            entity.SelectedPreferencePrompt = NormalizeText(request.SelectedPreferencePrompt);
            entity.MoodCode = NormalizeText(request.MoodCode);
            entity.VehicleSize = NormalizeText(request.VehicleSize);
            entity.AvoidTolls = request.AvoidTolls;
            entity.AvoidHighways = request.AvoidHighways;
            entity.AvoidFerries = request.AvoidFerries;
            entity.TrafficTimeMode = NormalizeText(request.TrafficTimeMode);
            entity.TrafficStartDateTime = request.TrafficStartDateTime;
            entity.TrafficEndDateTime = request.TrafficEndDateTime;
            entity.IncludeEvChargingStations = request.IncludeEvChargingStations;
            entity.StationsJson = SerializeStations(request.Stations);
        }

        private static string? NormalizeText(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static string? SerializeStations(List<FilteredPreferenceStationRequest>? stations)
        {
            var normalizedStations = (stations ?? new List<FilteredPreferenceStationRequest>())
                .Where(station =>
                    !string.IsNullOrWhiteSpace(station.Street) ||
                    !string.IsNullOrWhiteSpace(station.Number) ||
                    !string.IsNullOrWhiteSpace(station.CityArea) ||
                    !string.IsNullOrWhiteSpace(station.PostalCode))
                .Select(station => new
                {
                    Street = NormalizeText(station.Street),
                    Number = NormalizeText(station.Number),
                    CityArea = NormalizeText(station.CityArea),
                    PostalCode = NormalizeText(station.PostalCode)
                })
                .ToList();

            if (!normalizedStations.Any())
            {
                return null;
            }

            return JsonSerializer.Serialize(normalizedStations);
        }

        private async Task<FilteredPreference> ResolveBestPreferenceAsync(
            IReadOnlyList<FilteredPreference> history,
            CancellationToken cancellationToken)
        {
            if (history.Count == 1)
            {
                return history[0];
            }

            var aiSelectedId = await TryResolveBestPreferenceIdWithAiAsync(history, cancellationToken);
            if (aiSelectedId.HasValue)
            {
                var aiSelected = history.FirstOrDefault(x => x.Id == aiSelectedId.Value);
                if (aiSelected != null)
                {
                    return aiSelected;
                }
            }

            return ResolveBestPreferenceFallback(history);
        }

        private async Task<int?> TryResolveBestPreferenceIdWithAiAsync(
            IReadOnlyList<FilteredPreference> history,
            CancellationToken cancellationToken)
        {
            var apiKey = _configuration["GeminiSettings:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var model = _configuration["GeminiSettings:Model"] ?? "gemini-2.0-flash";
            var requestItems = history
                .Take(60)
                .Select(item => new
                {
                    item.Id,
                    item.AppliedAt,
                    item.SelectedPreferenceCode,
                    item.MoodCode,
                    item.VehicleSize,
                    item.AvoidTolls,
                    item.AvoidHighways,
                    item.AvoidFerries,
                    item.TrafficTimeMode,
                    item.IncludeEvChargingStations,
                    Stations = DeserializeStations(item.StationsJson)
                        .Select(station => new
                        {
                            station.Street,
                            station.Number,
                            station.CityArea,
                            station.PostalCode
                        })
                        .ToList()
                })
                .ToList();

            var prompt = $@"You receive a user's history of applied navigation filter sets.
Select exactly ONE record id that best represents the user's likely preferred default filters.
Prioritize repeated patterns and recent behavior.

History JSON:
{JsonSerializer.Serialize(requestItems)}

Return ONLY JSON in this format:
{{""selectedId"":123,""confidence"":0.0,""reason"":""short text""}}";

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
                    maxOutputTokens = 140,
                    responseMimeType = "application/json"
                }
            };

            var payload = JsonSerializer.Serialize(body);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                var parsedId = ParseGeminiSelectionId(raw);
                if (!parsedId.HasValue)
                {
                    return null;
                }

                return history.Any(item => item.Id == parsedId.Value) ? parsedId.Value : null;
            }
            catch
            {
                return null;
            }
        }

        private static int? ParseGeminiSelectionId(string rawResponse)
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

            using var parsed = JsonDocument.Parse(json);
            if (!parsed.RootElement.TryGetProperty("selectedId", out var selectedIdElement))
            {
                return null;
            }

            return selectedIdElement.TryGetInt32(out var selectedId) ? selectedId : null;
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

        private static FilteredPreference ResolveBestPreferenceFallback(IReadOnlyList<FilteredPreference> history)
        {
            var now = DateTime.UtcNow;
            var bestGroup = history
                .GroupBy(BuildPreferenceSignature)
                .Select(group => new
                {
                    Items = group.ToList(),
                    Score = group.Sum(item => CalculateRecencyWeight(item.AppliedAt, now))
                })
                .OrderByDescending(group => group.Score)
                .ThenByDescending(group => group.Items.Count)
                .ThenByDescending(group => group.Items.Max(item => item.AppliedAt))
                .First();

            return bestGroup.Items
                .OrderByDescending(item => item.AppliedAt)
                .First();
        }

        private static double CalculateRecencyWeight(DateTime appliedAt, DateTime nowUtc)
        {
            var days = Math.Max(0.0, (nowUtc - appliedAt).TotalDays);
            return 1.0 / (1.0 + (days / 14.0));
        }

        private static string BuildPreferenceSignature(FilteredPreference item)
        {
            var selectedPreferenceCode = NormalizeText(item.SelectedPreferenceCode)?.ToUpperInvariant() ?? string.Empty;
            var moodCode = NormalizeText(item.MoodCode)?.ToUpperInvariant() ?? string.Empty;
            var vehicleSize = NormalizeText(item.VehicleSize)?.ToUpperInvariant() ?? string.Empty;
            var trafficTimeMode = NormalizeText(item.TrafficTimeMode)?.ToUpperInvariant() ?? string.Empty;
            var startHour = item.TrafficStartDateTime.HasValue
                ? item.TrafficStartDateTime.Value.ToString("HH", CultureInfo.InvariantCulture)
                : string.Empty;
            var endHour = item.TrafficEndDateTime.HasValue
                ? item.TrafficEndDateTime.Value.ToString("HH", CultureInfo.InvariantCulture)
                : string.Empty;
            var stationFingerprint = BuildStationFingerprint(item.StationsJson);

            return string.Join("|", new[]
            {
                selectedPreferenceCode,
                moodCode,
                vehicleSize,
                item.AvoidTolls ? "1" : "0",
                item.AvoidHighways ? "1" : "0",
                item.AvoidFerries ? "1" : "0",
                trafficTimeMode,
                startHour,
                endHour,
                item.IncludeEvChargingStations ? "1" : "0",
                stationFingerprint
            });
        }

        private static string BuildStationFingerprint(string? stationsJson)
        {
            var stations = DeserializeStations(stationsJson);
            if (!stations.Any())
            {
                return string.Empty;
            }

            return string.Join(";",
                stations
                    .Select(station =>
                        $"{NormalizeText(station.Street)?.ToUpperInvariant() ?? string.Empty}|" +
                        $"{NormalizeText(station.Number)?.ToUpperInvariant() ?? string.Empty}|" +
                        $"{NormalizeText(station.CityArea)?.ToUpperInvariant() ?? string.Empty}|" +
                        $"{NormalizeText(station.PostalCode)?.ToUpperInvariant() ?? string.Empty}")
                    .OrderBy(value => value));
        }

        private static FilteredPreferenceResolvedResponse MapEntityToResolvedResponse(FilteredPreference entity)
        {
            return new FilteredPreferenceResolvedResponse
            {
                Id = entity.Id,
                UserID = entity.UserID,
                SelectedPreferenceCode = NormalizeText(entity.SelectedPreferenceCode),
                SelectedPreferencePrompt = NormalizeText(entity.SelectedPreferencePrompt),
                MoodCode = NormalizeText(entity.MoodCode),
                VehicleSize = NormalizeText(entity.VehicleSize),
                AvoidTolls = entity.AvoidTolls,
                AvoidHighways = entity.AvoidHighways,
                AvoidFerries = entity.AvoidFerries,
                TrafficTimeMode = NormalizeText(entity.TrafficTimeMode),
                TrafficStartDateTime = entity.TrafficStartDateTime,
                TrafficEndDateTime = entity.TrafficEndDateTime,
                IncludeEvChargingStations = entity.IncludeEvChargingStations,
                Stations = DeserializeStations(entity.StationsJson),
                AppliedAt = entity.AppliedAt,
                HasStoredNonStationFilters = HasStoredNonStationFilters(entity)
            };
        }

        private static List<FilteredPreferenceStationRequest> DeserializeStations(string? stationsJson)
        {
            if (string.IsNullOrWhiteSpace(stationsJson))
            {
                return new List<FilteredPreferenceStationRequest>();
            }

            try
            {
                var stations = JsonSerializer.Deserialize<List<FilteredPreferenceStationRequest>>(
                    stationsJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<FilteredPreferenceStationRequest>();

                return stations
                    .Select(station => new FilteredPreferenceStationRequest
                    {
                        Street = NormalizeText(station.Street),
                        Number = NormalizeText(station.Number),
                        CityArea = NormalizeText(station.CityArea),
                        PostalCode = NormalizeText(station.PostalCode)
                    })
                    .Where(station =>
                        !string.IsNullOrWhiteSpace(station.Street) ||
                        !string.IsNullOrWhiteSpace(station.Number) ||
                        !string.IsNullOrWhiteSpace(station.CityArea) ||
                        !string.IsNullOrWhiteSpace(station.PostalCode))
                    .ToList();
            }
            catch
            {
                return new List<FilteredPreferenceStationRequest>();
            }
        }

        private static bool HasStoredNonStationFilters(FilteredPreference entity)
        {
            var selectedPreferenceCode = NormalizeText(entity.SelectedPreferenceCode);
            var hasNonDefaultPreference =
                !string.IsNullOrWhiteSpace(selectedPreferenceCode) &&
                !selectedPreferenceCode.Equals("fast", StringComparison.OrdinalIgnoreCase) &&
                !selectedPreferenceCode.Equals("fastest", StringComparison.OrdinalIgnoreCase);
            var hasTrafficMode =
                !string.IsNullOrWhiteSpace(entity.TrafficTimeMode) &&
                !entity.TrafficTimeMode.Equals("none", StringComparison.OrdinalIgnoreCase);

            return
                hasNonDefaultPreference ||
                !string.IsNullOrWhiteSpace(entity.MoodCode) ||
                !string.IsNullOrWhiteSpace(entity.VehicleSize) ||
                entity.AvoidTolls ||
                entity.AvoidHighways ||
                entity.AvoidFerries ||
                hasTrafficMode ||
                entity.TrafficStartDateTime.HasValue ||
                entity.TrafficEndDateTime.HasValue ||
                entity.IncludeEvChargingStations;
        }
    }
}
