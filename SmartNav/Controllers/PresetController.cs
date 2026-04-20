using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PresetController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PresetController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("Icons")]
        public async Task<ActionResult> GetPresetIcons()
        {
            var icons = await _context.PresetIcons
                .OrderBy(x => x.Id)
                .ToListAsync();

            if (!icons.Any())
            {
                return NotFound(new { message = "Preset icons were not found." });
            }

            return Ok(new { data = icons });
        }

        [HttpPost("Create")]
        public async Task<ActionResult> Create([FromBody] PresetCreateRequest request)
        {
            if (request.UserID == null || request.UserID <= 0)
            {
                return BadRequest(new { message = "UserID is required." });
            }

            if (request.PresetIconId == null || request.PresetIconId <= 0)
            {
                return BadRequest(new { message = "PresetIconId is required." });
            }

            var userExists = await _context.Users.AnyAsync(x => x.Id == request.UserID.Value);
            if (!userExists)
            {
                return BadRequest(new { message = "User not found." });
            }

            var iconExists = await _context.PresetIcons.AnyAsync(x => x.Id == request.PresetIconId.Value);
            if (!iconExists)
            {
                return BadRequest(new { message = "Preset icon not found." });
            }

            var nextPosition = await _context.Presets
                .Where(x => x.UserID == request.UserID)
                .Select(x => (int?)x.Position)
                .MaxAsync() ?? 0;

            var preset = new Preset
            {
                UserID = request.UserID,
                Street = NormalizeText(request.Street),
                Number = NormalizeText(request.Number),
                CityArea = NormalizeText(request.CityArea),
                PostalCode = NormalizeText(request.PostalCode),
                Position = nextPosition + 1,
                PresetIconId = request.PresetIconId
            };

            if (!HasAnyAddressData(preset))
            {
                return BadRequest(new { message = "At least one address field is required." });
            }

            _context.Presets.Add(preset);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Preset saved successfully.",
                data = preset
            });
        }

        [HttpPost("GetByUser")]
        public async Task<ActionResult> GetByUser([FromBody] UserTripRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            var presets = await _context.Presets
                .Where(x => x.UserID == request.UserId)
                .OrderBy(x => x.Position ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.UserID,
                    x.Street,
                    x.Number,
                    x.CityArea,
                    x.PostalCode,
                    x.Position,
                    x.PresetIconId,
                    IconData = x.PresetIcon != null ? x.PresetIcon.IconData : null,
                    TranslationField = x.PresetIcon != null ? x.PresetIcon.TranslationField : null
                })
                .ToListAsync();

            return Ok(new { data = presets });
        }

        private static string? NormalizeText(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static bool HasAnyAddressData(Preset preset)
        {
            return
                !string.IsNullOrWhiteSpace(preset.Street) ||
                !string.IsNullOrWhiteSpace(preset.Number) ||
                !string.IsNullOrWhiteSpace(preset.CityArea) ||
                !string.IsNullOrWhiteSpace(preset.PostalCode);
        }
    }
}
