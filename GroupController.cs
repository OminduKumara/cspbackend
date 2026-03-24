namespace tmsserver;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tmsserver.Data.Repositories;
using tmsserver.Models;

[ApiController]
[Route("api/group")]
public class GroupController : ControllerBase
{
    private readonly IGroupRepository _groupRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IUserRepository _userRepository;

    public GroupController(
        IGroupRepository groupRepository, 
        ITournamentRepository tournamentRepository,
        IUserRepository userRepository)
    {
        _groupRepository = groupRepository;
        _tournamentRepository = tournamentRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Get all groups with players for a tournament
    /// GET /api/group/tournament/{tournamentId}
    /// </summary>
    [HttpGet("tournament/{tournamentId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetGroupsByTournament(int tournamentId)
    {
        try
        {
            var groups = await _groupRepository.GetGroupsWithPlayersByTournamentAsync(tournamentId);
            return Ok(new { success = true, data = groups, count = groups.Count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific group with all players
    /// GET /api/group/{groupId}
    /// </summary>
    [HttpGet("{groupId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetGroup(int groupId)
    {
        try
        {
            var group = await _groupRepository.GetGroupWithPlayersAsync(groupId);
            if (group == null)
                return NotFound(new { success = false, message = "Group not found" });

            return Ok(new { success = true, data = group });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Create groups with random player assignment
    /// POST /api/group/random-assign
    /// </summary>
    [HttpPost("random-assign")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateGroupsWithRandomAssignment([FromBody] RandomGroupAssignmentRequest request)
    {
        try
        {
            if (request.NumberOfGroups <= 0)
                return BadRequest(new { success = false, message = "Number of groups must be greater than 0" });

            if (request.PlayerIds.Count == 0)
                return BadRequest(new { success = false, message = "No players provided for assignment" });

            if (request.PlayerIds.Count < request.NumberOfGroups)
                return BadRequest(new { success = false, message = "Not enough players for the requested number of groups" });

            // Clear existing groups for this tournament
            await _groupRepository.ClearTournamentGroupsAsync(request.TournamentId);

            // Create groups
            var groups = new List<Group>();
            var groupNames = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };

            for (int i = 0; i < request.NumberOfGroups; i++)
            {
                var group = new Group
                {
                    TournamentId = request.TournamentId,
                    GroupName = $"Group {groupNames[i % groupNames.Length]}",
                    BracketPosition = i + 1
                };

                int groupId = await _groupRepository.CreateGroupAsync(group);
                group.Id = groupId;
                groups.Add(group);
            }

            // Randomly assign players to groups
            var random = new Random();
            var shuffledPlayers = request.PlayerIds.OrderBy(_ => random.Next()).ToList();

            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                int groupIndex = i % request.NumberOfGroups;
                await _groupRepository.AssignPlayerToGroupAsync(groups[groupIndex].Id, shuffledPlayers[i]);
            }

            // Return created groups with players
            var createdGroups = await _groupRepository.GetGroupsWithPlayersByTournamentAsync(request.TournamentId);

            return Ok(new 
            { 
                success = true, 
                message = "Groups created and players randomly assigned", 
                data = createdGroups,
                count = createdGroups.Count 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Manually create a single group
    /// POST /api/group
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateGroup([FromBody] Group groupRequest)
    {
        try
        {
            if (string.IsNullOrEmpty(groupRequest.GroupName))
                return BadRequest(new { success = false, message = "Group name is required" });

            int groupId = await _groupRepository.CreateGroupAsync(groupRequest);

            if (groupId == 0)
                return BadRequest(new { success = false, message = "Failed to create group" });

            return Ok(new 
            { 
                success = true, 
                message = "Group created successfully", 
                data = new { id = groupId, groupRequest.GroupName, groupRequest.TournamentId, groupRequest.BracketPosition }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Update a group's details
    /// PUT /api/group/{groupId}
    /// </summary>
    [HttpPut("{groupId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateGroup(int groupId, [FromBody] GroupUpdateRequest request)
    {
        try
        {
            var existingGroup = await _groupRepository.GetGroupByIdAsync(groupId);
            if (existingGroup == null)
                return NotFound(new { success = false, message = "Group not found" });

            existingGroup.GroupName = request.GroupName;
            existingGroup.BracketPosition = request.BracketPosition;

            bool updated = await _groupRepository.UpdateGroupAsync(existingGroup);

            if (!updated)
                return BadRequest(new { success = false, message = "Failed to update group" });

            // Update player assignments if provided
            if (request.PlayerIds != null && request.PlayerIds.Count > 0)
            {
                await _groupRepository.ClearGroupPlayersAsync(groupId);
                foreach (var playerId in request.PlayerIds)
                {
                    await _groupRepository.AssignPlayerToGroupAsync(groupId, playerId);
                }
            }

            var updatedGroup = await _groupRepository.GetGroupWithPlayersAsync(groupId);
            return Ok(new { success = true, message = "Group updated successfully", data = updatedGroup });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Assign a player to a group
    /// POST /api/group/{groupId}/player/{playerId}
    /// </summary>
    [HttpPost("{groupId}/player/{playerId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AssignPlayerToGroup(int groupId, int playerId)
    {
        try
        {
            var group = await _groupRepository.GetGroupByIdAsync(groupId);
            if (group == null)
                return NotFound(new { success = false, message = "Group not found" });

            bool assigned = await _groupRepository.AssignPlayerToGroupAsync(groupId, playerId);

            if (!assigned)
                return BadRequest(new { success = false, message = "Failed to assign player to group" });

            var updatedGroup = await _groupRepository.GetGroupWithPlayersAsync(groupId);
            return Ok(new { success = true, message = "Player assigned to group", data = updatedGroup });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Remove a player from a group
    /// DELETE /api/group/{groupId}/player/{playerId}
    /// </summary>
    [HttpDelete("{groupId}/player/{playerId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemovePlayerFromGroup(int groupId, int playerId)
    {
        try
        {
            bool removed = await _groupRepository.RemovePlayerFromGroupAsync(groupId, playerId);

            if (!removed)
                return BadRequest(new { success = false, message = "Failed to remove player from group" });

            var updatedGroup = await _groupRepository.GetGroupWithPlayersAsync(groupId);
            return Ok(new { success = true, message = "Player removed from group", data = updatedGroup });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a group and all its assignments
    /// DELETE /api/group/{groupId}
    /// </summary>
    [HttpDelete("{groupId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteGroup(int groupId)
    {
        try
        {
            bool deleted = await _groupRepository.DeleteGroupAsync(groupId);

            if (!deleted)
                return BadRequest(new { success = false, message = "Failed to delete group" });

            return Ok(new { success = true, message = "Group deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Reassign all groups for a tournament randomly
    /// POST /api/group/tournament/{tournamentId}/reassign
    /// </summary>
    [HttpPost("tournament/{tournamentId}/reassign")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ReassignGroups(int tournamentId, [FromBody] RandomGroupAssignmentRequest request)
    {
        try
        {
            // This reuses the random assignment logic by calling the endpoint logic
            request.TournamentId = tournamentId;
            return await CreateGroupsWithRandomAssignment(request);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
