namespace tmsserver.Models;

public class TournamentTeam
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int TeamOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
