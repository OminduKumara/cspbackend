using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.Text;
using tmsserver.Data.Repositories;
using tmsserver.Models;
using tmsserver.Services;
using AuthSvc = tmsserver.Services.IAuthorizationService;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly UserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationRequestRepository _registrationRequestRepository;
    private readonly AuthSvc _authorizationService;

    public AuthController(
        IConfiguration config,
        UserService userService,
        IUserRepository userRepository,
        IRegistrationRequestRepository registrationRequestRepository,
        AuthSvc authorizationService)
    {
        _config = config;
        _userService = userService;
        _userRepository = userRepository;
        _registrationRequestRepository = registrationRequestRepository;
        _authorizationService = authorizationService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        try
        {
            User? user = await _userRepository.GetUserByUsernameAsync(model.Username);
            user ??= await _userRepository.GetUserByEmailAsync(model.Username);
            user ??= await _userRepository.GetUserByIdentityNumberAsync(model.Username);

            if (user == null || !_userService.ValidatePassword(model.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            // Check if user is approved (only for non-admin users)
            if (user.Role == UserRole.PendingPlayer || !user.IsApproved)
            {
                return Unauthorized(new { message = "Your account is not yet approved. Please wait for admin approval." });
            }

            var token = GenerateToken(user.Id.ToString(), user.Username, user.Role.ToString());
            return Ok(new
            {
                token,
                id = user.Id,
                username = user.Username,
                email = user.Email,
                identityNumber = user.IdentityNumber,
                contactNumber = user.ContactNumber,
                address = user.Address,
                role = user.Role.ToString(),
                isApproved = user.IsApproved,
                approvedAt = user.ApprovedAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error during login", error = ex.Message });
        }
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || 
            string.IsNullOrWhiteSpace(model.IdentityNumber) ||
            string.IsNullOrWhiteSpace(model.Email) || 
            string.IsNullOrWhiteSpace(model.Password))
        {
            return BadRequest(new { message = "All fields are required" });
        }

        if (model.Password != model.ConfirmPassword)
        {
            return BadRequest(new { message = "Passwords do not match" });
        }

        if (model.Password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters" });
        }

        try
        {
            var user = await _userService.RegisterUserAsync(model.Username, model.IdentityNumber, model.Email, model.Password);
            
            
            var registrationRequest = new RegistrationRequest
            {
                UserId = user.Id,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            await _registrationRequestRepository.CreateRequestAsync(registrationRequest);

            return Ok(new { 
                message = "Registration submitted successfully. Please wait for admin approval.",
                userId = user.Id,
                username = user.Username,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("pending-registrations")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetPendingRegistrations()
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
                Status = "Pending Approval"
            }).ToList();

            return Ok(new { success = true, data = result, count = result.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching pending registrations", error = ex.Message });
        }
    }

    [HttpPost("approve-registration/{userId}")]
    [Authorize(Policy = "ApproveRegistrations")]
    public async Task<IActionResult> ApproveRegistration(int userId)
    {
        try
        {
            var adminIdClaim = User.FindFirst("sub") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("userId");
            
            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Invalid admin info" });
            }

            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid("You don't have permission to approve registrations");
            }

            var approved = await _authorizationService.ApproveUserAsync(userId, adminId);
            if (approved)
            {
                var user = await _userRepository.GetUserByIdAsync(userId);
                return Ok(new
                {
                    success = true,
                    message = "Player registration approved successfully",
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

            return BadRequest(new { success = false, message = "Failed to approve registration" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error approving registration", error = ex.Message });
        }
    }

    [HttpPost("reject-registration/{userId}")]
    [Authorize(Policy = "ApproveRegistrations")]
    public async Task<IActionResult> RejectRegistration(int userId, [FromBody] RejectionModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model?.Reason))
            {
                return BadRequest(new { message = "Rejection reason is required" });
            }

            var adminIdClaim = User.FindFirst("sub") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("userId");
            
            if (adminIdClaim == null || !int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Unauthorized(new { message = "Invalid admin info" });
            }

            var admin = await _userRepository.GetUserByIdAsync(adminId);
            if (admin?.Role != UserRole.Admin && admin?.Role != UserRole.SystemAdmin)
            {
                return Forbid("You don't have permission to reject registrations");
            }

            var rejected = await _authorizationService.RejectUserAsync(userId, adminId, model.Reason);
            if (rejected)
            {
                return Ok(new
                {
                    success = true,
                    message = "Player registration rejected",
                    reason = model.Reason
                });
            }

            return BadRequest(new { success = false, message = "Failed to reject registration" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error rejecting registration", error = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim?.Value, out int userId))
            {
                return Unauthorized(new { message = "Unable to identify user" });
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                success = true,
                user = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.IdentityNumber,
                    user.ContactNumber,
                    user.Address,
                    user.Role,
                    user.IsApproved,
                    user.CreatedAt,
                    user.ApprovedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching user info", error = ex.Message });
        }
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileModel model)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim?.Value, out int userId))
            {
                return Unauthorized(new { message = "Unable to identify user" });
            }

            var username = model.Username?.Trim() ?? string.Empty;
            var email = model.Email?.Trim() ?? string.Empty;
            var contactNumber = model.ContactNumber?.Trim() ?? string.Empty;
            var address = model.Address?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { message = "Name and email are required" });
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
            if (existingWithUsername != null && existingWithUsername.Id != userId)
            {
                return BadRequest(new { message = "Username is already taken" });
            }

            var existingWithEmail = await _userRepository.GetUserByEmailAsync(email);
            if (existingWithEmail != null && existingWithEmail.Id != userId)
            {
                return BadRequest(new { message = "Email is already in use" });
            }

            var updated = await _userRepository.UpdateUserProfileAsync(userId, username, email, contactNumber, address);
            if (!updated)
            {
                return NotFound(new { message = "User not found" });
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                success = true,
                message = "Profile updated successfully",
                user = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.IdentityNumber,
                    user.ContactNumber,
                    user.Address,
                    user.Role,
                    user.IsApproved,
                    user.CreatedAt,
                    user.ApprovedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating profile", error = ex.Message });
        }
    }

    private string GenerateToken(string userId, string username, string role)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
            new Claim("userId", userId),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RejectionModel
{
    public string? Reason { get; set; }
}

public class UpdateProfileModel
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? ContactNumber { get; set; }
    public string? Address { get; set; }
}
