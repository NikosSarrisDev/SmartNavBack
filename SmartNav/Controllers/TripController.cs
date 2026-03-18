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

        public TripController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("Create")]
        public async Task<ActionResult> CreateTrip([FromBody] Trip trip)
        {
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
                               }).Take(4).ToListAsync();

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

            await _context.SaveChangesAsync();
            return Ok(new { message = "Updated successfully", data = trip });
        }

        [HttpPost("Delete")]
        public async Task<ActionResult> DeleteTrip([FromBody] UserTripRequest request)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == request.UserId);
            if (trip == null) return Ok(new { message = "Trip not found" });

            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Deleted successfully" });
        }
    }
}