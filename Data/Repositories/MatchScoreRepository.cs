using tmsserver.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace tmsserver.Data.Repositories
{
    public interface IMatchScoreRepository
    {
        Task<List<MatchScore>> GetScoresForMatchAsync(int matchId);
        Task<MatchScore?> GetScoreForSetAsync(int matchId, int setNumber);
        Task AddOrUpdateScoreAsync(MatchScore score);
        Task<bool> DeleteScoreAsync(int matchId, int setNumber);
    }

    public class MatchScoreRepository : IMatchScoreRepository
    {
        private readonly string _connectionString;
        public MatchScoreRepository(IConfiguration config)
        {
            _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") ?? 
                config.GetConnectionString("DefaultConnection") ?? 
                throw new InvalidOperationException("Connection string is not configured.");
        }

        public async Task<List<MatchScore>> GetScoresForMatchAsync(int matchId)
        {
            var scores = new List<MatchScore>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM MatchScores WHERE MatchId = @MatchId ORDER BY SetNumber";
            cmd.Parameters.AddWithValue("@MatchId", matchId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                scores.Add(new MatchScore
                {
                    Id = reader.GetInt32(0),
                    MatchId = reader.GetInt32(1),
                    SetNumber = reader.GetInt32(2),
                    Team1Games = reader.GetInt32(3),
                    Team2Games = reader.GetInt32(4),
                    Team1TieBreak = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Team2TieBreak = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    CreatedAt = reader.GetDateTime(7),
                    UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }
            return scores;
        }

        public async Task<MatchScore?> GetScoreForSetAsync(int matchId, int setNumber)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM MatchScores WHERE MatchId = @MatchId AND SetNumber = @SetNumber";
            cmd.Parameters.AddWithValue("@MatchId", matchId);
            cmd.Parameters.AddWithValue("@SetNumber", setNumber);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new MatchScore
                {
                    Id = reader.GetInt32(0),
                    MatchId = reader.GetInt32(1),
                    SetNumber = reader.GetInt32(2),
                    Team1Games = reader.GetInt32(3),
                    Team2Games = reader.GetInt32(4),
                    Team1TieBreak = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Team2TieBreak = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    CreatedAt = reader.GetDateTime(7),
                    UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                };
            }
            return null;
        }

        public async Task AddOrUpdateScoreAsync(MatchScore score)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE MatchScores AS target
USING (SELECT @MatchId AS MatchId, @SetNumber AS SetNumber) AS source
ON (target.MatchId = source.MatchId AND target.SetNumber = source.SetNumber)
WHEN MATCHED THEN
    UPDATE SET Team1Games = @Team1Games, Team2Games = @Team2Games, Team1TieBreak = @Team1TieBreak, Team2TieBreak = @Team2TieBreak, UpdatedAt = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (MatchId, SetNumber, Team1Games, Team2Games, Team1TieBreak, Team2TieBreak, CreatedAt)
    VALUES (@MatchId, @SetNumber, @Team1Games, @Team2Games, @Team1TieBreak, @Team2TieBreak, GETUTCDATE());";
            cmd.Parameters.AddWithValue("@MatchId", score.MatchId);
            cmd.Parameters.AddWithValue("@SetNumber", score.SetNumber);
            cmd.Parameters.AddWithValue("@Team1Games", score.Team1Games);
            cmd.Parameters.AddWithValue("@Team2Games", score.Team2Games);
            cmd.Parameters.AddWithValue("@Team1TieBreak", (object?)score.Team1TieBreak ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Team2TieBreak", (object?)score.Team2TieBreak ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<bool> DeleteScoreAsync(int matchId, int setNumber)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM MatchScores WHERE MatchId = @MatchId AND SetNumber = @SetNumber";
            cmd.Parameters.AddWithValue("@MatchId", matchId);
            cmd.Parameters.AddWithValue("@SetNumber", setNumber);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
