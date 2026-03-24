namespace tmsserver.Data.Repositories;

using Microsoft.Data.SqlClient;
using tmsserver.Models;

public class GroupRepository : IGroupRepository
{
    private readonly string _connectionString;

    public GroupRepository(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") 
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    public async Task<List<GroupWithPlayers>> GetGroupsWithPlayersByTournamentAsync(int tournamentId)
    {
        var groupsDict = new Dictionary<int, GroupWithPlayers>();

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // Get all groups for tournament
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT g.Id, g.TournamentId, g.GroupName, g.BracketPosition, g.CreatedAt, g.UpdatedAt,
                           gp.Id as GroupPlayerId, gp.PlayerId, gp.AssignedAt,
                           u.Id, u.Username, u.Email, u.Role
                    FROM dbo.Groups g
                    LEFT JOIN dbo.GroupPlayers gp ON g.Id = gp.GroupId
                    LEFT JOIN dbo.Users u ON gp.PlayerId = u.Id
                    WHERE g.TournamentId = @TournamentId
                    ORDER BY g.Id, u.Username
                ";

                command.Parameters.AddWithValue("@TournamentId", tournamentId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int groupId = reader.GetInt32(0);

                        if (!groupsDict.ContainsKey(groupId))
                        {
                            var group = new Group
                            {
                                Id = groupId,
                                TournamentId = reader.GetInt32(1),
                                GroupName = reader.GetString(2),
                                BracketPosition = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                                CreatedAt = reader.GetDateTime(4),
                                UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                            };

                            groupsDict[groupId] = new GroupWithPlayers { Group = group, Players = new() };
                        }

                        if (!reader.IsDBNull(7))
                        {
                            var player = new User
                            {
                                Id = reader.GetInt32(7),
                                Username = reader.GetString(9),
                                Email = reader.GetString(10),
                                Role = (UserRole)reader.GetInt32(11)
                            };

                            groupsDict[groupId].Players.Add(player);
                        }
                    }
                }
            }

