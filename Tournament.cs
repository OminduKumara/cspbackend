namespace tmsserver.Models;

public enum TournamentStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public class Tournament
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TournamentStatus Status { get; set; } = TournamentStatus.Scheduled;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int CreatedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByAdminId { get; set; }
}

public class TournamentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TournamentStatusUpdate
{
    public string Status { get; set; }
}
