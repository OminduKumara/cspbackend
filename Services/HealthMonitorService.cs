using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace tmsserver.Services
{
    public class HealthMonitorService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthMonitorService> _logger;
        private readonly string _connectionString;

        public HealthMonitorService(IConfiguration configuration, ILogger<HealthMonitorService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") 
                ?? configuration.GetConnectionString("DefaultConnection");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Health Monitor Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                string status = "Unhealthy";

                try
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync(stoppingToken);
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT 1";
                            await command.ExecuteScalarAsync(stoppingToken);
                            status = "Healthy";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Background health check failed: {ex.Message}");
                }

                // Log the result to our new table
                await LogHealthStatusAsync(status);

                // Wait 15 minutes before pinging again
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private async Task LogHealthStatusAsync(string status)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        // 1. Insert the new record
                        command.CommandText = "INSERT INTO dbo.SystemHealthLogs (Status) VALUES (@Status);";
                        command.Parameters.AddWithValue("@Status", status);
                        await command.ExecuteNonQueryAsync();

                        // 2. Auto-delete data older than 7 days (Data Retention Policy!)
                        command.CommandText = "DELETE FROM dbo.SystemHealthLogs WHERE PingedAt < DATEADD(day, -7, GETUTCDATE());";
                        await command.ExecuteNonQueryAsync();
                    }
                }
                _logger.LogInformation($"Logged system health status: {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write health log to database: {ex.Message}");
            }
        }
    }
}