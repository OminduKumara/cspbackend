using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using tmsserver.Data.Repositories;
using tmsserver.Models;

namespace tmsserver.Controllers
{
    // This sets the base URL for these endpoints to: http://localhost:[port]/api/practicesessions
    [Route("api/[controller]")]
    [ApiController]
    public class PracticeSessionsController : ControllerBase
    {
        private readonly PracticeSessionRepository _repository;
        private readonly IUserRepository _userRepository;

        // We pull the configuration to pass the connection string down to the repository
        public PracticeSessionsController(IConfiguration configuration, IUserRepository userRepository)
        {
            _repository = new PracticeSessionRepository(configuration);
            _userRepository = userRepository;
        }

        // 1. GET: api/practicesessions
        // React uses this to fetch and display the schedule
        [HttpGet]
        public ActionResult<IEnumerable<PracticeSession>> Get()
        {
            var sessions = _repository.GetAllSessions();
            return Ok(sessions);
        }

        // 2. POST: api/practicesessions
        // Admin dashboard uses this to create a new session
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Post([FromBody] PracticeSession session)
        {
            _repository.AddSession(session);
            return Ok(new { message = "Practice session added successfully!" });
        }

        // 3. PUT: api/practicesessions/5
        // Admin dashboard uses this to update an existing session
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Put(int id, [FromBody] PracticeSession session)
        {
            session.Id = id; // Ensure the ID from the URL matches the data
            _repository.UpdateSession(session);
            return Ok(new { message = "Practice session updated successfully!" });
        }

        // 4. DELETE: api/practicesessions/5
        // Admin dashboard uses this to remove a session
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public ActionResult Delete(int id)
        {
            _repository.DeleteSession(id);
            return Ok(new { message = "Practice session deleted successfully!" });
        }

        [HttpGet("{id}/attendance")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAttendanceGrid(int id, [FromQuery] DateTime? attendanceDate = null)
        {
            var date = (attendanceDate ?? DateTime.UtcNow).Date;
            var session = _repository.GetAllSessions().FirstOrDefault(s => s.Id == id);
            if (session == null)
            {
                return NotFound(new { message = "Practice session not found" });
            }

            var players = await _userRepository.GetUsersByRoleAsync(UserRole.Player);
            var attendanceRows = _repository.GetAttendanceForSessionDate(id, date);
            var attendanceMap = attendanceRows.ToDictionary(a => a.PlayerId, a => a);

            var grid = players.Select(player =>
            {
                attendanceMap.TryGetValue(player.Id, out var row);
                return new
                {
                    playerId = player.Id,
                    playerName = player.Username,
                    playerEmail = player.Email,
                    playerIdentityNumber = player.IdentityNumber,
                    isPresent = row?.IsPresent ?? false,
                    hasExistingRecord = row != null,
                    markedAt = row?.MarkedAt,
                    markedByAdminId = row?.MarkedByAdminId
                };
            }).ToList();

            return Ok(new
            {
                success = true,
                session = new { session.Id, session.DayOfWeek, session.StartTime, session.EndTime, session.SessionType },
                attendanceDate = date,
                players = grid
            });
        }

        [HttpPut("{id}/attendance")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> SaveAttendanceGrid(int id, [FromBody] SavePracticeAttendanceRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { message = "Attendance items are required" });
            }

            if (!request.ConfirmSave)
            {
                return BadRequest(new { message = "Confirm save is required to update attendance" });
            }

            var session = _repository.GetAllSessions().FirstOrDefault(s => s.Id == id);
            if (session == null)
            {
                return NotFound(new { message = "Practice session not found" });
            }

            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("userId");
            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            var date = request.AttendanceDate.Date;

            var playerIds = (await _userRepository.GetUsersByRoleAsync(UserRole.Player)).Select(p => p.Id).ToHashSet();
            if (request.Items.Any(i => !playerIds.Contains(i.PlayerId)))
            {
                return BadRequest(new { message = "One or more attendance records reference invalid players" });
            }

            var items = request.Items
                .GroupBy(i => i.PlayerId)
                .Select(g => new PracticeAttendanceSaveItem { PlayerId = g.Key, IsPresent = g.Last().IsPresent })
                .ToList();

            _repository.UpsertAttendanceRecords(id, date, adminId, items);
            return Ok(new
            {
                success = true,
                message = "Attendance saved successfully",
                attendanceDate = date,
                updatedCount = items.Count
            });
        }

        [HttpGet("attendance-report")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetAttendanceReport([FromQuery] int minMissed = 1)
        {
            var rows = _repository.GetAttendanceMissReport()
                .Where(r => r.MissedSessions >= minMissed)
                .ToList();

            return Ok(new { success = true, data = rows, count = rows.Count });
        }

        [HttpGet("my-attendance")]
        [Authorize(Policy = "ApprovedPlayersOnly")]
        public IActionResult GetMyAttendance()
        {
            var userIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("userId");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int playerId))
            {
                return Unauthorized(new { message = "Unable to identify player" });
            }

            var rows = _repository.GetAttendanceForPlayer(playerId);
            return Ok(new
            {
                success = true,
                data = rows.Select(r => new
                {
                    r.AttendanceDate,
                    r.IsPresent,
                    r.MarkedAt,
                    session = new
                    {
                        r.SessionId,
                        r.DayOfWeek,
                        r.StartTime,
                        r.EndTime,
                        r.SessionType
                    },
                    markedByAdmin = string.IsNullOrWhiteSpace(r.MarkedByAdminName) ? "Admin" : r.MarkedByAdminName
                }),
                count = rows.Count
            });
        }
    }

    public class SavePracticeAttendanceRequest
    {
        public DateTime AttendanceDate { get; set; }
        public bool ConfirmSave { get; set; }
        public List<SavePracticeAttendanceItem> Items { get; set; } = new();
    }

    public class SavePracticeAttendanceItem
    {
        public int PlayerId { get; set; }
        public bool IsPresent { get; set; }
    }
}