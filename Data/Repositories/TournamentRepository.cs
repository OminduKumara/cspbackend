using Microsoft.Data.SqlClient;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface ITournamentRepository
{
    Task<List<Tournament>> GetAllTournamentsAsync();
    Task<Tournament?> GetTournamentByIdAsync(int id);
    Task<List<Tournament>> GetTournamentsByStatusAsync(TournamentStatus status);
    Task<Tournament> CreateTournamentAsync(Tournament tournament);
    Task<bool> UpdateTournamentAsync(Tournament tournament);
    Task<bool> DeleteTournamentAsync(int id);
    Task<bool> UpdateTournamentStatusAsync(int id, TournamentStatus status, int updatedByAdminId);
}

public class TournamentRepository : ITournamentRepository
{
    private readonly string _connectionString;

    public TournamentRepository(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
    }

    public async Task<List<Tournament>> GetAllTournamentsAsync()
    {
        var tournaments = new List<Tournament>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Status, StartDate, EndDate, CreatedByAdminId, CreatedAt, UpdatedAt, UpdatedByAdminId FROM Tournaments ORDER BY StartDate DESC";

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tournaments.Add(BuildTournamentFromReader(reader));
                }
            }
        }
        return tournaments;
    }

    public async Task<Tournament?> GetTournamentByIdAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Status, StartDate, EndDate, CreatedByAdminId, CreatedAt, UpdatedAt, UpdatedByAdminId FROM Tournaments WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildTournamentFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<List<Tournament>> GetTournamentsByStatusAsync(TournamentStatus status)
    {
        var tournaments = new List<Tournament>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Status, StartDate, EndDate, CreatedByAdminId, CreatedAt, UpdatedAt, UpdatedByAdminId FROM Tournaments WHERE Status = @status ORDER BY StartDate DESC";
            command.Parameters.AddWithValue("@status", (int)status);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tournaments.Add(BuildTournamentFromReader(reader));
                }
            }
        }
        return tournaments;
    }

    public async Task<Tournament> CreateTournamentAsync(Tournament tournament)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Tournaments (Name, Description, Status, StartDate, EndDate, CreatedByAdminId, CreatedAt) 
                VALUES (@name, @description, @status, @startDate, @endDate, @createdByAdminId, @createdAt);
                SELECT CAST(SCOPE_IDENTITY() as int);";
            
            command.Parameters.AddWithValue("@name", tournament.Name);
            command.Parameters.AddWithValue("@description", tournament.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@status", (int)tournament.Status);
            command.Parameters.AddWithValue("@startDate", tournament.StartDate);
            command.Parameters.AddWithValue("@endDate", tournament.EndDate);
            command.Parameters.AddWithValue("@createdByAdminId", tournament.CreatedByAdminId);
            command.Parameters.AddWithValue("@createdAt", tournament.CreatedAt);

            var result = await command.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out int newId))
            {
                tournament.Id = newId;
            }
        }
        return tournament;
    }

    public async Task<bool> UpdateTournamentAsync(Tournament tournament)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tournaments 
                SET Name = @name, Description = @description, Status = @status, 
                    StartDate = @startDate, EndDate = @endDate, UpdatedAt = @updatedAt, UpdatedByAdminId = @updatedByAdminId
                WHERE Id = @id";
            
            command.Parameters.AddWithValue("@id", tournament.Id);
            command.Parameters.AddWithValue("@name", tournament.Name);
            command.Parameters.AddWithValue("@description", tournament.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@status", (int)tournament.Status);
            command.Parameters.AddWithValue("@startDate", tournament.StartDate);
            command.Parameters.AddWithValue("@endDate", tournament.EndDate);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@updatedByAdminId", tournament.UpdatedByAdminId ?? (object)DBNull.Value);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    public async Task<bool> DeleteTournamentAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Tournaments WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    public async Task<bool> UpdateTournamentStatusAsync(int id, TournamentStatus status, int updatedByAdminId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Tournaments 
                SET Status = @status, UpdatedAt = @updatedAt, UpdatedByAdminId = @updatedByAdminId
                WHERE Id = @id";
            
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@status", (int)status);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@updatedByAdminId", updatedByAdminId);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    private Tournament BuildTournamentFromReader(SqlDataReader reader)
    {
        return new Tournament
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString(reader.GetOrdinal("Description")),
            Status = (TournamentStatus)reader.GetByte(reader.GetOrdinal("Status")),
            StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")),
            EndDate = reader.GetDateTime(reader.GetOrdinal("EndDate")),
            CreatedByAdminId = reader.GetInt32(reader.GetOrdinal("CreatedByAdminId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            UpdatedByAdminId = reader.IsDBNull(reader.GetOrdinal("UpdatedByAdminId")) ? null : reader.GetInt32(reader.GetOrdinal("UpdatedByAdminId"))
        };
    }
}
