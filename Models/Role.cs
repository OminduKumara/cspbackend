namespace tmsserver.Models;

public enum UserRole
{
    SystemAdmin = 1,
    Admin = 2,
    Player = 3,
    PendingPlayer = 4  // Players awaiting admin approval
}
