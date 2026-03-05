namespace tmsserver.Models;

public class RegistrationRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = "Pending";  // Pending, Approved, Rejected
    public int? ReviewedByAdminId { get; set; }
    public User? ReviewedByAdmin { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
