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
            var query = from u in _context.Users
                        join r in _context.Roles on u.RoleId equals r.RoleID into roleJoin
                        from r in roleJoin.DefaultIfEmpty()
                        select new
                        {
                            u.Id,
                            u.UserName,
                            u.Name,
                            u.Surname,
                            u.Email,
                            u.Phone,
                            u.IsVerified,
                            u.RoleId,
                            RoleName = r != null ? r.RoleName : null
                        };

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var likePattern = $"%{normalizedSearch}%";
                query = query.Where(u =>
                    (u.UserName != null && EF.Functions.Like(u.UserName.ToLower(), likePattern)) ||
                    (u.Name != null && EF.Functions.Like(u.Name.ToLower(), likePattern)) ||
                    (u.Surname != null && EF.Functions.Like(u.Surname.ToLower(), likePattern)) ||
                    (u.Email != null && EF.Functions.Like(u.Email.ToLower(), likePattern)));
            }

            var users = await query
                .OrderBy(u => u.Id)
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

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.TargetUserId);
            if (!userExists)
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

            var updatedRows = await _context.Users
                .Where(u => u.Id == request.TargetUserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.RoleId, request.NewRoleId));

            if (updatedRows <= 0)
            {
                return StatusCode(500, new { message = "Failed to update user role." });
            }

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
                    Id = request.TargetUserId,
                    RoleId = request.NewRoleId,
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

        [HttpPost("ApplyUserChanges")]
        public async Task<ActionResult> ApplyUserChanges([FromBody] AdminBulkUserChangesRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            if (request.Changes == null || request.Changes.Count == 0)
            {
                return BadRequest(new { message = "No user changes were provided." });
            }

            var changeItems = request.Changes
                .Where(x => x.TargetUserId > 0)
                .GroupBy(x => x.TargetUserId)
                .Select(g => g.Last())
                .ToList();

            if (changeItems.Count == 0)
            {
                return BadRequest(new { message = "No valid user changes were provided." });
            }

            var targetIds = changeItems.Select(x => x.TargetUserId).Distinct().ToList();
            var users = await _context.Users.Where(u => targetIds.Contains(u.Id)).ToListAsync();

            if (users.Count != targetIds.Count)
            {
                return NotFound(new { message = "One or more users were not found." });
            }

            var requiredRoleIds = changeItems
                .Where(x => x.NewRoleId.HasValue)
                .Select(x => x.NewRoleId!.Value)
                .Distinct()
                .ToList();

            var validRoleIds = requiredRoleIds.Count == 0
                ? new HashSet<int>()
                : (await _context.Roles
                    .Where(r => requiredRoleIds.Contains(r.RoleID))
                    .Select(r => r.RoleID)
                    .ToListAsync()).ToHashSet();

            if (requiredRoleIds.Any(roleId => !validRoleIds.Contains(roleId)))
            {
                return NotFound(new { message = "One or more selected roles were not found." });
            }

            var userMap = users.ToDictionary(u => u.Id, u => u);
            var updatedCount = 0;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var change in changeItems)
                {
                    var user = userMap[change.TargetUserId];
                    var changedSomething = false;

                    if (change.NewRoleId.HasValue)
                    {
                        if (change.TargetUserId == request.AdminUserId)
                        {
                            return BadRequest(new { message = "Admin cannot change their own role." });
                        }

                        if (user.RoleId != change.NewRoleId.Value)
                        {
                            user.RoleId = change.NewRoleId.Value;
                            changedSomething = true;
                        }
                    }

                    if (change.IsVerified.HasValue && user.IsVerified != change.IsVerified.Value)
                    {
                        user.IsVerified = change.IsVerified.Value;
                        changedSomething = true;
                    }

                    if (changedSomething)
                    {
                        updatedCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Failed to apply user changes." });
            }

            await TryCreateAuditLogAsync(
                request.AdminUserId,
                null,
                "APPLY_USER_CHANGES",
                $"Applied bulk changes to {updatedCount} users.");

            return Ok(new
            {
                message = "User changes applied successfully.",
                data = new
                {
                    updatedCount
                }
            });
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

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.TargetUserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userTripIds = await _context.Trips
                    .Where(t => t.UserID == request.TargetUserId)
                    .Select(t => t.Id)
                    .ToListAsync();

                if (userTripIds.Count > 0)
                {
                    await _context.Stations
                        .Where(s => userTripIds.Contains(s.TripID))
                        .ExecuteDeleteAsync();
                }

                await _context.Trips.Where(t => t.UserID == request.TargetUserId).ExecuteDeleteAsync();
                await _context.FilteredPreferences.Where(f => f.UserID == request.TargetUserId).ExecuteDeleteAsync();
                await _context.Presets.Where(p => p.UserID == request.TargetUserId).ExecuteDeleteAsync();
                await _context.UserSettings.Where(s => s.UserID == request.TargetUserId).ExecuteDeleteAsync();

                // Keep DBs with strict FKs to audit logs safe.
                await _context.AdminActionLogs
                    .Where(a => a.AdminUserId == request.TargetUserId || a.TargetUserId == request.TargetUserId)
                    .ExecuteDeleteAsync();

                await _context.Users.Where(u => u.Id == request.TargetUserId).ExecuteDeleteAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Failed to delete user and related data." });
            }

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

        [HttpPost("AnalyticsByUser")]
        public async Task<ActionResult> AnalyticsByUser([FromBody] AdminAnalyticsByUserRequest request)
        {
            var adminValidation = await EnsureAdminAsync(request.AdminUserId);
            if (adminValidation != null)
            {
                return adminValidation;
            }

            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new
                {
                    userId = u.Id,
                    userName = u.UserName ?? $"User {u.Id}"
                })
                .ToListAsync();

            if (users.Count == 0)
            {
                return Ok(new
                {
                    message = "success",
                    data = new
                    {
                        users = new List<object>(),
                        currentUserId = 0,
                        analyticsByUser = new List<object>()
                    }
                });
            }

            var vehicles = await _context.Vehicles
                .AsNoTracking()
                .OrderBy(v => v.Id)
                .Select(v => new
                {
                    vehicleId = v.Id,
                    vehicleLabel = v.Label ?? v.Code ?? $"Vehicle {v.Id}",
                    vehicleTranslationField = v.TranslationField
                })
                .ToListAsync();

            var allTrips = await _context.Trips
                .AsNoTracking()
                .Where(t => t.UserID.HasValue)
                .Select(t => new
                {
                    userId = t.UserID!.Value,
                    tripId = t.Id,
                    departure = t.Departure ?? "-",
                    destination = t.Destination ?? "-",
                    tripDate = t.TripDate,
                    vehicleId = t.VehicleID
                })
                .ToListAsync();

            var tripIds = allTrips
                .Where(t => t.tripId.HasValue)
                .Select(t => t.tripId!.Value)
                .Distinct()
                .ToList();

            var stationRows = await _context.Stations
                .AsNoTracking()
                .Where(s => tripIds.Contains(s.TripID))
                .Select(s => new
                {
                    s.TripID,
                    s.Street,
                    s.Number,
                    s.CityArea,
                    s.PostalCode
                })
                .ToListAsync();

            var stationCountMap = stationRows
                .GroupBy(s => s.TripID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(st =>
                        !string.IsNullOrWhiteSpace(st.Street) ||
                        !string.IsNullOrWhiteSpace(st.Number) ||
                        !string.IsNullOrWhiteSpace(st.CityArea) ||
                        !string.IsNullOrWhiteSpace(st.PostalCode)));

            var analyticsByUser = users
                .Select(u =>
                {
                    var userTrips = allTrips
                        .Where(t => t.userId == u.userId)
                        .OrderByDescending(t => t.tripDate)
                        .ToList();

                    var vehicleCountMap = userTrips
                        .GroupBy(t => t.vehicleId ?? -1)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var vehicleUsage = vehicles
                        .Select(v => new
                        {
                            vehicleId = (int?)v.vehicleId,
                            vehicleLabel = v.vehicleLabel,
                            vehicleTranslationField = v.vehicleTranslationField,
                            tripCount = vehicleCountMap.TryGetValue(v.vehicleId, out var count) ? count : 0
                        })
                        .ToList<object>();

                    vehicleUsage.Add(new
                    {
                        vehicleId = (int?)null,
                        vehicleLabel = "Any vehicle (not selected)",
                        vehicleTranslationField = (string?)null,
                        tripCount = vehicleCountMap.TryGetValue(-1, out var anyCount) ? anyCount : 0
                    });

                    var trips = userTrips
                        .Select(t => new
                        {
                            tripId = t.tripId,
                            departure = t.departure,
                            destination = t.destination,
                            tripDate = t.tripDate,
                            stationCount = t.tripId.HasValue && stationCountMap.TryGetValue(t.tripId.Value, out var stationCount) ? stationCount : 0,
                            displayLabel = $"({t.departure} - {t.destination})"
                        })
                        .ToList();

                    return new
                    {
                        userId = u.userId,
                        userName = u.userName,
                        vehicleUsage,
                        trips,
                        selectedTripId = trips.FirstOrDefault()?.tripId ?? 0
                    };
                })
                .ToList();

            var usersWithTrips = analyticsByUser
                .Where(x => x.trips.Count > 0)
                .Select(x => x.userId)
                .ToHashSet();

            var currentUserId = request.TargetUserId > 0 && analyticsByUser.Any(x => x.userId == request.TargetUserId)
                ? request.TargetUserId
                : (
                    usersWithTrips.Contains(request.AdminUserId)
                        ? request.AdminUserId
                        : (analyticsByUser.FirstOrDefault(x => usersWithTrips.Contains(x.userId))?.userId ?? analyticsByUser.First().userId)
                );

            return Ok(new
            {
                message = "success",
                data = new
                {
                    users,
                    currentUserId,
                    analyticsByUser
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
