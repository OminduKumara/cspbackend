using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using tmsserver.Models;
using tmsserver.Data.Repositories;

[Route("api/[controller]")]
[ApiController]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IUserRepository _userRepository;

    public TournamentsController(
        ITournamentRepository tournamentRepository,
        IUserRepository userRepository)
    {
        _tournamentRepository = tournamentRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Get all tournaments
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTournaments()
    {
        try
        {
            var tournaments = await _tournamentRepository.GetAllTournamentsAsync();
            var result = tournaments.Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                Status = t.Status.ToString(),
                t.StartDate,
                t.EndDate,
                t.CreatedAt,
                t.UpdatedAt
            }).ToList();
            
            return Ok(new
            {
                success = true,
                data = result,
                count = result.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching tournaments", error = ex.Message });
        }
    }

    /// <summary>
    /// Get tournament by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTournament(int id)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(id);
            if (tournament == null)
            {
                return NotFound(new { message = "Tournament not found" });
            }

            var result = new
            {
                tournament.Id,
                tournament.Name,
                tournament.Description,
                Status = tournament.Status.ToString(),
                tournament.StartDate,
                tournament.EndDate,
                tournament.CreatedAt,
                tournament.UpdatedAt
            };

            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching tournament", error = ex.Message });
        }
    }

    /// <summary>
    /// Get tournaments by status
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetTournamentsByStatus(int status)
    {
        try
        {
            if (!Enum.IsDefined(typeof(TournamentStatus), status))
            {
                return BadRequest(new { message = "Invalid tournament status" });
            }

            var tournaments = await _tournamentRepository.GetTournamentsByStatusAsync((TournamentStatus)status);
            var result = tournaments.Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                Status = t.Status.ToString(),
                t.StartDate,
                t.EndDate,
                t.CreatedAt
            }).ToList();
            
            return Ok(new
            {
                success = true,
                data = result,
                count = result.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching tournaments by status", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new tournament (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateTournament([FromBody] TournamentRequest request)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Tournament name is required" });
            }

            if (request.StartDate >= request.EndDate)
            {
                return BadRequest(new { message = "End date must be after start date" });
            }

            // Get admin ID from JWT token
            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");

            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            // Verify admin is admin or system admin
            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid();
            }

            var tournament = new Tournament
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                Status = TournamentStatus.Scheduled,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CreatedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow
            };

            await _tournamentRepository.CreateTournamentAsync(tournament);

            var result = new
            {
                tournament.Id,
                tournament.Name,
                tournament.Description,
                Status = tournament.Status.ToString(),
                tournament.StartDate,
                tournament.EndDate,
                tournament.CreatedAt
            };

            return CreatedAtAction(nameof(GetTournament), new { id = tournament.Id }, new
            {
                success = true,
                message = "Tournament created successfully",
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating tournament", error = ex.Message });
        }
    }

    /// <summary>
    /// Update tournament status (Admin only)
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateTournamentStatus(int id, [FromBody] TournamentStatusUpdate request)
    {
        try
        {
            Console.WriteLine($"[UpdateTournamentStatus] Received request for tournament ID: {id}");
            Console.WriteLine($"[UpdateTournamentStatus] Request object: {(request == null ? "NULL" : $"Status={request.Status}")}");
            
            if (request == null)
            {
                Console.WriteLine("[UpdateTournamentStatus] Request body is null!");
                return BadRequest(new { message = "Request body cannot be empty" });
            }

            // Get admin ID from JWT token
            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");

            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            // Verify admin is admin or system admin
            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid();
            }

            // Check if tournament exists
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(id);
            if (tournament == null)
            {
                return NotFound(new { message = "Tournament not found" });
            }

            // Parse string status to enum
            if (!Enum.TryParse<TournamentStatus>(request.Status, true, out var parsedStatus))
            {
                return BadRequest(new { message = "Invalid tournament status. Valid values: Scheduled, InProgress, Completed, Cancelled" });
            }

            Console.WriteLine($"[UpdateTournamentStatus] About to update tournament {id} with status: {request.Status}");
            var updated = await _tournamentRepository.UpdateTournamentStatusAsync(id, parsedStatus, adminId);

            if (!updated)
            {
                return BadRequest(new { message = "Failed to update tournament status" });
            }

            var updatedTournament = await _tournamentRepository.GetTournamentByIdAsync(id);
            var result = new
            {
                updatedTournament.Id,
                updatedTournament.Name,
                updatedTournament.Description,
                Status = updatedTournament.Status.ToString(),
                updatedTournament.StartDate,
                updatedTournament.EndDate,
                updatedTournament.UpdatedAt
            };
            
            return Ok(new
            {
                success = true,
                message = "Tournament status updated successfully",
                data = result
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateTournamentStatus] Exception: {ex.Message}");
            Console.WriteLine($"[UpdateTournamentStatus] Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { message = "Error updating tournament status", error = ex.Message });
        }
    }

    /// <summary>
    /// Update tournament details (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateTournament(int id, [FromBody] TournamentRequest request)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Tournament name is required" });
            }

            if (request.StartDate >= request.EndDate)
            {
                return BadRequest(new { message = "End date must be after start date" });
            }

            // Get admin ID from JWT token
            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");

            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            // Verify admin is admin or system admin
            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid();
            }

            // Check if tournament exists
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(id);
            if (tournament == null)
            {
                return NotFound(new { message = "Tournament not found" });
            }

            tournament.Name = request.Name.Trim();
            tournament.Description = request.Description?.Trim() ?? string.Empty;
            tournament.StartDate = request.StartDate;
            tournament.EndDate = request.EndDate;
            tournament.UpdatedByAdminId = adminId;

            var updated = await _tournamentRepository.UpdateTournamentAsync(tournament);

            if (!updated)
            {
                return BadRequest(new { message = "Failed to update tournament" });
            }

            var updatedTournament = await _tournamentRepository.GetTournamentByIdAsync(id);
            var result = new
            {
                updatedTournament.Id,
                updatedTournament.Name,
                updatedTournament.Description,
                Status = updatedTournament.Status.ToString(),
                updatedTournament.StartDate,
                updatedTournament.EndDate,
                updatedTournament.UpdatedAt
            };

            return Ok(new
            {
                success = true,
                message = "Tournament updated successfully",
                data = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating tournament", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a tournament (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteTournament(int id)
    {
        try
        {
            // Get admin ID from JWT token
            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");

            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            // Verify admin is admin or system admin
            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid();
            }

            // Check if tournament exists
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(id);
            if (tournament == null)
            {
                return NotFound(new { message = "Tournament not found" });
            }

            var deleted = await _tournamentRepository.DeleteTournamentAsync(id);

            if (!deleted)
            {
                return BadRequest(new { message = "Failed to delete tournament" });
            }

            return Ok(new
            {
                success = true,
                message = "Tournament deleted successfully",
                deletedId = id
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting tournament", error = ex.Message });
        }
    }
}
