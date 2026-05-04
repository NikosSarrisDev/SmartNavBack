using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;
using System.Text.Json;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("GetByUser")]
        public async Task<ActionResult> GetByUser([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
            {
                return NotFound(new { message = "User not found." });
            }

            var entity = await _context.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserID == request.UserId);

            var response = entity == null
                ? CreateDefaultSettingsResponse(request.UserId)
                : MapToResponse(entity);

            return Ok(new { message = "success", data = response });
        }

        [HttpPost("SaveByUser")]
        public async Task<ActionResult> SaveByUser([FromBody] UserSettingsUpsertRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
            {
                return NotFound(new { message = "User not found." });
            }

            var entity = await _context.UserSettings
                .FirstOrDefaultAsync(x => x.UserID == request.UserId);

            if (entity == null)
            {
                entity = new UserSettings
                {
                    UserID = request.UserId
                };
                _context.UserSettings.Add(entity);
            }

            ApplyRequestToEntity(request, entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Settings saved successfully.", data = MapToResponse(entity) });
        }

        [HttpPost("ExportData")]
        public async Task<ActionResult> ExportData([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == request.UserId)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.Name,
                    u.Surname,
                    u.Phone,
                    u.IsVerified
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var settings = await _context.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserID == request.UserId);

            var trips = await _context.Trips
                .AsNoTracking()
                .Where(t => t.UserID == request.UserId)
                .Select(t => new
                {
                    t.Id,
                    t.Departure,
                    t.Destination,
                    t.DistanceKM,
                    t.Score,
                    t.TripDate,
                    t.VehicleID
                })
                .OrderByDescending(t => t.TripDate)
                .ToListAsync();

            var stations = await _context.Stations
                .AsNoTracking()
                .Where(s => s.Trip != null && s.Trip.UserID == request.UserId)
                .Select(s => new
                {
                    s.Id,
                    s.TripID,
                    s.Street,
                    s.Number,
                    s.CityArea,
                    s.PostalCode,
                    s.Position
                })
                .ToListAsync();

            var filteredPreferences = await _context.FilteredPreferences
                .AsNoTracking()
                .Where(f => f.UserID == request.UserId)
                .OrderByDescending(f => f.AppliedAt)
                .ToListAsync();

            var presets = await _context.Presets
                .AsNoTracking()
                .Where(p => p.UserID == request.UserId)
                .OrderBy(p => p.Position)
                .Select(p => new
                {
                    p.Id,
                    p.Street,
                    p.Number,
                    p.CityArea,
                    p.PostalCode,
                    p.Position,
                    p.PresetIconId
                })
                .ToListAsync();

            var exportPayload = new
            {
                exportedAtUtc = DateTime.UtcNow,
                user,
                settings = settings == null ? CreateDefaultSettingsResponse(request.UserId) : MapToResponse(settings),
                trips,
                stations,
                filteredPreferences,
                presets
            };

            return Ok(new { message = "success", data = exportPayload });
        }

        [HttpPost("DeleteHistory")]
        public async Task<ActionResult> DeleteHistory([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
            {
                return NotFound(new { message = "User not found." });
            }

            var deletedTrips = await _context.Trips
                .Where(t => t.UserID == request.UserId)
                .ExecuteDeleteAsync();

            return Ok(new { message = "Trip history deleted successfully.", data = new { deletedTrips } });
        }

        [HttpPost("DeleteAccount")]
        public async Task<ActionResult> DeleteAccount([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.UserSettings.Where(x => x.UserID == request.UserId).ExecuteDeleteAsync();
                await _context.FilteredPreferences.Where(x => x.UserID == request.UserId).ExecuteDeleteAsync();
                await _context.Presets.Where(x => x.UserID == request.UserId).ExecuteDeleteAsync();
                await _context.Trips.Where(x => x.UserID == request.UserId).ExecuteDeleteAsync();
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return Ok(new { message = "Account deleted successfully." });
        }

        private static object CreateDefaultSettingsResponse(int userId)
        {
            return new
            {
                userId,
                aiAggressiveness = 3,
                alwaysShowRouteExplanation = true,
                alternativeRoutesCount = 2,
                useHistoryPersonalization = true,
                theme = "system",
                mapStyle = "standard",
                distanceUnit = "km",
                timeFormat = "24h",
                chipDensity = "comfortable",
                largeText = false,
                highContrast = false,
                storeTrips = true,
                storeRatings = true,
                storeStations = true,
                consentLocationHistory = false,
                consentAiTraining = false,
                updatedAt = DateTime.UtcNow
            };
        }

        private static object MapToResponse(UserSettings entity)
        {
            return new
            {
                userId = entity.UserID,
                aiAggressiveness = entity.AiAggressiveness,
                alwaysShowRouteExplanation = entity.AlwaysShowRouteExplanation,
                alternativeRoutesCount = entity.AlternativeRoutesCount,
                useHistoryPersonalization = entity.UseHistoryPersonalization,
                theme = entity.Theme,
                mapStyle = entity.MapStyle,
                distanceUnit = entity.DistanceUnit,
                timeFormat = entity.TimeFormat,
                chipDensity = entity.ChipDensity,
                largeText = entity.LargeText,
                highContrast = entity.HighContrast,
                storeTrips = entity.StoreTrips,
                storeRatings = entity.StoreRatings,
                storeStations = entity.StoreStations,
                consentLocationHistory = entity.ConsentLocationHistory,
                consentAiTraining = entity.ConsentAiTraining,
                updatedAt = entity.UpdatedAt
            };
        }

        private static void ApplyRequestToEntity(UserSettingsUpsertRequest request, UserSettings entity)
        {
            entity.AiAggressiveness = Math.Clamp(request.AiAggressiveness, 1, 5);
            entity.AlwaysShowRouteExplanation = request.AlwaysShowRouteExplanation;
            entity.AlternativeRoutesCount = Math.Clamp(request.AlternativeRoutesCount, 1, 3);
            entity.UseHistoryPersonalization = request.UseHistoryPersonalization;
            entity.Theme = NormalizeByWhitelist(request.Theme, new[] { "light", "dark", "system" }, "system");
            entity.MapStyle = NormalizeByWhitelist(request.MapStyle, new[] { "standard", "satellite", "terrain" }, "standard");
            entity.DistanceUnit = NormalizeByWhitelist(request.DistanceUnit, new[] { "km", "mi" }, "km");
            entity.TimeFormat = NormalizeByWhitelist(request.TimeFormat, new[] { "12h", "24h" }, "24h");
            entity.ChipDensity = NormalizeByWhitelist(request.ChipDensity, new[] { "compact", "comfortable" }, "comfortable");
            entity.LargeText = request.LargeText;
            entity.HighContrast = request.HighContrast;
            entity.StoreTrips = request.StoreTrips;
            entity.StoreRatings = request.StoreRatings;
            entity.StoreStations = request.StoreStations;
            entity.ConsentLocationHistory = request.ConsentLocationHistory;
            entity.ConsentAiTraining = request.ConsentAiTraining;
        }

        private static string NormalizeByWhitelist(string? value, IEnumerable<string> whitelist, string fallback)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return whitelist.Contains(normalized) ? normalized : fallback;
        }
    }
}
