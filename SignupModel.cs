namespace tmsserver.Models;

public class SignupModel
{
    public string Username { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;  // e.g., it23575776
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
