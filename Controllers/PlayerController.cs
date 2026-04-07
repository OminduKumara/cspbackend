using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;

[Route("api/[controller]")]
[ApiController]
[Authorize] // Requires the user to be logged in
public class PlayerController : ControllerBase
{
    private readonly string _connectionString;

    public PlayerController(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") 
            ?? configuration.GetConnectionString("DefaultConnection");
    }

    [HttpGet("dashboard-stats")]
    public IActionResult GetDashboardStats()
    {
        try
        {
            // 1. Extract the Player's ID from their JWT Token
            var userIdClaim = User.FindFirst("sub") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier) 
                ?? User.FindFirst("userId");
                
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int playerId))
            {
                return Unauthorized(new { message = "Invalid player token." });
            }

            int sessionsAttended = 0;
            int sessionsMissed = 0;
            int activeInventoryItems = 0;
            string nextPractice = "No upcoming practices";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // 2. Get Attendance Stats for this specific player
                string attendanceQuery = @"
                    SELECT 
                        SUM(CASE WHEN IsPresent = 1 THEN 1 ELSE 0 END) as Attended,
                        SUM(CASE WHEN IsPresent = 0 THEN 1 ELSE 0 END) as Missed
                    FROM PracticeAttendance
                    WHERE PlayerId = @PlayerId";

                using (SqlCommand cmd = new SqlCommand(attendanceQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@PlayerId", playerId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            sessionsAttended = reader["Attended"] != DBNull.Value ? Convert.ToInt32(reader["Attended"]) : 0;
                            sessionsMissed = reader["Missed"] != DBNull.Value ? Convert.ToInt32(reader["Missed"]) : 0;
                        }
                    }
                }

                // 3. Get Active Inventory Count
                string inventoryQuery = @"
                    SELECT COUNT(*) as ActiveItems 
                    FROM InventoryTransaction 
                    WHERE IssuedToUserId = @PlayerId AND QuantityChanged > 0"; // Assuming positive quantity means they hold it

                using (SqlCommand cmd = new SqlCommand(inventoryQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@PlayerId", playerId);
                    activeInventoryItems = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 4. Get Next Upcoming Practice (Simplistic approach: First practice of the week)
                string nextPracticeQuery = @"
                    SELECT TOP 1 DayOfWeek, StartTime 
                    FROM PracticeSessions 
                    ORDER BY Id ASC"; // Adjust ORDER BY logic based on how you store days

                using (SqlCommand cmd = new SqlCommand(nextPracticeQuery, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            nextPractice = $"{reader["DayOfWeek"]} at {reader["StartTime"]}";
                        }
                    }
                }
            }

            // 5. Calculate Percentage
            int totalSessions = sessionsAttended + sessionsMissed;
            double attendancePercentage = totalSessions == 0 ? 0 : Math.Round(((double)sessionsAttended / totalSessions) * 100, 1);

            // 6. Return the formatted data to the React frontend
            return Ok(new
            {
                success = true,
                data = new
                {
                    attendancePercentage = attendancePercentage,
                    sessionsAttended = sessionsAttended,
                    sessionsMissed = sessionsMissed,
                    activeInventoryItems = activeInventoryItems,
                    nextPractice = nextPractice
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching dashboard stats", error = ex.Message });
        }
    }
}