using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Interfaces;
using SmartNav.Models;
using SmartNav.Services;

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

        [HttpGet("Avatars")]
        public async Task<ActionResult> GetAvatars()
        {
            var avatars = await _context.Avatars.ToListAsync();

            if (avatars == null || !avatars.Any())
            {
                return NotFound("Δεν βρέθηκαν Avatars.");
            }

            return Ok(avatars);
        }

        [HttpGet("Roles")]
        public async Task<ActionResult> GetRoles()
        {
            var roles = await _context.Roles.ToListAsync();

            if (roles == null || !roles.Any())
            {
                return NotFound("Δεν βρέθηκαν Ρόλοι.");
            }

            return Ok(roles);
        }
    }
}
