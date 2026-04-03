using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GetlookUpsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GetlookUpsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("Avatars")]
        public async Task<ActionResult> GetAvatars()
        {
            var avatars = await _context.Avatars.ToListAsync();

            if (avatars == null || !avatars.Any())
            {
                return NotFound("Δεν βρέθηκαν Avatars.");
            }

            return Ok(new { data = avatars });
        }

        [HttpPost("Roles")]
        public async Task<ActionResult> GetRoles()
        {
            var roles = await _context.Roles.ToListAsync();

            if (roles == null || !roles.Any())
            {
                return NotFound("Δεν βρέθηκαν Ρόλοι.");
            }

            return Ok(new { data = roles });
        }

        [HttpPost("Preference")]
        public async Task<ActionResult> GetPreference()
        {
            var preferences = await _context.Preferences.ToListAsync();

            if (preferences == null || !preferences.Any())
            {
                return NotFound("Δεν βρέθηκαν Προτιμήσεις.");
            }

            return Ok(new { data = preferences });
        }

        [HttpPost("Vehicle")]
        public async Task<ActionResult> GetVehicle()
        {
            var vehicles = await _context.Vehicles
                .OrderBy(v => v.Id)
                .ToListAsync();

            if (vehicles == null || !vehicles.Any())
            {
                return NotFound("Δεν βρέθηκαν Οχήματα.");
            }

            return Ok(new { data = vehicles });
        }

        [HttpPost("CurrentUserActivePreference")]
        public async Task<ActionResult> GetCurrentUserActivePreference([FromBody] UserTripRequest request)
        {
            var data = await (from us in _context.Users
                              join pf in _context.Preferences on us.PreferenceId equals pf.Id
                              where us.Id == request.UserId
                              select new
                              {
                                  ActivePreference = pf.Id,
                                  pf.Code
                              }).ToListAsync();

            if (data == null)
            {
                return Ok(new { message = "No preferences found for this user." });
            }

            return Ok(new { message = "success", data });
        }

        [HttpPost("CurrentUserRoleAndAvatar")]
        public async Task<ActionResult> GetCurrentUserRole([FromBody] UserTripRequest request)
        {
            var data = await (from us in _context.Users
                               join rl in _context.Roles on us.RoleId equals rl.RoleID
                               join av in _context.Avatars on us.AvatarId equals av.Id
                               where us.Id == request.UserId
                               select new
                               {
                                   rl.RoleName,
                                   av.AvatarURL
                               }).FirstOrDefaultAsync();

            if (data == null)
            {
                return Ok(new { message = "No roles or avatar found for this user." });
            }

            return Ok(new { message = "success", data });
        }
    }
}
