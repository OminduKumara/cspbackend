using Microsoft.Data.SqlClient;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface ITournamentMatchRepository
{
    Task<List<TournamentMatch>> GetMatchesByTournamentAsync(int tournamentId);
    Task<List<TournamentMatch>> GetPlayoffMatchesByTournamentAsync(int tournamentId);
    Task<TournamentMatch?> GetMatchByIdAsync(int matchId);
    Task<TournamentMatch> CreateMatchAsync(TournamentMatch match);
    Task<bool> UpdateMatchAsync(TournamentMatch match);
    Task<bool> DeleteMatchAsync(int matchId);
    Task<bool> DeleteAllMatchesByTournamentAsync(int tournamentId);
    Task<List<TournamentMatch>> GetRegularMatchesByTournamentAsync(int tournamentId);
}

public class TournamentMatchRepository : ITournamentMatchRepository
{
    private readonly string _connectionString;

    public TournamentMatchRepository(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
    }

    public async Task<List<TournamentMatch>> GetMatchesByTournamentAsync(int tournamentId)
    {
        var matches = new List<TournamentMatch>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, TournamentId, Team1Id, Team2Id, WinnerId, IsPlayoff, CreatedAt, UpdatedAt
                FROM TournamentMatches
                WHERE TournamentId = @tournamentId
                ORDER BY IsPlayoff ASC, CreatedAt ASC";
            command.Parameters.AddWithValue("@tournamentId", tournamentId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    matches.Add(BuildMatchFromReader(reader));
                }
            }
        }
        return matches;
    }

    public async Task<List<TournamentMatch>> GetRegularMatchesByTournamentAsync(int tournamentId)
    {
        var matches = new List<TournamentMatch>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, TournamentId, Team1Id, Team2Id, WinnerId, IsPlayoff, CreatedAt, UpdatedAt
                FROM TournamentMatches
                WHERE TournamentId = @tournamentId AND IsPlayoff = 0
                ORDER BY CreatedAt ASC";
            command.Parameters.AddWithValue("@tournamentId", tournamentId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    matches.Add(BuildMatchFromReader(reader));
                }
            }
        }
        return matches;
    }

    public async Task<List<TournamentMatch>> GetPlayoffMatchesByTournamentAsync(int tournamentId)
    {
        var matches = new List<TournamentMatch>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, TournamentId, Team1Id, Team2Id, WinnerId, IsPlayoff, CreatedAt, UpdatedAt
                FROM TournamentMatches
                WHERE TournamentId = @tournamentId AND IsPlayoff = 1
                ORDER BY CreatedAt ASC";
            command.Parameters.AddWithValue("@tournamentId", tournamentId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    matches.Add(BuildMatchFromReader(reader));
                }
            }
        }
        return matches;
    }

    public async Task<TournamentMatch?> GetMatchByIdAsync(int matchId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, TournamentId, Team1Id, Team2Id, WinnerId, IsPlayoff, CreatedAt, UpdatedAt
                FROM TournamentMatches
                WHERE Id = @matchId";
            command.Parameters.AddWithValue("@matchId", matchId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildMatchFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<TournamentMatch> CreateMatchAsync(TournamentMatch match)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO TournamentMatches (TournamentId, Team1Id, Team2Id, WinnerId, IsPlayoff, CreatedAt)
                VALUES (@tournamentId, @team1Id, @team2Id, @winnerId, @isPlayoff, @createdAt);
                SELECT CAST(SCOPE_IDENTITY() as int);";
            command.Parameters.AddWithValue("@tournamentId", match.TournamentId);
            command.Parameters.AddWithValue("@team1Id", match.Team1Id);
            command.Parameters.AddWithValue("@team2Id", match.Team2Id);
            command.Parameters.AddWithValue("@winnerId", match.WinnerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isPlayoff", match.IsPlayoff);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

            match.Id = (int)await command.ExecuteScalarAsync()!;
            match.CreatedAt = DateTime.UtcNow;
        }
        return match;
    }

    public async Task<bool> UpdateMatchAsync(TournamentMatch match)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE TournamentMatches
                SET WinnerId = @winnerId, UpdatedAt = @updatedAt
                WHERE Id = @id";
            command.Parameters.AddWithValue("@winnerId", match.WinnerId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@id", match.Id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }

    public async Task<bool> DeleteMatchAsync(int matchId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TournamentMatches WHERE Id = @matchId";
            command.Parameters.AddWithValue("@matchId", matchId);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }

    public async Task<bool> DeleteAllMatchesByTournamentAsync(int tournamentId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TournamentMatches WHERE TournamentId = @tournamentId";
            command.Parameters.AddWithValue("@tournamentId", tournamentId);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }

    private TournamentMatch BuildMatchFromReader(SqlDataReader reader)
    {
        return new TournamentMatch
        {
            Id = reader.GetInt32(0),
            TournamentId = reader.GetInt32(1),
            Team1Id = reader.GetInt32(2),
            Team2Id = reader.GetInt32(3),
            WinnerId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            IsPlayoff = reader.GetBoolean(5),
            CreatedAt = reader.GetDateTime(6),
            UpdatedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
        };
    }
}
