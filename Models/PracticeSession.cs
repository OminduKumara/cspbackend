namespace tmsserver.Models
{
    public class PracticeSession
    {
        public int Id { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
    }
}