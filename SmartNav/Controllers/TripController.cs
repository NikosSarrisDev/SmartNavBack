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
            return Ok(new { message = "Trip created", data = trip });
        }

        [HttpPost("ListAll")]
        public async Task<ActionResult> GetAllTrips()
        {
            var trips = await _context.Trips.ToListAsync();
            return Ok(new { data = trips });
        }

        [HttpPost("GetUserTripDetails")]
        public async Task<ActionResult> GetUserTripDetails([FromBody] UserTripInfoDTO request)
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
                                   DistanceKM = tr.DistanceKM,
                                   Score = tr.Score
                               }).ToListAsync();

            if (query == null || !query.Any())
            {
                return Ok(new { message = "No trips found for this user." });
            }

            return Ok(new { message = "success", data = query });
        }

        [HttpPost("Update")]
        public async Task<ActionResult> UpdateTrip([FromBody] Trip updatedTrip)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == updatedTrip.Id);
            if (trip == null) return Ok(new { message = "Trip not found" });

            trip.Destination = updatedTrip.Destination;
            trip.DistanceKM = updatedTrip.DistanceKM;
            trip.Score = updatedTrip.Score;
            trip.TripDate = updatedTrip.TripDate;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Updated successfully", data = trip });
        }

        [HttpPost("Delete")]
        public async Task<ActionResult> DeleteTrip([FromBody] int id)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return Ok(new { message = "Trip not found" });

            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Deleted successfully" });
        }
    }
}