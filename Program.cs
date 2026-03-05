using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Data;
using Scalar.AspNetCore;
using tmsserver.Data;
using tmsserver.Data.Repositories;
using tmsserver.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Use AZURE_MYSQL_CONNECTIONSTRING environment variable if set, otherwise fall back to config
var connectionString = Environment.GetEnvironmentVariable("AZURE_MYSQL_CONNECTIONSTRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' or environment variable 'AZURE_MYSQL_CONNECTIONSTRING' is not configured.");
}

var databaseServerVersion = builder.Configuration["Database:ServerVersion"] ?? "8.0.36";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(databaseServerVersion)))
);

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

// Apply EF Core migrations at startup (ensures schema is created/updated)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupMigration");

    bool tableExists(string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    try
    {
        var schemaAlreadyExists = tableExists("Users") || tableExists("users") ||
                                  tableExists("Roles") || tableExists("roles");

        if (schemaAlreadyExists)
        {
            logger.LogWarning("Existing database tables detected. Skipping automatic migration to avoid duplicate table creation.");
        }
        else
        {
            dbContext.Database.Migrate();
        }
    }
    catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning(ex, "Table(s) already exist. Continuing startup without applying migrations.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database migration skipped because the database is unavailable during startup. The application will continue to start.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowLocalhost");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();