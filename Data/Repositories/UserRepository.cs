using Microsoft.Data.SqlClient;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface IUserRepository
{
    Task<List<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdentityNumberAsync(string identityNumber);
    Task<List<User>> GetUsersByRoleAsync(UserRole role);
    Task<List<User>> GetPendingApprovalsAsync();
    Task<User> CreateUserAsync(User user);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> ApproveUserAsync(int userId, int approvedByAdminId);
    Task<bool> DeleteUserAsync(int id);
}

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var users = new List<User>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users";

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    users.Add(BuildUserFromReader(reader));
                }
            }
        }
        return users;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildUserFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildUserFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Email = @email";
            command.Parameters.AddWithValue("@email", email);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildUserFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<User?> GetUserByIdentityNumberAsync(string identityNumber)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE IdentityNumber = @identityNumber";
            command.Parameters.AddWithValue("@identityNumber", identityNumber);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildUserFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<List<User>> GetUsersByRoleAsync(UserRole role)
    {
        var users = new List<User>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Role = @role";
            command.Parameters.AddWithValue("@role", (int)role);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    users.Add(BuildUserFromReader(reader));
                }
            }
        }
        return users;
    }

    public async Task<List<User>> GetPendingApprovalsAsync()
    {
        var users = new List<User>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, ApprovedByAdminId, CreatedAt, ApprovedAt FROM Users WHERE Role = @role AND IsApproved = 0";
            command.Parameters.AddWithValue("@role", (int)UserRole.PendingPlayer);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    users.Add(BuildUserFromReader(reader));
                }
            }
        }
        return users;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Users (Username, Email, IdentityNumber, PasswordHash, Role, IsApproved, CreatedAt) 
                VALUES (@username, @email, @identityNumber, @passwordHash, @role, @isApproved, @createdAt);
                SELECT LAST_INSERT_ID();";
            
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@identityNumber", user.IdentityNumber);
            command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            command.Parameters.AddWithValue("@role", (int)user.Role);
            command.Parameters.AddWithValue("@isApproved", user.IsApproved);
            command.Parameters.AddWithValue("@createdAt", user.CreatedAt);

            var result = await command.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out int newId))
            {
                user.Id = newId;
            }
        }
        return user;
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users 
                SET Username = @username, Email = @email, IdentityNumber = @identityNumber, 
                    PasswordHash = @passwordHash, Role = @role, IsApproved = @isApproved, 
                    ApprovedByAdminId = @approvedByAdminId, ApprovedAt = @approvedAt
                WHERE Id = @id";
            
            command.Parameters.AddWithValue("@id", user.Id);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@identityNumber", user.IdentityNumber);
            command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            command.Parameters.AddWithValue("@role", (int)user.Role);
            command.Parameters.AddWithValue("@isApproved", user.IsApproved);
            command.Parameters.AddWithValue("@approvedByAdminId", user.ApprovedByAdminId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@approvedAt", user.ApprovedAt ?? (object)DBNull.Value);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    public async Task<bool> ApproveUserAsync(int userId, int approvedByAdminId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users 
                SET IsApproved = 1, Role = @playerRole, ApprovedByAdminId = @approvedByAdminId, ApprovedAt = @approvedAt
                WHERE Id = @id AND Role = @pendingRole";
            
            command.Parameters.AddWithValue("@id", userId);
            command.Parameters.AddWithValue("@approvedByAdminId", approvedByAdminId);
            command.Parameters.AddWithValue("@approvedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@playerRole", (int)UserRole.Player);
            command.Parameters.AddWithValue("@pendingRole", (int)UserRole.PendingPlayer);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Users WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    private User BuildUserFromReader(SqlDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            IdentityNumber = reader.GetString(reader.GetOrdinal("IdentityNumber")),
            PasswordHash = reader.IsDBNull(reader.GetOrdinal("PasswordHash")) ? string.Empty : reader.GetString(reader.GetOrdinal("PasswordHash")),
            Role = (UserRole)reader.GetInt32(reader.GetOrdinal("Role")),
            IsApproved = reader.GetBoolean(reader.GetOrdinal("IsApproved")),
            ApprovedByAdminId = reader.IsDBNull(reader.GetOrdinal("ApprovedByAdminId")) ? null : reader.GetInt32(reader.GetOrdinal("ApprovedByAdminId")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            ApprovedAt = reader.IsDBNull(reader.GetOrdinal("ApprovedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ApprovedAt"))
        };
    }
}
