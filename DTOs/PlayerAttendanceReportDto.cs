using System;

namespace tmsserver.DTOs
{
    public class PlayerAttendanceReportDto
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string IdentityNumber { get; set; } = string.Empty;
        public int TotalSessionsScheduled { get; set; }
        public int SessionsAttended { get; set; }
        public double AttendancePercentage { get; set; }
    }
}