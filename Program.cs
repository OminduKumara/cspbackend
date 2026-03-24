using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;
using Scalar.AspNetCore;
using tmsserver.Data;
using tmsserver.Data.Repositories;
using tmsserver.Services;


// Load .env file for environment variables
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers();
 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Use AZURE_SQL_CONNECTIONSTRING environment variable
var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
}

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization(options =>
{
    // Policy: Only Admins can manage users
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("SystemAdmin", "Admin"));

    // Policy: Admin or System Admin can approve registrations
    options.AddPolicy("ApproveRegistrations", policy =>
        policy.RequireRole("SystemAdmin", "Admin"));

    // Policy: Only approved players
    options.AddPolicy("ApprovedPlayersOnly", policy =>
        policy.RequireRole("SystemAdmin", "Admin", "Player"));
});

// Add repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRegistrationRequestRepository, RegistrationRequestRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<ITournamentTeamRepository, TournamentTeamRepository>();
builder.Services.AddScoped<ITournamentMatchRepository, TournamentMatchRepository>();
builder.Services.AddScoped<IMatchScoreRepository, MatchScoreRepository>();
builder.Services.AddScoped<ILiveGameScoreRepository, LiveGameScoreRepository>();

// Add services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", builder =>
    {
        builder.WithOrigins(
                "http://localhost:5173", 
                "http://localhost:3000",
                "https://csp-group-4.vercel.app"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

await InitializeDatabaseAsync(connectionString);

app.MapOpenApi();
app.MapScalarApiReference();

app.UseCors("AllowLocalhost");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void LoadDotEnv()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(AppContext.BaseDirectory, ".env")
    };

    var envPath = candidates.FirstOrDefault(File.Exists);
    if (string.IsNullOrWhiteSpace(envPath))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
        {
            value = value[1..^1];
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static async Task InitializeDatabaseAsync(string connectionString)
{
    try
    {
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    IF OBJECT_ID('dbo.Tournaments', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.Tournaments (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            Name VARCHAR(100) NOT NULL,
                            Description VARCHAR(500),
                            Status TINYINT NOT NULL DEFAULT 0,
                            StartDate DATETIME NOT NULL,
                            EndDate DATETIME NOT NULL,
                            CreatedByAdminId INT NOT NULL,
                            CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME NULL,
                            UpdatedByAdminId INT NULL,
                            CONSTRAINT FK_Tournaments_CreatedByAdmin FOREIGN KEY (CreatedByAdminId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
                            CONSTRAINT FK_Tournaments_UpdatedByAdmin FOREIGN KEY (UpdatedByAdminId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
                        );
                        CREATE INDEX idx_tournaments_status ON dbo.Tournaments(Status);
                        CREATE INDEX idx_tournaments_dates ON dbo.Tournaments(StartDate, EndDate);
                    END;

                    IF OBJECT_ID('dbo.Groups', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.Groups (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            TournamentId INT NOT NULL,
                            GroupName VARCHAR(50) NOT NULL,
                            BracketPosition INT NULL,
                            CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME NULL,
                            CONSTRAINT FK_Groups_Tournament FOREIGN KEY (TournamentId) REFERENCES dbo.Tournaments(Id) ON DELETE CASCADE
                        );
                        CREATE INDEX idx_groups_tournament ON dbo.Groups(TournamentId);
                        CREATE INDEX idx_groups_bracket ON dbo.Groups(BracketPosition);
                    END;

                    IF OBJECT_ID('dbo.GroupPlayers', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.GroupPlayers (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            GroupId INT NOT NULL,
                            PlayerId INT NOT NULL,
                            AssignedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_GroupPlayers_Group FOREIGN KEY (GroupId) REFERENCES dbo.Groups(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_GroupPlayers_Player FOREIGN KEY (PlayerId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
                            CONSTRAINT UQ_GroupPlayers UNIQUE(GroupId, PlayerId)
                        );
                        CREATE INDEX idx_groupplayers_group ON dbo.GroupPlayers(GroupId);
                        CREATE INDEX idx_groupplayers_player ON dbo.GroupPlayers(PlayerId);
                    END;

                    IF OBJECT_ID('dbo.TournamentTeams', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.TournamentTeams (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            TournamentId INT NOT NULL,
                            TeamName VARCHAR(255) NOT NULL,
                            TeamOrder INT NOT NULL DEFAULT 0,
                            CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME NULL,
                            CONSTRAINT FK_TournamentTeams_Tournament FOREIGN KEY (TournamentId) REFERENCES dbo.Tournaments(Id) ON DELETE CASCADE,
                            CONSTRAINT UQ_TournamentTeams_Name UNIQUE(TournamentId, TeamName)
                        );
                        CREATE INDEX idx_tournament_teams_tournament ON dbo.TournamentTeams(TournamentId);
                        CREATE INDEX idx_tournament_teams_order ON dbo.TournamentTeams(TournamentId, TeamOrder);
                    END;

                    IF OBJECT_ID('dbo.TournamentMatches', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.TournamentMatches (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            TournamentId INT NOT NULL,
                            Team1Id INT NOT NULL,
                            Team2Id INT NOT NULL,
                            WinnerId INT NULL,
                            IsPlayoff BIT NOT NULL DEFAULT 0,
                            CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME NULL,
                            CONSTRAINT FK_TournamentMatches_Tournament FOREIGN KEY (TournamentId) REFERENCES dbo.Tournaments(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_TournamentMatches_Team1 FOREIGN KEY (Team1Id) REFERENCES dbo.TournamentTeams(Id) ON DELETE NO ACTION,
                            CONSTRAINT FK_TournamentMatches_Team2 FOREIGN KEY (Team2Id) REFERENCES dbo.TournamentTeams(Id) ON DELETE NO ACTION,
                            CONSTRAINT FK_TournamentMatches_Winner FOREIGN KEY (WinnerId) REFERENCES dbo.TournamentTeams(Id) ON DELETE NO ACTION
                        );
                        CREATE INDEX idx_tournament_matches_tournament ON dbo.TournamentMatches(TournamentId);
                        CREATE INDEX idx_tournament_matches_teams ON dbo.TournamentMatches(Team1Id, Team2Id);
                        CREATE INDEX idx_tournament_matches_winner ON dbo.TournamentMatches(WinnerId);
                        CREATE INDEX idx_tournament_matches_playoff ON dbo.TournamentMatches(IsPlayoff);
                    END;

                    IF OBJECT_ID('dbo.MatchScores', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.MatchScores (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            MatchId INT NOT NULL,
                            SetNumber INT NOT NULL,
                            Team1Games INT NOT NULL DEFAULT 0,
                            Team2Games INT NOT NULL DEFAULT 0,
                            Team1TieBreak INT NULL,
                            Team2TieBreak INT NULL,
                            CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            UpdatedAt DATETIME NULL,
                            CONSTRAINT FK_MatchScores_Match FOREIGN KEY (MatchId) REFERENCES dbo.TournamentMatches(Id) ON DELETE CASCADE,
                            CONSTRAINT UQ_MatchScores_Set UNIQUE(MatchId, SetNumber)
                        );
                        CREATE INDEX idx_match_scores_match ON dbo.MatchScores(MatchId);
                    END;

                    IF OBJECT_ID('dbo.LiveGameScores', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.LiveGameScores (
                            MatchId INT PRIMARY KEY,
                            Team1Points VARCHAR(10) NOT NULL DEFAULT '0',
                            Team2Points VARCHAR(10) NOT NULL DEFAULT '0',
                            ServingTeamId INT NULL,
                            UpdatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_LiveGameScores_Match FOREIGN KEY (MatchId) REFERENCES dbo.TournamentMatches(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_LiveGameScores_ServingTeam FOREIGN KEY (ServingTeamId) REFERENCES dbo.TournamentTeams(Id) ON DELETE NO ACTION
                        );
                    END;

                    IF OBJECT_ID('dbo.Inventory', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.Inventory (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Name NVARCHAR(100) NOT NULL,
                            Description NVARCHAR(255),
                            Quantity INT NOT NULL,
                            Category NVARCHAR(100),
                            CreatedAt DATETIME NOT NULL,
                            UpdatedAt DATETIME NULL,
                            Condition NVARCHAR(100) NULL
                        );
                    END
                    ELSE
                    BEGIN
                        IF COL_LENGTH('dbo.Inventory', 'Condition') IS NULL
                        BEGIN
                            ALTER TABLE dbo.Inventory ADD Condition NVARCHAR(100) NULL;
                        END
                    END;

                    IF OBJECT_ID('dbo.InventoryTransaction', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.InventoryTransaction (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            InventoryItemId INT NOT NULL,
                            IssuedToUserId INT NULL,
                            QuantityChanged INT NOT NULL,
                            Comment NVARCHAR(255),
                            Timestamp DATETIME NOT NULL,
                            PerformedByAdminId INT NULL,
                            CONSTRAINT FK_InventoryTransaction_Inventory FOREIGN KEY (InventoryItemId) REFERENCES dbo.Inventory(Id),
                            CONSTRAINT FK_InventoryTransaction_IssuedToUser FOREIGN KEY (IssuedToUserId) REFERENCES dbo.Users(Id),
                            CONSTRAINT FK_InventoryTransaction_PerformedByAdmin FOREIGN KEY (PerformedByAdminId) REFERENCES dbo.Users(Id)
                        );
                    END

                    IF OBJECT_ID('dbo.PracticeSessions', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.PracticeSessions (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            DayOfWeek NVARCHAR(20) NOT NULL,
                            StartTime NVARCHAR(20) NOT NULL,
                            EndTime NVARCHAR(20) NOT NULL,
                            SessionType NVARCHAR(100) NOT NULL
                        );
                    END;

                    IF OBJECT_ID('dbo.PracticeAttendance', 'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.PracticeAttendance (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            PracticeSessionId INT NOT NULL,
                            PlayerId INT NOT NULL,
                            AttendanceDate DATE NOT NULL,
                            IsPresent BIT NOT NULL DEFAULT 0,
                            MarkedByAdminId INT NULL,
                            MarkedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_PracticeAttendance_Session FOREIGN KEY (PracticeSessionId) REFERENCES dbo.PracticeSessions(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_PracticeAttendance_Player FOREIGN KEY (PlayerId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_PracticeAttendance_Admin FOREIGN KEY (MarkedByAdminId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
                            CONSTRAINT UQ_PracticeAttendance UNIQUE(PracticeSessionId, PlayerId, AttendanceDate)
                        );
                        CREATE INDEX idx_practiceattendance_session_date ON dbo.PracticeAttendance(PracticeSessionId, AttendanceDate);
                        CREATE INDEX idx_practiceattendance_player_date ON dbo.PracticeAttendance(PlayerId, AttendanceDate);
                    END;
                    
                    IF COL_LENGTH('dbo.Users', 'ContactNumber') IS NULL
                    BEGIN
                        ALTER TABLE dbo.Users ADD ContactNumber NVARCHAR(30) NULL;
                    END;

                    IF COL_LENGTH('dbo.Users', 'Address') IS NULL
                    BEGIN
                        ALTER TABLE dbo.Users ADD Address NVARCHAR(255) NULL;
                    END;
                ";
                await command.ExecuteNonQueryAsync();
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization warning: {ex.Message}");
    }
}