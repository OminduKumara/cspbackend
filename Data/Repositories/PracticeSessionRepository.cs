using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient; 
using Microsoft.Extensions.Configuration;
using tmsserver.Models;

namespace tmsserver.Data.Repositories
{
    public class PracticeSessionRepository
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
                
                // Using parameters prevents SQL Injection attacks!
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
                
                // The Id parameter is crucial here so Azure knows exactly which row to update
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
    }
}