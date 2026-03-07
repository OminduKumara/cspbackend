using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using tmsserver.Data.Repositories;
using tmsserver.Models;

public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationRequestRepository _registrationRequestRepository;

    public UserService(
        IUserRepository userRepository,
        IRegistrationRequestRepository registrationRequestRepository)
    {
        _userRepository = userRepository;
        _registrationRequestRepository = registrationRequestRepository;
    }

    public bool ValidatePassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == hash;
    }

    public bool ValidateEmailDomain(string email)
    {
        var normalizedEmail = email.Trim();
        return normalizedEmail.EndsWith("@sliit.lk", StringComparison.OrdinalIgnoreCase)
            || normalizedEmail.EndsWith("@my.sliit.lk", StringComparison.OrdinalIgnoreCase);
    }

    public bool ValidateIdentityNumber(string identityNumber)
    {
        var normalizedIdentity = identityNumber.Trim();
        bool hasLetters = normalizedIdentity.Any(char.IsLetter);
        bool hasNumbers = normalizedIdentity.Any(char.IsDigit);
        return hasLetters && hasNumbers;
    }

    public async Task<User> RegisterUserAsync(string username, string identityNumber, string email, string password)
    {
        username = username.Trim();
        identityNumber = identityNumber.Trim();
        email = email.Trim();

        
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(identityNumber) || 
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new Exception("All fields are required");
        }

        if (!ValidateEmailDomain(email))
        {
            throw new Exception("Email must be from @sliit.lk or @my.sliit.lk domain");
        }

        if (!ValidateIdentityNumber(identityNumber))
        {
            throw new Exception("Identity number must contain both letters and numbers");
        }

        
        if (await _userRepository.GetUserByUsernameAsync(username) != null)
        {
            throw new Exception("Username already exists");
        }

        if (await _userRepository.GetUserByEmailAsync(email) != null)
        {
            throw new Exception("Email already registered");
        }

        if (await _userRepository.GetUserByIdentityNumberAsync(identityNumber) != null)
        {
            throw new Exception("Identity number already registered");
        }

        var user = new User
        {
            Username = username,
            IdentityNumber = identityNumber,
            Email = email,
            PasswordHash = HashPassword(password),
            Role = UserRole.PendingPlayer,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateUserAsync(user);

        return user;
    }

    public async Task<bool> ApproveRegistrationAsync(int userId, int adminId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user != null && user.Role != UserRole.PendingPlayer)
        {
            user = null;
        }
        if (user == null)
        {
            throw new Exception("User not found or already processed");
        }

        var approved = await _userRepository.ApproveUserAsync(userId, adminId);
        if (!approved)
        {
            throw new Exception("Failed to approve user");
        }

        var registrationRequest = await _registrationRequestRepository.GetRequestByUserIdAsync(userId);
        if (registrationRequest != null)
        {
            await _registrationRequestRepository.ApproveRequestAsync(registrationRequest.Id, adminId);
        }

        return true;
    }

    public async Task<bool> RejectRegistrationAsync(int userId, int adminId, string? reason = null)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user != null && user.Role != UserRole.PendingPlayer)
        {
            user = null;
        }
        if (user == null)
        {
            throw new Exception("User not found or already processed");
        }

        var registrationRequest = await _registrationRequestRepository.GetRequestByUserIdAsync(userId);
        if (registrationRequest != null)
        {
            await _registrationRequestRepository.RejectRequestAsync(registrationRequest.Id, adminId, reason ?? "Rejected by admin");
        }

        await _userRepository.DeleteUserAsync(userId);
        return true;
    }

    public async Task<List<User>> GetPendingRegistrationsAsync()
    {
        return await _userRepository.GetPendingApprovalsAsync();
    }

    public static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
