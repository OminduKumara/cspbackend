namespace tmsserver.Models
{
    public class MatchScore
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public int SetNumber { get; set; }
        public int Team1Games { get; set; }
        public int Team2Games { get; set; }
        public int? Team1TieBreak { get; set; }
        public int? Team2TieBreak { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
