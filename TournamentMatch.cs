namespace tmsserver.Models;

public class TournamentMatch
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public int Team1Id { get; set; }
    public int Team2Id { get; set; }
    public int? WinnerId { get; set; }
    public bool IsPlayoff { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
