using System;
using System.Collections.Generic;

namespace tmsserver.DTOs
{
    public class PlayerDashboardStatsDto
    {
        public int PlayerId { get; set; }
        public double OverallAttendancePercentage { get; set; }
        public int TotalPracticesAttended { get; set; }
        
        // This list will power the Recharts Bar/Line chart on the frontend
        public List<WeeklyAttendanceStat> WeeklyStats { get; set; } = new List<WeeklyAttendanceStat>();
    }

    public class WeeklyAttendanceStat
    {
        public string WeekLabel { get; set; } = string.Empty;
        public int SessionsAttended { get; set; }
    }
}