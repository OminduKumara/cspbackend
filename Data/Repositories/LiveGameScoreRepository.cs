using tmsserver.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace tmsserver.Data.Repositories
{
    public interface ILiveGameScoreRepository
    {
        Task<LiveGameScore?> GetLiveScoreAsync(int matchId);
        Task SetLiveScoreAsync(LiveGameScore score);
    }

    public class LiveGameScoreRepository : ILiveGameScoreRepository
    {
        private readonly string _connectionString;
        public LiveGameScoreRepository(IConfiguration config)
        {
            _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") ?? 
                config.GetConnectionString("DefaultConnection") ?? 
                throw new InvalidOperationException("Connection string is not configured.");
        }

        public async Task<LiveGameScore?> GetLiveScoreAsync(int matchId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LiveGameScores WHERE MatchId = @MatchId";
            cmd.Parameters.AddWithValue("@MatchId", matchId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LiveGameScore
                {
                    MatchId = reader.GetInt32(0),
                    Team1Points = reader.GetString(1),
                    Team2Points = reader.GetString(2),
                    ServingTeamId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    UpdatedAt = reader.GetDateTime(4)
                };
            }
            return null;
        }

        public async Task SetLiveScoreAsync(LiveGameScore score)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE LiveGameScores AS target
USING (SELECT @MatchId AS MatchId) AS source
ON (target.MatchId = source.MatchId)
WHEN MATCHED THEN
    UPDATE SET Team1Points = @Team1Points, Team2Points = @Team2Points, ServingTeamId = @ServingTeamId, UpdatedAt = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (MatchId, Team1Points, Team2Points, ServingTeamId, UpdatedAt)
    VALUES (@MatchId, @Team1Points, @Team2Points, @ServingTeamId, GETUTCDATE());";
            cmd.Parameters.AddWithValue("@MatchId", score.MatchId);
            cmd.Parameters.AddWithValue("@Team1Points", score.Team1Points);
            cmd.Parameters.AddWithValue("@Team2Points", score.Team2Points);
            cmd.Parameters.AddWithValue("@ServingTeamId", (object?)score.ServingTeamId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
