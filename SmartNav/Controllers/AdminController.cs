using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("Dashboard")]
        public async Task<ActionResult> Dashboard([FromBody] AdminBaseRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            var now = DateTime.UtcNow;
            var last7Days = now.AddDays(-7);

            var totalUsers = await _context.Users.CountAsync();
            var verifiedUsers = await _context.Users.CountAsync(u => u.IsVerified);
            var totalTrips = await _context.Trips.CountAsync();
            var totalPresets = await _context.Presets.CountAsync();
            var totalFilteredPreferences = await _context.FilteredPreferences.CountAsync();
            var tripsLast7Days = await _context.Trips.CountAsync(t => t.TripDate >= last7Days);
            var averageScore = await _context.Trips
                .Where(t => t.Score.HasValue)
                .AverageAsync(t => (decimal?)t.Score) ?? 0m;

            return Ok(new
            {
                message = "success",
                data = new
                {
                    totalUsers,
                    verifiedUsers,
                    totalTrips,
                    totalPresets,
                    totalFilteredPreferences,
                    tripsLast7Days,
                    averageScore
                }
            });
        }

        [HttpPost("Users")]
        public async Task<ActionResult> Users([FromBody] AdminUsersRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            var normalizedSearch = request.Search?.Trim().ToLowerInvariant();
            var query = _context.Users
                .Include(u => u.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(normalizedSearch)) ||
                    (u.Name != null && u.Name.ToLower().Contains(normalizedSearch)) ||
                    (u.Surname != null && u.Surname.ToLower().Contains(normalizedSearch)) ||
                    (u.Email != null && u.Email.ToLower().Contains(normalizedSearch)));
            }

            var users = await query
                .OrderBy(u => u.Id)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Name,
                    u.Surname,
                    u.Email,
                    u.Phone,
                    u.IsVerified,
                    u.RoleId,
                    RoleName = u.Role != null ? u.Role.RoleName : null
                })
                .ToListAsync();

            return Ok(new { message = "success", data = users });
        }

        [HttpPost("UpdateUserRole")]
        public async Task<ActionResult> UpdateUserRole([FromBody] AdminUpdateUserRoleRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            if (request.TargetUserId <= 0 || request.NewRoleId <= 0)
            {
                return BadRequest(new { message = "TargetUserId and NewRoleId are required." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (request.TargetUserId == request.AdminUserId)
            {
                return BadRequest(new { message = "Admin cannot change their own role." });
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleID == request.NewRoleId);
            if (role == null)
            {
                return NotFound(new { message = "Role not found." });
            }

            user.RoleId = request.NewRoleId;
            await _context.SaveChangesAsync();
            await TryCreateAuditLogAsync(
                request.AdminUserId,
                request.TargetUserId,
                "UPDATE_USER_ROLE",
                $"Role changed to {request.NewRoleId} ({role.RoleName}).");

            return Ok(new
            {
                message = "User role updated successfully.",
                data = new
                {
                    user.Id,
                    user.RoleId,
                    RoleName = role.RoleName
                }
            });
        }

        [HttpPost("UpdateUserVerification")]
        public async Task<ActionResult> UpdateUserVerification([FromBody] AdminUpdateUserVerificationRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            if (request.TargetUserId <= 0)
            {
                return BadRequest(new { message = "TargetUserId is required." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.IsVerified = request.IsVerified;
            await _context.SaveChangesAsync();
            await TryCreateAuditLogAsync(
                request.AdminUserId,
                request.TargetUserId,
                "UPDATE_USER_VERIFICATION",
                $"Set IsVerified = {request.IsVerified}.");

            return Ok(new { message = "User verification updated successfully." });
        }

        [HttpPost("DeleteUser")]
        public async Task<ActionResult> DeleteUser([FromBody] AdminDeleteUserRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            if (request.TargetUserId <= 0)
            {
                return BadRequest(new { message = "TargetUserId is required." });
            }

            if (request.TargetUserId == request.AdminUserId)
            {
                return BadRequest(new { message = "You cannot delete your own account." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            await TryCreateAuditLogAsync(
                request.AdminUserId,
                request.TargetUserId,
                "DELETE_USER",
                "User account deleted.");

            return Ok(new { message = "User deleted successfully." });
        }

        [HttpPost("AuditLogs")]
        public async Task<ActionResult> AuditLogs([FromBody] AdminAuditLogsRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            var take = request.Take <= 0 ? 100 : Math.Min(request.Take, 500);
            try
            {
                var logs = await (
                    from log in _context.AdminActionLogs
                    join admin in _context.Users on log.AdminUserId equals admin.Id into adminJoin
                    from admin in adminJoin.DefaultIfEmpty()
                    join target in _context.Users on log.TargetUserId equals target.Id into targetJoin
                    from target in targetJoin.DefaultIfEmpty()
                    orderby log.CreatedAt descending
                    select new
                    {
                        log.Id,
                        log.ActionType,
                        log.Details,
                        log.CreatedAt,
                        log.AdminUserId,
                        AdminUserName = admin != null ? admin.UserName : null,
                        log.TargetUserId,
                        TargetUserName = target != null ? target.UserName : null
                    })
                    .Take(take)
                    .ToListAsync();

                return Ok(new { message = "success", data = logs });
            }
            catch
            {
                return Ok(new { message = "Audit log table is not available yet.", data = new List<object>() });
            }
        }

        [HttpPost("Analytics")]
        public async Task<ActionResult> Analytics([FromBody] AdminAnalyticsRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            var vehicleUsage = await (
                from trip in _context.Trips
                join vehicle in _context.Vehicles on trip.VehicleID equals vehicle.Id into vehicleJoin
                from vehicle in vehicleJoin.DefaultIfEmpty()
                group trip by new
                {
                    VehicleId = trip.VehicleID,
                    VehicleLabel = vehicle != null ? vehicle.Label : null,
                    VehicleCode = vehicle != null ? vehicle.Code : null
                }
                into grp
                orderby grp.Count() descending
                select new
                {
                    vehicleId = grp.Key.VehicleId,
                    vehicleLabel = grp.Key.VehicleId == null
                        ? "Any vehicle (not selected)"
                        : (grp.Key.VehicleLabel ?? grp.Key.VehicleCode ?? $"Vehicle {grp.Key.VehicleId}"),
                    tripCount = grp.Count()
                })
                .ToListAsync();

            var stationRows = await _context.Stations
                .Select(s => new
                {
                    s.TripID,
                    s.Street,
                    s.Number,
                    s.CityArea,
                    s.PostalCode
                })
                .ToListAsync();

            var stationCountPerTrip = stationRows
                .GroupBy(s => s.TripID)
                .Select(group => new
                {
                    tripId = group.Key,
                    stationCount = group.Count(st =>
                        !string.IsNullOrWhiteSpace(st.Street) ||
                        !string.IsNullOrWhiteSpace(st.Number) ||
                        !string.IsNullOrWhiteSpace(st.CityArea) ||
                        !string.IsNullOrWhiteSpace(st.PostalCode))
                })
                .ToList();

            var stationBuckets = stationCountPerTrip
                .GroupBy(x => x.stationCount)
                .OrderBy(x => x.Key)
                .Select(x => new
                {
                    stationCount = x.Key,
                    tripCount = x.Count()
                })
                .ToList();

            return Ok(new
            {
                message = "success",
                data = new
                {
                    vehicleUsage,
                    stationBuckets
                }
            });
        }

        private async Task<ActionResult?> EnsureAdminAsync(int adminUserId)
        {
            if (adminUserId <= 0)
            {
                return BadRequest(new { message = "AdminUserId is required." });
            }

            var adminUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == adminUserId);

            if (adminUser == null)
            {
                return NotFound(new { message = "Admin user not found." });
            }

            var roleName = adminUser.Role?.RoleName;
            if (!IsAdminRole(roleName))
            {
                return StatusCode(403, new { message = "Admin permissions required." });
            }

            return null;
        }

        private static bool IsAdminRole(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var normalized = roleName.Trim().ToLowerInvariant();
            return normalized.Contains("admin") || normalized.Contains("διαχ");
        }

        private async Task TryCreateAuditLogAsync(
            int adminUserId,
            int? targetUserId,
            string actionType,
            string? details)
        {
            try
            {
                _context.AdminActionLogs.Add(new AdminActionLog
                {
                    AdminUserId = adminUserId,
                    TargetUserId = targetUserId,
                    ActionType = actionType,
                    Details = details
                });
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Keep main admin actions working even if audit table is not yet migrated.
            }
        }
    }
}
