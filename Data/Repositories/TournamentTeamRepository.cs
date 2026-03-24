using Microsoft.Data.SqlClient;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface ITournamentTeamRepository
{
    Task<List<TournamentTeam>> GetTeamsByTournamentAsync(int tournamentId);
    Task<TournamentTeam?> GetTeamByIdAsync(int teamId);
    Task<TournamentTeam> CreateTeamAsync(TournamentTeam team);
    Task<bool> UpdateTeamAsync(TournamentTeam team);
    Task<bool> DeleteTeamAsync(int teamId);
    Task<bool> DeleteAllTeamsByTournamentAsync(int tournamentId);
}

public class TournamentTeamRepository : ITournamentTeamRepository
{
    private readonly string _connectionString;

    public TournamentTeamRepository(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
    }

    public async Task<List<TournamentTeam>> GetTeamsByTournamentAsync(int tournamentId)
    {
        var teams = new List<TournamentTeam>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, TournamentId, TeamName, TeamOrder, CreatedAt, UpdatedAt
                FROM TournamentTeams
                WHERE TournamentId = @tournamentId
                ORDER BY TeamOrder ASC";
            command.Parameters.AddWithValue("@tournamentId", tournamentId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    teams.Add(BuildTeamFromReader(reader));
                }
            }
        }
        return teams;
    }

    public async Task<TournamentTeam?> GetTeamByIdAsync(int teamId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, TournamentId, TeamName, TeamOrder, CreatedAt, UpdatedAt
                FROM TournamentTeams
                WHERE Id = @teamId";
            command.Parameters.AddWithValue("@teamId", teamId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildTeamFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<TournamentTeam> CreateTeamAsync(TournamentTeam team)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO TournamentTeams (TournamentId, TeamName, TeamOrder, CreatedAt)
                VALUES (@tournamentId, @teamName, @teamOrder, @createdAt);
                SELECT CAST(SCOPE_IDENTITY() as int);";
            command.Parameters.AddWithValue("@tournamentId", team.TournamentId);
            command.Parameters.AddWithValue("@teamName", team.TeamName);
            command.Parameters.AddWithValue("@teamOrder", team.TeamOrder);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

            team.Id = (int)await command.ExecuteScalarAsync()!;
            team.CreatedAt = DateTime.UtcNow;
        }
        return team;
    }

    public async Task<bool> UpdateTeamAsync(TournamentTeam team)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE TournamentTeams
                SET TeamName = @teamName, TeamOrder = @teamOrder, UpdatedAt = @updatedAt
                WHERE Id = @id";
            command.Parameters.AddWithValue("@teamName", team.TeamName);
            command.Parameters.AddWithValue("@teamOrder", team.TeamOrder);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@id", team.Id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }

    public async Task<bool> DeleteTeamAsync(int teamId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TournamentTeams WHERE Id = @teamId";
            command.Parameters.AddWithValue("@teamId", teamId);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }

    public async Task<bool> DeleteAllTeamsByTournamentAsync(int tournamentId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TournamentTeams WHERE TournamentId = @tournamentId";
            command.Parameters.AddWithValue("@tournamentId", tournamentId);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }

    private TournamentTeam BuildTeamFromReader(SqlDataReader reader)
    {
        return new TournamentTeam
        {
            Id = reader.GetInt32(0),
            TournamentId = reader.GetInt32(1),
            TeamName = reader.GetString(2),
            TeamOrder = reader.GetInt32(3),
            CreatedAt = reader.GetDateTime(4),
            UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
        };
    }
}
