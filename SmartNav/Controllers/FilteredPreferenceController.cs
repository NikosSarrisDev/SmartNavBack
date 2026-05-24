using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;
using System.Globalization;
using System.Text.Json;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilteredPreferenceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public FilteredPreferenceController(
            AppDbContext context,
            IConfiguration configuration)
        {
            _context = context;
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

            var resolverSettings = GetResolverSettings();
            var history = await _context.FilteredPreferences
                .Where(x => x.UserID == request.UserId)
                .OrderByDescending(x => x.AppliedAt)
                .Take(resolverSettings.HistoryLimit)
                .ToListAsync();

            if (!history.Any())
            {
                return Ok(new { message = "No filtered preference found for this user.", data = (FilteredPreferenceResolvedResponse?)null });
            }

            var entity = ResolveBestPreference(history, resolverSettings);
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

        private FilteredPreference ResolveBestPreference(
            IReadOnlyList<FilteredPreference> history,
            PreferenceResolverSettings settings)
        {
            if (history.Count == 1)
            {
                return history[0];
            }

            var now = DateTime.UtcNow;
            var groupedBySignature = history
                .GroupBy(BuildPreferenceSignature)
                .ToDictionary(group => group.Key, group => group.ToList());

            var maxGroupCount = Math.Max(1, groupedBySignature.Values.Max(items => items.Count));
            var historyCount = Math.Max(1, history.Count);
            var inputWeightSum = settings.FrequencyWeight + settings.RecencyWeight + settings.CompletenessWeight + settings.ConsistencyWeight;
            inputWeightSum = inputWeightSum <= 0 ? 1.0 : inputWeightSum;

            FilteredPreference bestItem = history[0];
            var bestScore = double.MinValue;

            foreach (var item in history)
            {
                var signature = BuildPreferenceSignature(item);
                var groupCount = groupedBySignature.TryGetValue(signature, out var signatureItems)
                    ? signatureItems.Count
                    : 1;

                var frequencyNorm = Clamp01((double)groupCount / maxGroupCount);
                var consistencyNorm = Clamp01((double)groupCount / historyCount);
                var recencyNorm = CalculateRecencyWeight(item.AppliedAt, now, settings.RecencyHalfLifeDays);
                var completenessNorm = CalculateCompletenessScore(item, settings);

                var fuzzyScore = InferFuzzyPreferenceScore(
                    frequencyNorm,
                    recencyNorm,
                    completenessNorm,
                    consistencyNorm);

                var weightedInputScore =
                    ((frequencyNorm * settings.FrequencyWeight) +
                     (recencyNorm * settings.RecencyWeight) +
                     (completenessNorm * settings.CompletenessWeight) +
                     (consistencyNorm * settings.ConsistencyWeight)) / inputWeightSum;

                var finalScore =
                    (fuzzyScore * settings.FuzzyInferenceWeight) +
                    (weightedInputScore * (1.0 - settings.FuzzyInferenceWeight));

                // Small tie-break bonus to keep very recent, same-quality choices preferred.
                finalScore += recencyNorm * settings.RecentTieBreakerWeight;

                if (finalScore > bestScore ||
                    (Math.Abs(finalScore - bestScore) < 0.000001 && item.AppliedAt > bestItem.AppliedAt))
                {
                    bestScore = finalScore;
                    bestItem = item;
                }
            }

            return bestItem;
        }

        private PreferenceResolverSettings GetResolverSettings()
        {
            var section = _configuration.GetSection("FilteredPreferenceResolution");

            return new PreferenceResolverSettings
            {
                HistoryLimit = Math.Clamp(section.GetValue("HistoryLimit", 150), 20, 500),
                RecencyHalfLifeDays = Math.Clamp(section.GetValue("RecencyHalfLifeDays", 14.0), 1.0, 120.0),
                FrequencyWeight = Math.Clamp(section.GetValue("FrequencyWeight", 0.35), 0.0, 1.0),
                RecencyWeight = Math.Clamp(section.GetValue("RecencyWeight", 0.35), 0.0, 1.0),
                CompletenessWeight = Math.Clamp(section.GetValue("CompletenessWeight", 0.20), 0.0, 1.0),
                ConsistencyWeight = Math.Clamp(section.GetValue("ConsistencyWeight", 0.10), 0.0, 1.0),
                FuzzyInferenceWeight = Math.Clamp(section.GetValue("FuzzyInferenceWeight", 0.70), 0.0, 1.0),
                RecentTieBreakerWeight = Math.Clamp(section.GetValue("RecentTieBreakerWeight", 0.02), 0.0, 0.15),
                StationPresenceWeight = Math.Clamp(section.GetValue("StationPresenceWeight", 0.20), 0.0, 0.8),
                AdvancedFilterPresenceWeight = Math.Clamp(section.GetValue("AdvancedFilterPresenceWeight", 0.80), 0.2, 1.0)
            };
        }

        private static double CalculateRecencyWeight(DateTime appliedAt, DateTime nowUtc, double halfLifeDays)
        {
            var days = Math.Max(0.0, (nowUtc - appliedAt).TotalDays);
            return 1.0 / (1.0 + (days / Math.Max(1.0, halfLifeDays)));
        }

        private static double CalculateCompletenessScore(FilteredPreference item, PreferenceResolverSettings settings)
        {
            var stationScore = DeserializeStations(item.StationsJson).Any() ? 1.0 : 0.0;

            var advancedFeatures = 0.0;
            if (!string.IsNullOrWhiteSpace(NormalizeText(item.MoodCode))) advancedFeatures += 1.0;
            if (!string.IsNullOrWhiteSpace(NormalizeText(item.VehicleSize))) advancedFeatures += 1.0;
            if (item.AvoidTolls) advancedFeatures += 1.0;
            if (item.AvoidHighways) advancedFeatures += 1.0;
            if (item.AvoidFerries) advancedFeatures += 1.0;
            if (!string.IsNullOrWhiteSpace(NormalizeText(item.TrafficTimeMode)) &&
                !string.Equals(item.TrafficTimeMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                advancedFeatures += 1.0;
            }
            if (item.TrafficStartDateTime.HasValue) advancedFeatures += 1.0;
            if (item.TrafficEndDateTime.HasValue) advancedFeatures += 1.0;
            if (item.IncludeEvChargingStations) advancedFeatures += 1.0;

            var advancedScore = Clamp01(advancedFeatures / 9.0);
            return Clamp01((stationScore * settings.StationPresenceWeight) + (advancedScore * settings.AdvancedFilterPresenceWeight));
        }

        private static double InferFuzzyPreferenceScore(
            double frequencyNorm,
            double recencyNorm,
            double completenessNorm,
            double consistencyNorm)
        {
            var frequencyLow = Trapezoid(frequencyNorm, 0.0, 0.0, 0.28, 0.50);
            var frequencyMedium = Triangle(frequencyNorm, 0.32, 0.58, 0.82);
            var frequencyHigh = Trapezoid(frequencyNorm, 0.68, 0.84, 1.0, 1.0);

            var recencyLow = Trapezoid(recencyNorm, 0.0, 0.0, 0.28, 0.50);
            var recencyMedium = Triangle(recencyNorm, 0.34, 0.58, 0.82);
            var recencyHigh = Trapezoid(recencyNorm, 0.70, 0.86, 1.0, 1.0);

            var consistencyLow = Trapezoid(consistencyNorm, 0.0, 0.0, 0.20, 0.42);
            var consistencyMedium = Triangle(consistencyNorm, 0.24, 0.50, 0.76);
            var consistencyHigh = Trapezoid(consistencyNorm, 0.64, 0.82, 1.0, 1.0);

            var completenessMedium = Triangle(completenessNorm, 0.28, 0.54, 0.78);
            var completenessHigh = Trapezoid(completenessNorm, 0.64, 0.82, 1.0, 1.0);

            var veryHigh = Math.Max(
                Math.Min(frequencyHigh, recencyHigh),
                Math.Min(frequencyHigh, consistencyHigh));

            var high = Math.Max(
                Math.Max(Math.Min(frequencyMedium, recencyHigh), Math.Min(frequencyHigh, recencyMedium)),
                Math.Max(Math.Min(completenessHigh, recencyMedium), Math.Min(consistencyMedium, recencyHigh)));

            var medium = Math.Max(
                Math.Min(frequencyMedium, recencyMedium),
                Math.Max(Math.Min(completenessMedium, recencyMedium), Math.Min(frequencyHigh, recencyLow)));

            var low = Math.Max(
                Math.Min(frequencyLow, recencyLow),
                Math.Min(consistencyLow, recencyLow));

            const double veryLowCenter = 0.12;
            const double lowCenter = 0.30;
            const double mediumCenter = 0.56;
            const double highCenter = 0.78;
            const double veryHighCenter = 0.92;

            var numerator =
                (low * lowCenter) +
                (medium * mediumCenter) +
                (high * highCenter) +
                (veryHigh * veryHighCenter);
            var denominator = low + medium + high + veryHigh;

            if (denominator <= 0)
            {
                return veryLowCenter;
            }

            return Clamp01(numerator / denominator);
        }

        private static double Triangle(double x, double a, double b, double c)
        {
            if (x <= a || x >= c) return 0.0;
            if (Math.Abs(x - b) < double.Epsilon) return 1.0;
            return x < b ? (x - a) / Math.Max(0.000001, b - a) : (c - x) / Math.Max(0.000001, c - b);
        }

        private static double Trapezoid(double x, double a, double b, double c, double d)
        {
            if (x <= a || x >= d) return 0.0;
            if (x >= b && x <= c) return 1.0;
            if (x > a && x < b) return (x - a) / Math.Max(0.000001, b - a);
            return (d - x) / Math.Max(0.000001, d - c);
        }

        private static double Clamp01(double value)
        {
            return Math.Clamp(value, 0.0, 1.0);
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

        private sealed class PreferenceResolverSettings
        {
            public int HistoryLimit { get; set; }
            public double RecencyHalfLifeDays { get; set; }
            public double FrequencyWeight { get; set; }
            public double RecencyWeight { get; set; }
            public double CompletenessWeight { get; set; }
            public double ConsistencyWeight { get; set; }
            public double FuzzyInferenceWeight { get; set; }
            public double RecentTieBreakerWeight { get; set; }
            public double StationPresenceWeight { get; set; }
            public double AdvancedFilterPresenceWeight { get; set; }
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