            // Update player count
            foreach (var item in groupsDict.Values)
            {
                item.PlayerCount = item.Players.Count;
            }
        }

        return groupsDict.Values.ToList();
    }

    public async Task<GroupWithPlayers?> GetGroupWithPlayersAsync(int groupId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT g.Id, g.TournamentId, g.GroupName, g.BracketPosition, g.CreatedAt, g.UpdatedAt,
                           u.Id, u.Username, u.Email, u.Role
                    FROM dbo.Groups g
                    LEFT JOIN dbo.GroupPlayers gp ON g.Id = gp.GroupId
                    LEFT JOIN dbo.Users u ON gp.PlayerId = u.Id
                    WHERE g.Id = @GroupId
                    ORDER BY u.Username
                ";

                command.Parameters.AddWithValue("@GroupId", groupId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    GroupWithPlayers? result = null;

                    while (await reader.ReadAsync())
                    {
                        if (result == null)
                        {
                            result = new GroupWithPlayers
                            {
                                Group = new Group
                                {
                                    Id = reader.GetInt32(0),
                                    TournamentId = reader.GetInt32(1),
                                    GroupName = reader.GetString(2),
                                    BracketPosition = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                                    CreatedAt = reader.GetDateTime(4),
                                    UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                                },
                                Players = new()
                            };
                        }

                        if (!reader.IsDBNull(6))
                        {
                            var player = new User
                            {
                                Id = reader.GetInt32(6),
                                Username = reader.GetString(7),
                                Email = reader.GetString(8),
                                Role = (UserRole)reader.GetInt32(9)
                            };

                            result.Players.Add(player);
                        }
                    }

                    if (result != null)
                    {
                        result.PlayerCount = result.Players.Count;
                    }

                    return result;
                }
            }
        }
    }

    public async Task<Group?> GetGroupByIdAsync(int groupId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, TournamentId, GroupName, BracketPosition, CreatedAt, UpdatedAt
                    FROM dbo.Groups
                    WHERE Id = @GroupId
                ";

                command.Parameters.AddWithValue("@GroupId", groupId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Group
                        {
                            Id = reader.GetInt32(0),
                            TournamentId = reader.GetInt32(1),
                            GroupName = reader.GetString(2),
                            BracketPosition = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            CreatedAt = reader.GetDateTime(4),
                            UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                        };
                    }
                }
            }
        }

        return null;
    }

    public async Task<List<Group>> GetGroupsByTournamentAsync(int tournamentId)
    {
        var groups = new List<Group>();

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, TournamentId, GroupName, BracketPosition, CreatedAt, UpdatedAt
                    FROM dbo.Groups
                    WHERE TournamentId = @TournamentId
                    ORDER BY GroupName
                ";

                command.Parameters.AddWithValue("@TournamentId", tournamentId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        groups.Add(new Group
                        {
                            Id = reader.GetInt32(0),
                            TournamentId = reader.GetInt32(1),
                            GroupName = reader.GetString(2),
                            BracketPosition = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            CreatedAt = reader.GetDateTime(4),
                            UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                        });
                    }
                }
            }
        }

        return groups;
    }

    public async Task<int> CreateGroupAsync(Group group)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO dbo.Groups (TournamentId, GroupName, BracketPosition, CreatedAt)
                    VALUES (@TournamentId, @GroupName, @BracketPosition, @CreatedAt);
                    SELECT SCOPE_IDENTITY();
                ";

                command.Parameters.AddWithValue("@TournamentId", group.TournamentId);
                command.Parameters.AddWithValue("@GroupName", group.GroupName);
                command.Parameters.AddWithValue("@BracketPosition", group.BracketPosition ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
        }
    }

    public async Task<bool> UpdateGroupAsync(Group group)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    UPDATE dbo.Groups
                    SET GroupName = @GroupName, BracketPosition = @BracketPosition, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id
                ";

                command.Parameters.AddWithValue("@Id", group.Id);
                command.Parameters.AddWithValue("@GroupName", group.GroupName);
                command.Parameters.AddWithValue("@BracketPosition", group.BracketPosition ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> DeleteGroupAsync(int groupId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    DELETE FROM dbo.GroupPlayers WHERE GroupId = @GroupId;
                    DELETE FROM dbo.Groups WHERE Id = @GroupId;
                ";

                command.Parameters.AddWithValue("@GroupId", groupId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> AssignPlayerToGroupAsync(int groupId, int playerId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO dbo.GroupPlayers (GroupId, PlayerId, AssignedAt)
                    VALUES (@GroupId, @PlayerId, @AssignedAt)
                ";

                command.Parameters.AddWithValue("@GroupId", groupId);
                command.Parameters.AddWithValue("@PlayerId", playerId);
                command.Parameters.AddWithValue("@AssignedAt", DateTime.UtcNow);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> RemovePlayerFromGroupAsync(int groupId, int playerId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM dbo.GroupPlayers WHERE GroupId = @GroupId AND PlayerId = @PlayerId";

                command.Parameters.AddWithValue("@GroupId", groupId);
                command.Parameters.AddWithValue("@PlayerId", playerId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> ClearGroupPlayersAsync(int groupId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM dbo.GroupPlayers WHERE GroupId = @GroupId";
                command.Parameters.AddWithValue("@GroupId", groupId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected >= 0;
            }
        }
    }

    public async Task<bool> ClearTournamentGroupsAsync(int tournamentId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    DELETE FROM dbo.GroupPlayers 
                    WHERE GroupId IN (SELECT Id FROM dbo.Groups WHERE TournamentId = @TournamentId);
                    DELETE FROM dbo.Groups WHERE TournamentId = @TournamentId;
                ";

                command.Parameters.AddWithValue("@TournamentId", tournamentId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected >= 0;
            }
        }
    }
}
