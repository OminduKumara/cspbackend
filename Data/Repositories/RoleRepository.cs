using Microsoft.Data.SqlClient;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface IRoleRepository
{
    Task<List<RoleEntity>> GetAllRolesAsync();
    Task<RoleEntity?> GetRoleByIdAsync(int id);
    Task<RoleEntity?> GetRoleByNameAsync(string name);
    Task<RoleEntity> CreateRoleAsync(RoleEntity role);
    Task<bool> UpdateRoleAsync(RoleEntity role);
    Task<bool> DeleteRoleAsync(int id);
}

public class RoleRepository : IRoleRepository
{
    private readonly string _connectionString;

    public RoleRepository(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
    }

    public async Task<List<RoleEntity>> GetAllRolesAsync()
    {
        var roles = new List<RoleEntity>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Permissions, CreatedAt FROM Roles";

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    roles.Add(BuildRoleFromReader(reader));
                }
            }
        }
        return roles;
    }

    public async Task<RoleEntity?> GetRoleByIdAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Permissions, CreatedAt FROM Roles WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildRoleFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<RoleEntity?> GetRoleByNameAsync(string name)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Permissions, CreatedAt FROM Roles WHERE Name = @name";
            command.Parameters.AddWithValue("@name", name);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildRoleFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<RoleEntity> CreateRoleAsync(RoleEntity role)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Roles (Name, Description, Permissions, CreatedAt)
                VALUES (@name, @description, @permissions, @createdAt);
                SELECT LAST_INSERT_ID();";
            
            command.Parameters.AddWithValue("@name", role.Name);
            command.Parameters.AddWithValue("@description", role.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@permissions", role.PermissionsJson ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@createdAt", role.CreatedAt);

            var result = await command.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out int newId))
            {
                role.Id = newId;
            }
        }
        return role;
    }

    public async Task<bool> UpdateRoleAsync(RoleEntity role)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Roles
                SET Name = @name, Description = @description, Permissions = @permissions
                WHERE Id = @id";
            
            command.Parameters.AddWithValue("@id", role.Id);
            command.Parameters.AddWithValue("@name", role.Name);
            command.Parameters.AddWithValue("@description", role.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@permissions", role.PermissionsJson ?? (object)DBNull.Value);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    public async Task<bool> DeleteRoleAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Roles WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    private RoleEntity BuildRoleFromReader(SqlDataReader reader)
    {
        return new RoleEntity
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            PermissionsJson = reader.IsDBNull(reader.GetOrdinal("Permissions")) ? null : reader.GetString(reader.GetOrdinal("Permissions")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }
}
