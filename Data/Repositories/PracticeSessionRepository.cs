using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient; 
using Microsoft.Extensions.Configuration;
using tmsserver.Models;
using tmsserver.DTOs;

namespace tmsserver.Data.Repositories
{
    public interface IPracticeSessionRepository
    {
        List<PracticeSession> GetAllSessions();
        void AddSession(PracticeSession session);
        void UpdateSession(PracticeSession session);
        void DeleteSession(int id);
        List<PlayerAttendanceReportDto> GetAttendanceReport(DateTime startDate, DateTime endDate);
        List<PracticeAttendanceRow> GetAttendanceForSessionDate(int sessionId, DateTime attendanceDate);
        void UpsertAttendanceRecords(int sessionId, DateTime attendanceDate, int adminId, List<PracticeAttendanceSaveItem> items);
        List<PracticeAttendanceReportRow> GetAttendanceMissReport();
        List<PlayerAttendanceViewRow> GetAttendanceForPlayer(int playerId);
    }

    public class PracticeSessionRepository : IPracticeSessionRepository
    {
        private readonly string _connectionString;

        // This constructor pulls your connection string securely from the app environment
        public PracticeSessionRepository(IConfiguration configuration)
        {
            _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") 
                ?? configuration.GetConnectionString("DefaultConnection");
        }

