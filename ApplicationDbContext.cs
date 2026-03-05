using Microsoft.EntityFrameworkCore;
using tmsserver.Models;

namespace tmsserver.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<RegistrationRequest> RegistrationRequests { get; set; }
    public DbSet<RoleEntity> Roles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.IdentityNumber)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Configure RegistrationRequest
        modelBuilder.Entity<RegistrationRequest>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistrationRequest>()
            .HasOne(r => r.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed initial admin users
        var systemAdminPasswordHash = HashPassword("admin123");
        var admin1PasswordHash = HashPassword("admin123");
        var admin2PasswordHash = HashPassword("admin123");
        var seedDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "systemadmin",
                IdentityNumber = "IT0001",
                Email = "admin@sliit.lk",
                PasswordHash = systemAdminPasswordHash,
                Role = UserRole.SystemAdmin,
                IsApproved = true,
                CreatedAt = seedDate,
                ApprovedAt = seedDate
            },
            new User
            {
                Id = 2,
                Username = "admin1",
                IdentityNumber = "AD0001",
                Email = "admin1@sliit.lk",
                PasswordHash = admin1PasswordHash,
                Role = UserRole.Admin,
                IsApproved = true,
                CreatedAt = seedDate,
                ApprovedAt = seedDate
            },
            new User
            {
                Id = 3,
                Username = "admin2",
                IdentityNumber = "AD0002",
                Email = "admin2@sliit.lk",
                PasswordHash = admin2PasswordHash,
                Role = UserRole.Admin,
                IsApproved = true,
                CreatedAt = seedDate,
                ApprovedAt = seedDate
            }
        );

        // Seed default roles
        modelBuilder.Entity<RoleEntity>(eb =>
        {
            eb.HasKey(r => r.Id);
            eb.Property(r => r.Name).IsRequired();
        });

        modelBuilder.Entity<RoleEntity>().HasData(
            new RoleEntity { Id = 1, Name = "SystemAdmin", Description = "System administrator with full access", PermissionsJson = "[\"*\"]", CreatedAt = seedDate },
            new RoleEntity { Id = 2, Name = "Admin", Description = "Administrator with management access", PermissionsJson = "[\"manage_users\",\"manage_players\",\"approve_registrations\",\"view_reports\"]", CreatedAt = seedDate },
            new RoleEntity { Id = 3, Name = "Player", Description = "Tournament player", PermissionsJson = "[\"view_tournaments\",\"register_tournament\",\"view_results\"]", CreatedAt = seedDate },
            new RoleEntity { Id = 4, Name = "PendingPlayer", Description = "Player awaiting approval", PermissionsJson = "[]", CreatedAt = seedDate }
        );
    }

    private static string HashPassword(string password)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
