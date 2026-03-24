using tmsserver.Data.Repositories;
using tmsserver.Models;

namespace tmsserver.Services;

public interface IAuthorizationService
{
    Task<bool> IsUserApprovedAsync(int userId);
    Task<bool> IsAdminAsync(User user);
    Task<bool> IsSystemAdminAsync(User user);
    Task<bool> HasPermissionAsync(User user, string permission);
    Task<List<User>> GetPendingApprovalAsync();
    Task<bool> ApproveUserAsync(int userId, int approvedByAdminId);
    Task<bool> RejectUserAsync(int userId, int rejectedByAdminId, string reason);
    string? GetUserRoleClaimValue(User user);
}

public class AuthorizationService : IAuthorizationService
{
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationRequestRepository _registrationRequestRepository;

    public AuthorizationService(IUserRepository userRepository, IRegistrationRequestRepository registrationRequestRepository)
    {
        _userRepository = userRepository;
        _registrationRequestRepository = registrationRequestRepository;
    }

    public async Task<bool> IsUserApprovedAsync(int userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
            return false;

        return user.IsApproved && user.Role != UserRole.PendingPlayer;
    }

    public async Task<bool> IsAdminAsync(User user)
    {
        return user.Role == UserRole.Admin || user.Role == UserRole.SystemAdmin;
    }

    public async Task<bool> IsSystemAdminAsync(User user)
    {
        return user.Role == UserRole.SystemAdmin;
    }

    public async Task<bool> HasPermissionAsync(User user, string permission)
    {
        if (user.Role == UserRole.SystemAdmin)
            return true; // System admins have all permissions

        return user.Role switch
        {
            UserRole.Admin => HasAdminPermission(permission),
            UserRole.Player => HasPlayerPermission(permission),
            _ => false
        };
    }

    public async Task<List<User>> GetPendingApprovalAsync()
    {
        return await _userRepository.GetPendingApprovalsAsync();
    }

    public async Task<bool> ApproveUserAsync(int userId, int approvedByAdminId)
    {
        var approved = await _userRepository.ApproveUserAsync(userId, approvedByAdminId);
        
        if (approved)
        {
            var registrationRequest = await _registrationRequestRepository.GetRequestByUserIdAsync(userId);
            if (registrationRequest != null)
            {
                await _registrationRequestRepository.ApproveRequestAsync(registrationRequest.Id, approvedByAdminId);
            }
        }

        return approved;
    }

    public async Task<bool> RejectUserAsync(int userId, int rejectedByAdminId, string reason)
    {
        var registrationRequest = await _registrationRequestRepository.GetRequestByUserIdAsync(userId);
        if (registrationRequest != null)
        {
            return await _registrationRequestRepository.RejectRequestAsync(registrationRequest.Id, rejectedByAdminId, reason);
        }
        return false;
    }

    public string? GetUserRoleClaimValue(User user)
    {
        return user.Role.ToString();
    }

    private bool HasAdminPermission(string permission)
    {
        var adminPermissions = new[] 
        { 
            "manage_users", 
            "manage_players", 
            "approve_registrations", 
            "view_reports"
        };
        return adminPermissions.Contains(permission);
    }

    private bool HasPlayerPermission(string permission)
    {
        var playerPermissions = new[]
        {
            "view_tournaments",
            "register_tournament",
            "view_results"
        };
        return playerPermissions.Contains(permission);
    }
}
