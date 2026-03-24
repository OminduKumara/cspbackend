namespace tmsserver.Data.Repositories;

using tmsserver.Models;

public interface IGroupRepository
{
    Task<List<GroupWithPlayers>> GetGroupsWithPlayersByTournamentAsync(int tournamentId);
    Task<GroupWithPlayers?> GetGroupWithPlayersAsync(int groupId);
    Task<Group?> GetGroupByIdAsync(int groupId);
    Task<List<Group>> GetGroupsByTournamentAsync(int tournamentId);
    Task<int> CreateGroupAsync(Group group);
    Task<bool> UpdateGroupAsync(Group group);
    Task<bool> DeleteGroupAsync(int groupId);
    Task<bool> AssignPlayerToGroupAsync(int groupId, int playerId);
    Task<bool> RemovePlayerFromGroupAsync(int groupId, int playerId);
    Task<bool> ClearGroupPlayersAsync(int groupId);
    Task<bool> ClearTournamentGroupsAsync(int tournamentId);
}
