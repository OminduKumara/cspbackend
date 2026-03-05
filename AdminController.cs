using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using tmsserver.Models;
using tmsserver.Services;
using tmsserver.Data.Repositories;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly tmsserver.Services.IAuthorizationService _authorizationService;
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationRequestRepository _registrationRequestRepository;

    public AdminController(
        tmsserver.Services.IAuthorizationService authorizationService,
        IUserRepository userRepository,
        IRegistrationRequestRepository registrationRequestRepository)
    {
        _authorizationService = authorizationService;
        _userRepository = userRepository;
        _registrationRequestRepository = registrationRequestRepository;
    }

    /// <summary>
    /// Get all pending player approval requests
    /// </summary>
    [HttpGet("pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        try
        {
            var pendingUsers = await _authorizationService.GetPendingApprovalAsync();
            var result = pendingUsers.Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.IdentityNumber,
                u.CreatedAt,
                Status = "Pending"
            }).ToList();

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching pending approvals", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all registration requests with status
    /// </summary>
    [HttpGet("registration-requests")]
    public async Task<IActionResult> GetRegistrationRequests([FromQuery] string? status = null)
    {
        try
        {
            List<RegistrationRequest> requests;
            
            if (!string.IsNullOrEmpty(status))
            {
                requests = await _registrationRequestRepository.GetRequestsByStatusAsync(status);
            }
            else
            {
                requests = await _registrationRequestRepository.GetAllPendingRequestsAsync();
            }

            var result = new List<dynamic>();
            foreach (var request in requests)
            {
                var user = await _userRepository.GetUserByIdAsync(request.UserId);
                if (user != null)
                {
                    result.Add(new
                    {
                        request.Id,
                        request.UserId,
                        User = new { user.Id, user.Username, user.Email, user.IdentityNumber },
                        request.Status,
                        request.CreatedAt,
                        request.ReviewedAt,
                        request.RejectionReason
                    });
                }
            }

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching registration requests", error = ex.Message });
        }
    }

    /// <summary>
    /// Approve a pending player registration
    /// </summary>
    [HttpPost("approve-player/{userId}")]
    public async Task<IActionResult> ApprovePlayer(int userId)
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
                return Forbid("You don't have permission to approve players");
            }

            // Approve the user
            var approved = await _authorizationService.ApproveUserAsync(userId, adminId);
            
            if (approved)
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                return Ok(new
                {
                    success = true,
                    message = "Player approved successfully",
                    user = new
                    {
                        user?.Id,
                        user?.Username,
                        user?.Email,
                        user?.Role,
                        user?.IsApproved
                    }
                });
            }

            return BadRequest(new { success = false, message = "Failed to approve player" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error approving player", error = ex.Message });
        }
    }

    /// <summary>
    /// Reject a pending player registration
    /// </summary>
    [HttpPost("reject-player/{userId}")]
    public async Task<IActionResult> RejectPlayer(int userId, [FromBody] RejectRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { message = "Rejection reason is required" });
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
                return Forbid("You don't have permission to reject players");
            }

            // Reject the user
            var rejected = await _authorizationService.RejectUserAsync(userId, adminId, request.Reason);

            if (rejected)
            {
                return Ok(new
                {
                    success = true,
                    message = "Player registration rejected",
                    reason = request.Reason
                });
            }

            return BadRequest(new { success = false, message = "Failed to reject player" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error rejecting player", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string? role = null)
    {
        try
        {
            List<User> users;

            if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, out var userRole))
            {
                users = await _userRepository.GetUsersByRoleAsync(userRole);
            }
            else
            {
                users = await _userRepository.GetAllUsersAsync();
            }

            var result = users.Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.IdentityNumber,
                u.Role,
                u.IsApproved,
                u.CreatedAt,
                u.ApprovedAt
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching users", error = ex.Message });
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        try
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.IdentityNumber,
                    user.Role,
                    user.IsApproved,
                    user.ApprovedByAdminId,
                    user.CreatedAt,
                    user.ApprovedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching user", error = ex.Message });
        }
    }
}

public class RejectRequest
{
    public string Reason { get; set; } = string.Empty;
}
