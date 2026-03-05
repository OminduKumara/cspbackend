namespace tmsserver.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;  // e.g., it23575776
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.PendingPlayer;
    public bool IsApproved { get; set; } = false;  // For pending players
    public int? ApprovedByAdminId { get; set; }  // Which admin approved this player
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
}
