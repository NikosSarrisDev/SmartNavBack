using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNav.Data;
using SmartNav.Interfaces;
using SmartNav.Models;

namespace SmartNav.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAiSuggestionService _aiSuggestionService;
        private const decimal DuplicateDistanceToleranceKm = 0.30m;
        private const int DuplicateTimeWindowMinutes = 30;

        public TripController(AppDbContext context, IAiSuggestionService aiSuggestionService)
        {
            _context = context;
            _aiSuggestionService = aiSuggestionService;
        }

        [HttpPost("Create")]
        public async Task<ActionResult> CreateTrip([FromBody] Trip trip)
        {
            if (trip.UserID == null || trip.UserID <= 0)
            {
                return BadRequest(new { message = "UserID is required." });
            }

            if (string.IsNullOrWhiteSpace(trip.SuggestedPreference))
            {
                trip.SuggestedPreference = trip.ChosenPreference;
            }

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
            return Ok(new { message = "Trip created successfully", data = trip });
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
                                   Score = tr.Score
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

        [HttpPost("GetAISuggestions")]
        public async Task<ActionResult> AnalyzeUserBehavior([FromBody] UserTripRequest request)
        {
            var history = await _context.Trips
                .Where(t => t.UserID == request.UserId)
                .OrderByDescending(t => t.TripDate)
                .Take(20)
                .ToListAsync();

            var preferenceCodes = await _context.Preferences
                .Where(p => !string.IsNullOrWhiteSpace(p.Code))
                .Select(p => p.Code!)
                .ToListAsync();

            var suggestion = await _aiSuggestionService.GetSuggestedPreferenceAsync(history, preferenceCodes);

            return Ok(new
            {
                message = "success",
                data = suggestion
            });
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

            await _context.SaveChangesAsync();
            return Ok(new { message = "Updated successfully", data = trip });
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
            var sameChosenPreference = NormalizeText(existingTrip.ChosenPreference) == NormalizeText(incomingTrip.ChosenPreference);
            var sameSuggestedPreference = NormalizeText(existingTrip.SuggestedPreference) == NormalizeText(incomingTrip.SuggestedPreference);

            if (!sameDestination || !sameDeparture || !sameChosenPreference || !sameSuggestedPreference)
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
    }
}
