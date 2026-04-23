using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Models;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const decimal DuplicateDistanceToleranceKm = 0.30m;
        private const int DuplicateTimeWindowMinutes = 30;

        public TripController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("Create")]
        public async Task<ActionResult> CreateTrip([FromBody] TripCreateRequest request)
        {
            if (request.UserID == null || request.UserID <= 0)
            {
                return BadRequest(new { message = "UserID is required." });
            }

            var resolvedVehicleId = await ResolveVehicleIdAsync(request);
            if (resolvedVehicleId == -1)
            {
                return BadRequest(new { message = "Invalid vehicle option." });
            }

            var trip = new Trip
            {
                UserID = request.UserID,
                Destination = request.Destination,
                Departure = request.Departure,
                DistanceKM = request.DistanceKM,
                Score = request.Score,
                TripDate = request.TripDate,
                VehicleID = resolvedVehicleId > 0 ? resolvedVehicleId : null
            };

            var incomingTripDate = trip.TripDate == default ? DateTime.UtcNow : trip.TripDate;
            trip.TripDate = incomingTripDate;

            var candidateWindowStart = incomingTripDate.AddMinutes(-DuplicateTimeWindowMinutes);
            var candidateWindowEnd = incomingTripDate.AddMinutes(DuplicateTimeWindowMinutes);

            var existingTrips = await _context.Trips
                .Where(t => t.UserID == trip.UserID && t.TripDate >= candidateWindowStart && t.TripDate <= candidateWindowEnd)
                .ToListAsync();

            var duplicateTrip = existingTrips.FirstOrDefault(existing => IsSameTrip(existing, trip));
            if (duplicateTrip != null)
            {
                return Ok(new
                {
                    message = "Trip already exists. Existing record returned.",
                    duplicate = true,
                    data = duplicateTrip
                });
            }

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            var stations = BuildStations(request.Stations, trip.Id ?? 0);
            if (stations.Any())
            {
                _context.Stations.AddRange(stations);
                await _context.SaveChangesAsync();
            }

            var createdTrip = await _context.Trips
                .Where(t => t.Id == trip.Id)
                .Select(t => new
                {
                    t.Id,
                    t.UserID,
                    t.Departure,
                    t.Destination,
                    t.DistanceKM,
                    t.Score,
                    t.TripDate,
                    t.VehicleID,
                    VehicleCode = t.Vehicle != null ? t.Vehicle.Code : null,
                    Stations = t.Stations!
                        .OrderBy(s => s.Position)
                        .Select(s => new
                        {
                            s.Id,
                            s.Street,
                            s.Number,
                            s.CityArea,
                            s.PostalCode,
                            s.Position
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(t => t.Id == trip.Id);

            if (createdTrip == null)
            {
                return Ok(new { message = "Trip created successfully", data = trip });
            }

            return Ok(new { message = "Trip created successfully", data = createdTrip });
        }

        [HttpPost("ListAll")]
        public async Task<ActionResult> GetAllTrips()
        {
            var trips = await _context.Trips.ToListAsync();
            return Ok(new { data = trips });
        }

        [HttpPost("GetUserTripDetails")]
        public async Task<ActionResult> GetUserTripDetails([FromBody] UserTripRequest request)
        {
            var query = await (from us in _context.Users
                               join tr in _context.Trips on us.Id equals tr.UserID
                               where us.Id == request.UserId
                               select new Trip
                               {
                                   Id = tr.Id,
                                   UserID = tr.UserID,
                                   TripDate = tr.TripDate,
                                   Destination = tr.Destination,
                                   Departure = tr.Departure,
                                   DistanceKM = tr.DistanceKM,
                                   Score = tr.Score,
                                   VehicleID = tr.VehicleID
                               }).OrderByDescending(t => t.TripDate).Take(4).ToListAsync();

            var statistics = await _context.Trips
                .Where(tr => tr.UserID == request.UserId)
                .GroupBy(tr => tr.UserID)
                .Select(g => new
                {
                    TotalTrips = g.Count(),
                    TotalDistance = g.Sum(tr => tr.DistanceKM)
                })
                .FirstOrDefaultAsync();

            if (query == null || !query.Any() || statistics == null)
            {
                return Ok(new { message = "No trips found for this user." });
            }

            return Ok(new { message = "success", data = query, statistics = statistics });
        }

        [HttpPost("Update")]
        public async Task<ActionResult> UpdateTrip([FromBody] Trip updatedTrip)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == updatedTrip.Id);
            if (trip == null) return Ok(new { message = "Trip not found" });

            trip.Destination = updatedTrip.Destination;
            trip.Departure = updatedTrip.Departure;
            trip.DistanceKM = updatedTrip.DistanceKM;
            trip.Score = updatedTrip.Score;
            trip.TripDate = updatedTrip.TripDate;
            trip.VehicleID = updatedTrip.VehicleID;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Updated successfully", data = trip });
        }

        [HttpPost("Rate")]
        public async Task<ActionResult> RateTrip([FromBody] TripRateRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest(new { message = "UserId is required." });
            }

            if (request.Score < 1 || request.Score > 5)
            {
                return BadRequest(new { message = "Score must be between 1 and 5." });
            }

            Trip? trip = null;

            if (request.TripId.HasValue && request.TripId.Value > 0)
            {
                trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.Id == request.TripId.Value && t.UserID == request.UserId);
            }

            if (trip == null)
            {
                trip = await _context.Trips
                    .Where(t => t.UserID == request.UserId)
                    .OrderByDescending(t => t.TripDate)
                    .ThenByDescending(t => t.Id)
                    .FirstOrDefaultAsync();
            }

            if (trip == null)
            {
                return NotFound(new { message = "Trip not found." });
            }

            trip.Score = request.Score;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Trip rating saved successfully.", data = trip });
        }

        [HttpPost("Delete")]
        public async Task<ActionResult> DeleteTrip([FromBody] TripDeleteRequest request)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == request.TripId && t.UserID == request.UserId);
            if (trip == null) return Ok(new { message = "Trip not found" });

            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Deleted successfully" });
        }

        private static bool IsSameTrip(Trip existingTrip, Trip incomingTrip)
        {
            if (existingTrip.UserID != incomingTrip.UserID)
            {
                return false;
            }

            var sameDestination = NormalizeText(existingTrip.Destination) == NormalizeText(incomingTrip.Destination);
            var sameDeparture = NormalizeText(existingTrip.Departure) == NormalizeText(incomingTrip.Departure);
            var sameVehicle = existingTrip.VehicleID == incomingTrip.VehicleID;

            if (!sameDestination || !sameDeparture || !sameVehicle)
            {
                return false;
            }

            if (existingTrip.DistanceKM.HasValue && incomingTrip.DistanceKM.HasValue)
            {
                var distanceDiff = Math.Abs(existingTrip.DistanceKM.Value - incomingTrip.DistanceKM.Value);
                if (distanceDiff > DuplicateDistanceToleranceKm)
                {
                    return false;
                }
            }

            var tripDateDiff = Math.Abs((existingTrip.TripDate - incomingTrip.TripDate).TotalMinutes);
            return tripDateDiff <= DuplicateTimeWindowMinutes;
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private async Task<int> ResolveVehicleIdAsync(TripCreateRequest request)
        {
            if (request.VehicleID.HasValue && request.VehicleID.Value > 0)
            {
                var exists = await _context.Vehicles.AnyAsync(v => v.Id == request.VehicleID.Value);
                return exists ? request.VehicleID.Value : -1;
            }

            if (!string.IsNullOrWhiteSpace(request.VehicleCode))
            {
                var vehicleCode = request.VehicleCode.Trim().ToLowerInvariant();
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Code != null && v.Code.ToLower() == vehicleCode);
                return vehicle?.Id ?? -1;
            }

            return 0;
        }

        private static List<Station> BuildStations(IEnumerable<StationCreateRequest>? incomingStations, int tripId)
        {
            if (tripId <= 0)
            {
                return new List<Station>();
            }

            var stations = (incomingStations ?? Enumerable.Empty<StationCreateRequest>())
                .Select((station, index) => new Station
                {
                    TripID = tripId,
                    Street = station.Street?.Trim(),
                    Number = station.Number?.Trim(),
                    CityArea = station.CityArea?.Trim(),
                    PostalCode = station.PostalCode?.Trim(),
                    Position = station.Position.HasValue && station.Position.Value > 0 ? station.Position.Value : index + 1
                })
                .Where(HasAddressData)
                .ToList();

            if (!stations.Any())
            {
                return new List<Station>
                {
                    new Station
                    {
                        TripID = tripId,
                        Street = null,
                        Number = null,
                        CityArea = null,
                        PostalCode = null,
                        Position = null
                    }
                };
            }

            return stations;
        }

        private static bool HasAddressData(Station station)
        {
            return
                !string.IsNullOrWhiteSpace(station.Street) ||
                !string.IsNullOrWhiteSpace(station.Number) ||
                !string.IsNullOrWhiteSpace(station.CityArea) ||
                !string.IsNullOrWhiteSpace(station.PostalCode);
        }
    }
}
