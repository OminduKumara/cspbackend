namespace tmsserver.Models
{
    public class LiveGameScore
    {
        public int MatchId { get; set; }
        public string Team1Points { get; set; } = "0";
        public string Team2Points { get; set; } = "0";
        public int? ServingTeamId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
