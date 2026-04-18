using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;
using System.Text.Json;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilteredPreferenceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FilteredPreferenceController(AppDbContext context)
        {
            _context = context;
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

            var entity = await _context.FilteredPreferences
                .Where(x => x.UserID == request.UserId)
                .OrderByDescending(x => x.AppliedAt)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                return Ok(new { message = "No filtered preference found for this user.", data = (FilteredPreferenceResolvedResponse?)null });
            }

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

        private static FilteredPreferenceResolvedResponse MapEntityToResolvedResponse(FilteredPreference entity)
        {
            return new FilteredPreferenceResolvedResponse
            {
                Id = entity.Id,
                UserID = entity.UserID,
                SelectedPreferenceCode = NormalizeText(entity.SelectedPreferenceCode),
                SelectedPreferencePrompt = NormalizeText(entity.SelectedPreferencePrompt),
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