        // 1. GET ALL: Fetches the schedule for your Landing Page
        public List<PracticeSession> GetAllSessions()
        {
            var sessions = new List<PracticeSession>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = "SELECT Id, DayOfWeek, StartTime, EndTime, SessionType FROM PracticeSessions";
                SqlCommand cmd = new SqlCommand(query, conn);
                
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sessions.Add(new PracticeSession
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            DayOfWeek = reader["DayOfWeek"].ToString(),
                            StartTime = reader["StartTime"].ToString(),
                            EndTime = reader["EndTime"].ToString(),
                            SessionType = reader["SessionType"].ToString()
                        });
                    }
                }
            }
            return sessions;
        }

        // 2. CREATE: Allows admins to add a new practice time
        public void AddSession(PracticeSession session)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"INSERT INTO PracticeSessions (DayOfWeek, StartTime, EndTime, SessionType) 
                                 VALUES (@DayOfWeek, @StartTime, @EndTime, @SessionType)";
                
                SqlCommand cmd = new SqlCommand(query, conn);
                
                cmd.Parameters.AddWithValue("@DayOfWeek", session.DayOfWeek);
                cmd.Parameters.AddWithValue("@StartTime", session.StartTime);
                cmd.Parameters.AddWithValue("@EndTime", session.EndTime);
                cmd.Parameters.AddWithValue("@SessionType", session.SessionType);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // 3. UPDATE: Allows admins to modify an existing practice time
        public void UpdateSession(PracticeSession session)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"UPDATE PracticeSessions 
                                 SET DayOfWeek = @DayOfWeek, 
                                     StartTime = @StartTime, 
                                     EndTime = @EndTime, 
                                     SessionType = @SessionType 
                                 WHERE Id = @Id";
                
                SqlCommand cmd = new SqlCommand(query, conn);
                
                cmd.Parameters.AddWithValue("@Id", session.Id);
                cmd.Parameters.AddWithValue("@DayOfWeek", session.DayOfWeek);
                cmd.Parameters.AddWithValue("@StartTime", session.StartTime);
                cmd.Parameters.AddWithValue("@EndTime", session.EndTime);
                cmd.Parameters.AddWithValue("@SessionType", session.SessionType);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // 4. DELETE: Allows admins to remove a canceled practice
        public void DeleteSession(int id)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM PracticeSessions WHERE Id = @Id";
                
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", id);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // 5. GET ATTENDANCE REPORT
        public List<PlayerAttendanceReportDto> GetAttendanceReport(DateTime startDate, DateTime endDate)
        {
            var reportData = new List<PlayerAttendanceReportDto>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        u.Id AS PlayerId,
                        u.Username AS PlayerName,
                        u.IdentityNumber,
                        COUNT(pa.Id) AS TotalSessionsScheduled,
                        SUM(CASE WHEN pa.IsPresent = 1 THEN 1 ELSE 0 END) AS SessionsAttended
                    FROM Users u
                    INNER JOIN PracticeAttendance pa ON u.Id = pa.PlayerId
                    WHERE u.Role = @playerRole 
                      AND pa.AttendanceDate >= @StartDate 
                      AND pa.AttendanceDate <= @EndDate
                    GROUP BY u.Id, u.Username, u.IdentityNumber
                    HAVING COUNT(pa.Id) > 0"; 

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@playerRole", (int)UserRole.Player); 
                cmd.Parameters.AddWithValue("@StartDate", startDate.Date);
                cmd.Parameters.AddWithValue("@EndDate", endDate.Date);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var stat = new PlayerAttendanceReportDto
                        {
                            PlayerId = Convert.ToInt32(reader["PlayerId"]),
                            // ADDED DEFENSIVE DBNull CHECKS HERE
                            PlayerName = reader["PlayerName"] == DBNull.Value ? "Unknown" : reader["PlayerName"].ToString(),
                            IdentityNumber = reader["IdentityNumber"] == DBNull.Value ? "N/A" : reader["IdentityNumber"].ToString(),
                            TotalSessionsScheduled = Convert.ToInt32(reader["TotalSessionsScheduled"]),
                            SessionsAttended = Convert.ToInt32(reader["SessionsAttended"])
                        };

                        stat.AttendancePercentage = Math.Round(
                            ((double)stat.SessionsAttended / stat.TotalSessionsScheduled) * 100, 2);

                        reportData.Add(stat);
                    }
                }
            }

            return reportData.OrderBy(r => r.AttendancePercentage).ToList();
        }

        public List<PracticeAttendanceRow> GetAttendanceForSessionDate(int sessionId, DateTime attendanceDate)
        {
            var rows = new List<PracticeAttendanceRow>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                const string query = @"
                    SELECT pa.Id, pa.PracticeSessionId, pa.PlayerId, pa.AttendanceDate, pa.IsPresent, pa.MarkedByAdminId, pa.MarkedAt
                    FROM PracticeAttendance pa
                    WHERE pa.PracticeSessionId = @sessionId
                      AND CAST(pa.AttendanceDate AS DATE) = CAST(@attendanceDate AS DATE)";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@sessionId", sessionId);
                cmd.Parameters.AddWithValue("@attendanceDate", attendanceDate.Date);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new PracticeAttendanceRow
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            PracticeSessionId = Convert.ToInt32(reader["PracticeSessionId"]),
                            PlayerId = Convert.ToInt32(reader["PlayerId"]),
                            AttendanceDate = Convert.ToDateTime(reader["AttendanceDate"]),
                            IsPresent = Convert.ToBoolean(reader["IsPresent"]),
                            MarkedByAdminId = reader["MarkedByAdminId"] == DBNull.Value ? null : Convert.ToInt32(reader["MarkedByAdminId"]),
                            MarkedAt = reader["MarkedAt"] == DBNull.Value ? null : Convert.ToDateTime(reader["MarkedAt"])
                        });
                    }
                }
            }

            return rows;
        }

        public void UpsertAttendanceRecords(int sessionId, DateTime attendanceDate, int adminId, List<PracticeAttendanceSaveItem> items)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            const string mergeQuery = @"
                                MERGE PracticeAttendance AS target
                                USING (SELECT @sessionId AS PracticeSessionId, @playerId AS PlayerId, @attendanceDate AS AttendanceDate) AS source
                                ON target.PracticeSessionId = source.PracticeSessionId
                                   AND target.PlayerId = source.PlayerId
                                   AND CAST(target.AttendanceDate AS DATE) = CAST(source.AttendanceDate AS DATE)
                                WHEN MATCHED THEN
                                    UPDATE SET IsPresent = @isPresent, MarkedByAdminId = @adminId, MarkedAt = @markedAt
                                WHEN NOT MATCHED THEN
                                    INSERT (PracticeSessionId, PlayerId, AttendanceDate, IsPresent, MarkedByAdminId, MarkedAt)
                                    VALUES (@sessionId, @playerId, @attendanceDate, @isPresent, @adminId, @markedAt);";

                            SqlCommand cmd = new SqlCommand(mergeQuery, conn, transaction);
                            cmd.Parameters.AddWithValue("@sessionId", sessionId);
                            cmd.Parameters.AddWithValue("@playerId", item.PlayerId);
                            cmd.Parameters.AddWithValue("@attendanceDate", attendanceDate.Date);
                            cmd.Parameters.AddWithValue("@isPresent", item.IsPresent);
                            cmd.Parameters.AddWithValue("@adminId", adminId);
                            cmd.Parameters.AddWithValue("@markedAt", DateTime.UtcNow);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<PracticeAttendanceReportRow> GetAttendanceMissReport()
        {
            var rows = new List<PracticeAttendanceReportRow>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                const string query = @"
                    SELECT
                        u.Id AS PlayerId,
                        u.Username,
                        u.Email,
                        COUNT(pa.Id) AS TotalMarkedSessions,
                        SUM(CASE WHEN pa.IsPresent = 0 THEN 1 ELSE 0 END) AS MissedSessions
                    FROM Users u
                    LEFT JOIN PracticeAttendance pa ON pa.PlayerId = u.Id
                    WHERE u.Role = @playerRole
                    GROUP BY u.Id, u.Username, u.Email
                    ORDER BY MissedSessions DESC, TotalMarkedSessions DESC, u.Username ASC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@playerRole", (int)UserRole.Player);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var totalMarked = Convert.ToInt32(reader["TotalMarkedSessions"]);
                        var missed = Convert.ToInt32(reader["MissedSessions"]);
                        rows.Add(new PracticeAttendanceReportRow
                        {
                            PlayerId = Convert.ToInt32(reader["PlayerId"]),
                            Username = reader["Username"].ToString() ?? string.Empty,
                            Email = reader["Email"].ToString() ?? string.Empty,
                            TotalMarkedSessions = totalMarked,
                            MissedSessions = missed,
                            MissPercentage = totalMarked == 0 ? 0 : Math.Round((double)missed / totalMarked * 100, 2)
                        });
                    }
                }
            }

            return rows;
        }

        public List<PlayerAttendanceViewRow> GetAttendanceForPlayer(int playerId)
        {
            var rows = new List<PlayerAttendanceViewRow>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                const string query = @"
                    SELECT
                        pa.AttendanceDate,
                        pa.IsPresent,
                        pa.MarkedAt,
                        ps.Id AS SessionId,
                        ps.DayOfWeek,
                        ps.StartTime,
                        ps.EndTime,
                        ps.SessionType,
                        adminU.Username AS MarkedByAdminName
                    FROM PracticeAttendance pa
                    INNER JOIN PracticeSessions ps ON ps.Id = pa.PracticeSessionId
                    LEFT JOIN Users adminU ON adminU.Id = pa.MarkedByAdminId
                    WHERE pa.PlayerId = @playerId
                    ORDER BY pa.AttendanceDate DESC, ps.DayOfWeek ASC, ps.StartTime ASC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@playerId", playerId);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new PlayerAttendanceViewRow
                        {
                            AttendanceDate = Convert.ToDateTime(reader["AttendanceDate"]),
                            IsPresent = Convert.ToBoolean(reader["IsPresent"]),
                            MarkedAt = reader["MarkedAt"] == DBNull.Value ? null : Convert.ToDateTime(reader["MarkedAt"]),
                            SessionId = Convert.ToInt32(reader["SessionId"]),
                            DayOfWeek = reader["DayOfWeek"].ToString() ?? string.Empty,
                            StartTime = reader["StartTime"].ToString() ?? string.Empty,
                            EndTime = reader["EndTime"].ToString() ?? string.Empty,
                            SessionType = reader["SessionType"].ToString() ?? string.Empty,
                            MarkedByAdminName = reader["MarkedByAdminName"] == DBNull.Value ? string.Empty : reader["MarkedByAdminName"].ToString() ?? string.Empty
                        });
                    }
                }
            }

            return rows;
        }
    }

    // HELPER CLASSES
    public class PracticeAttendanceRow
    {
        public int Id { get; set; }
        public int PracticeSessionId { get; set; }
        public int PlayerId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public bool IsPresent { get; set; }
        public int? MarkedByAdminId { get; set; }
        public DateTime? MarkedAt { get; set; }
    }

    public class PracticeAttendanceSaveItem
    {
        public int PlayerId { get; set; }
        public bool IsPresent { get; set; }
    }

    public class PracticeAttendanceReportRow
    {
        public int PlayerId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalMarkedSessions { get; set; }
        public int MissedSessions { get; set; }
        public double MissPercentage { get; set; }
    }

    public class PlayerAttendanceViewRow
    {
        public DateTime AttendanceDate { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? MarkedAt { get; set; }
        public int SessionId { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public string MarkedByAdminName { get; set; } = string.Empty;
    }
}