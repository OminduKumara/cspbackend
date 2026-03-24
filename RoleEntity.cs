namespace tmsserver.Models;

public class RoleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PermissionsJson { get; set; }  // JSON array of permissions
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<string> GetPermissions()
    {
        if (string.IsNullOrEmpty(PermissionsJson))
            return new List<string>();
        
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(PermissionsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
