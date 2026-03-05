using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using tmsserver.Data;
using tmsserver.Models;

public class UserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public User? FindUserByUsername(string username)
    {
        return _context.Users.FirstOrDefault(u => u.Username == username);
    }

    public User? FindUserByEmail(string email)
    {
        return _context.Users.FirstOrDefault(u => u.Email == email);
    }

    public User? FindUserByIdentityNumber(string identityNumber)
    {
        return _context.Users.FirstOrDefault(u => u.IdentityNumber == identityNumber);
    }

    public User? FindUserById(int id)
    {
        return _context.Users.FirstOrDefault(u => u.Id == id);
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

        
        if (FindUserByUsername(username) != null)
        {
            throw new Exception("Username already exists");
        }

        if (FindUserByEmail(email) != null)
        {
            throw new Exception("Email already registered");
        }

        if (FindUserByIdentityNumber(identityNumber) != null)
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

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<bool> ApproveRegistrationAsync(int userId, int adminId)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == userId && u.Role == UserRole.PendingPlayer);
        if (user == null)
        {
            throw new Exception("User not found or already processed");
        }

        user.Role = UserRole.Player;
        user.IsApproved = true;
        user.ApprovedByAdminId = adminId;
        user.ApprovedAt = DateTime.UtcNow;

        var registrationRequest = _context.RegistrationRequests.FirstOrDefault(r => r.UserId == userId);
        if (registrationRequest != null)
        {
            registrationRequest.Status = "Approved";
            registrationRequest.ReviewedByAdminId = adminId;
            registrationRequest.ReviewedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectRegistrationAsync(int userId, int adminId, string? reason = null)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == userId && u.Role == UserRole.PendingPlayer);
        if (user == null)
        {
            throw new Exception("User not found or already processed");
        }

        var registrationRequest = _context.RegistrationRequests.FirstOrDefault(r => r.UserId == userId);
        if (registrationRequest != null)
        {
            registrationRequest.Status = "Rejected";
            registrationRequest.ReviewedByAdminId = adminId;
            registrationRequest.ReviewedAt = DateTime.UtcNow;
            registrationRequest.RejectionReason = reason;
        }

        // Delete the pending user
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<User>> GetPendingRegistrationsAsync()
    {
        return await _context.Users
            .Where(u => u.Role == UserRole.PendingPlayer && !u.IsApproved)
            .ToListAsync();
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
