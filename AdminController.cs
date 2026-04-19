using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.RegularExpressions;
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
    private readonly IPracticeSessionRepository _practiceSessionRepository;

    public AdminController(
        tmsserver.Services.IAuthorizationService authorizationService,
        IUserRepository userRepository,
        IRegistrationRequestRepository registrationRequestRepository,
        IPracticeSessionRepository practiceSessionRepository)
    {
        _authorizationService = authorizationService;
        _userRepository = userRepository;
        _registrationRequestRepository = registrationRequestRepository;
        _practiceSessionRepository = practiceSessionRepository;
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
            var adminIdClaim = User.FindFirst("sub") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");
            
            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid("You don't have permission to approve players");
            }

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

            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");
            
            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Unable to identify admin" });
            }

            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid("You don't have permission to reject players");
            }

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
                u.ContactNumber,
                u.Address,
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
                    user.ContactNumber,
                    user.Address,
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

    /// <summary>
    /// Update user details by ID
    /// </summary>
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserByAdminRequest request)
    {
        try
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var username = request.Username?.Trim() ?? string.Empty;
            var email = request.Email?.Trim() ?? string.Empty;
            var identityNumber = request.IdentityNumber?.Trim() ?? string.Empty;
            var contactNumber = request.ContactNumber?.Trim() ?? string.Empty;
            var address = request.Address?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(identityNumber))
            {
                return BadRequest(new { message = "Username, email and identity number are required" });
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return BadRequest(new { message = "Invalid email format" });
            }

            if (!string.IsNullOrWhiteSpace(contactNumber) && !Regex.IsMatch(contactNumber, @"^\+?[0-9\s\-]{7,15}$"))
            {
                return BadRequest(new { message = "Invalid phone number format" });
            }

            var existingWithUsername = await _userRepository.GetUserByUsernameAsync(username);
            if (existingWithUsername != null && existingWithUsername.Id != id)
            {
                return BadRequest(new { message = "Username is already taken" });
            }

            var existingWithEmail = await _userRepository.GetUserByEmailAsync(email);
            if (existingWithEmail != null && existingWithEmail.Id != id)
            {
                return BadRequest(new { message = "Email is already in use" });
            }

            var existingWithIdentity = await _userRepository.GetUserByIdentityNumberAsync(identityNumber);
            if (existingWithIdentity != null && existingWithIdentity.Id != id)
            {
                return BadRequest(new { message = "Identity number is already in use" });
            }

            if (!Enum.IsDefined(typeof(UserRole), request.Role))
            {
                return BadRequest(new { message = "Invalid role value" });
            }

            user.Username = username;
            user.Email = email;
            user.IdentityNumber = identityNumber;
            user.ContactNumber = contactNumber;
            user.Address = address;
            user.Role = (UserRole)request.Role;
            user.IsApproved = request.IsApproved;

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                if (request.NewPassword.Length < 6)
                {
                    return BadRequest(new { message = "Password must be at least 6 characters" });
                }
                user.PasswordHash = UserService.HashPassword(request.NewPassword);
            }

            var adminIdClaim = User.FindFirst("sub")
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?? User.FindFirst("userId");

            if (request.IsApproved && user.ApprovedByAdminId == null && adminIdClaim != null && int.TryParse(adminIdClaim.Value, out int adminId))
            {
                user.ApprovedByAdminId = adminId;
                user.ApprovedAt = DateTime.UtcNow;
            }

            if (!request.IsApproved)
            {
                user.ApprovedAt = null;
            }

            var updated = await _userRepository.UpdateUserAsync(user);
            if (!updated)
            {
                return BadRequest(new { message = "Failed to update user" });
            }

            var refreshedUser = await _userRepository.GetUserByIdAsync(id);
            return Ok(new
            {
                success = true,
                message = "User updated successfully",
                data = new
                {
                    refreshedUser?.Id,
                    refreshedUser?.Username,
                    refreshedUser?.Email,
                    refreshedUser?.IdentityNumber,
                    refreshedUser?.ContactNumber,
                    refreshedUser?.Address,
                    refreshedUser?.Role,
                    refreshedUser?.IsApproved,
                    refreshedUser?.ApprovedByAdminId,
                    refreshedUser?.CreatedAt,
                    refreshedUser?.ApprovedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating user", error = ex.Message });
        }
    }

    /// <summary>
    /// Generate PDF-ready attendance report data
    /// </summary>
    [HttpGet("reports/attendance")]
    public IActionResult GenerateAttendanceReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate > endDate)
            {
                return BadRequest(new { message = "Start date cannot be after the end date." });
            }

            // Call the repository method
            var reportData = _practiceSessionRepository.GetAttendanceReport(startDate, endDate);

            return Ok(new 
            { 
                success = true,
                message = "Report generated successfully",
                data = reportData 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error generating report", error = ex.Message });
        }
    }
}

public class RejectRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class UpdateUserByAdminRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? IdentityNumber { get; set; }
    public string? ContactNumber { get; set; }
    public string? Address { get; set; }
    public int Role { get; set; }
    public bool IsApproved { get; set; }
    public string? NewPassword { get; set; }
}