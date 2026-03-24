namespace tmsserver.Models;

public class Group
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public string GroupName { get; set; } = string.Empty;  // e.g., "Group A", "Group B"
    public int? BracketPosition { get; set; }  // Position in bracket for knockout stages
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class GroupPlayer
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int PlayerId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public class GroupWithPlayers
{
    public Group Group { get; set; } = new();
    public List<User> Players { get; set; } = new();
    public int PlayerCount { get; set; }
}

public class RandomGroupAssignmentRequest
{
    public int TournamentId { get; set; }
    public int NumberOfGroups { get; set; }
    public List<int> PlayerIds { get; set; } = new();
}

public class GroupUpdateRequest
{
    public string GroupName { get; set; } = string.Empty;
    public int? BracketPosition { get; set; }
    public List<int> PlayerIds { get; set; } = new();
}
