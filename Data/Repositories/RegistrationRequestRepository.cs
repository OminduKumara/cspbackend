using Microsoft.Data.SqlClient;
using tmsserver.Models;

namespace tmsserver.Data.Repositories;

public interface IRegistrationRequestRepository
{
    Task<List<RegistrationRequest>> GetAllPendingRequestsAsync();
    Task<RegistrationRequest?> GetRequestByIdAsync(int id);
    Task<RegistrationRequest?> GetRequestByUserIdAsync(int userId);
    Task<List<RegistrationRequest>> GetRequestsByStatusAsync(string status);
    Task<RegistrationRequest> CreateRequestAsync(RegistrationRequest request);
    Task<bool> UpdateRequestStatusAsync(int requestId, string status, int? reviewedByAdminId = null, string? rejectionReason = null);
    Task<bool> ApproveRequestAsync(int requestId, int approvedByAdminId);
    Task<bool> RejectRequestAsync(int requestId, int rejectedByAdminId, string reason);
}

public class RegistrationRequestRepository : IRegistrationRequestRepository
{
    private readonly string _connectionString;

    public RegistrationRequestRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<List<RegistrationRequest>> GetAllPendingRequestsAsync()
    {
        var requests = new List<RegistrationRequest>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT r.Id, r.UserId, r.Status, r.ReviewedByAdminId, r.ReviewedAt, r.RejectionReason, r.CreatedAt
                FROM RegistrationRequests r
                WHERE r.Status = 'Pending'
                ORDER BY r.CreatedAt ASC";

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    requests.Add(BuildRegistrationRequestFromReader(reader));
                }
            }
        }
        return requests;
    }

    public async Task<RegistrationRequest?> GetRequestByIdAsync(int id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, Status, ReviewedByAdminId, ReviewedAt, RejectionReason, CreatedAt
                FROM RegistrationRequests
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildRegistrationRequestFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<RegistrationRequest?> GetRequestByUserIdAsync(int userId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, Status, ReviewedByAdminId, ReviewedAt, RejectionReason, CreatedAt
                FROM RegistrationRequests
                WHERE UserId = @userId";
            command.Parameters.AddWithValue("@userId", userId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return BuildRegistrationRequestFromReader(reader);
                }
            }
        }
        return null;
    }

    public async Task<List<RegistrationRequest>> GetRequestsByStatusAsync(string status)
    {
        var requests = new List<RegistrationRequest>();
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, Status, ReviewedByAdminId, ReviewedAt, RejectionReason, CreatedAt
                FROM RegistrationRequests
                WHERE Status = @status
                ORDER BY CreatedAt ASC";
            command.Parameters.AddWithValue("@status", status);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    requests.Add(BuildRegistrationRequestFromReader(reader));
                }
            }
        }
        return requests;
    }

    public async Task<RegistrationRequest> CreateRequestAsync(RegistrationRequest request)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO RegistrationRequests (UserId, Status, CreatedAt)
                VALUES (@userId, @status, @createdAt);
                SELECT LAST_INSERT_ID();";
            
            command.Parameters.AddWithValue("@userId", request.UserId);
            command.Parameters.AddWithValue("@status", request.Status);
            command.Parameters.AddWithValue("@createdAt", request.CreatedAt);

            var result = await command.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out int newId))
            {
                request.Id = newId;
            }
        }
        return request;
    }

    public async Task<bool> UpdateRequestStatusAsync(int requestId, string status, int? reviewedByAdminId = null, string? rejectionReason = null)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE RegistrationRequests
                SET Status = @status, ReviewedByAdminId = @reviewedByAdminId, ReviewedAt = @reviewedAt, RejectionReason = @rejectionReason
                WHERE Id = @id";
            
            command.Parameters.AddWithValue("@id", requestId);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@reviewedByAdminId", reviewedByAdminId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@reviewedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@rejectionReason", rejectionReason ?? (object)DBNull.Value);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
    }

    public async Task<bool> ApproveRequestAsync(int requestId, int approvedByAdminId)
    {
        return await UpdateRequestStatusAsync(requestId, "Approved", approvedByAdminId);
    }

    public async Task<bool> RejectRequestAsync(int requestId, int rejectedByAdminId, string reason)
    {
        return await UpdateRequestStatusAsync(requestId, "Rejected", rejectedByAdminId, reason);
    }

    private RegistrationRequest BuildRegistrationRequestFromReader(SqlDataReader reader)
    {
        return new RegistrationRequest
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            ReviewedByAdminId = reader.IsDBNull(reader.GetOrdinal("ReviewedByAdminId")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedByAdminId")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            RejectionReason = reader.IsDBNull(reader.GetOrdinal("RejectionReason")) ? null : reader.GetString(reader.GetOrdinal("RejectionReason")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }
}
