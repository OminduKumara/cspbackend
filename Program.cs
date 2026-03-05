using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

// Use AZURE_SQL_CONNECTIONSTRING environment variable if set, otherwise fall back to config
var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' or environment variable 'AZURE_SQL_CONNECTIONSTRING' is not configured.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
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

// Skip database startup tasks to ensure app starts quickly
// Database will be created on first API call if needed

// Enable OpenAPI documentation for testing
app.MapOpenApi();
app.MapScalarApiReference();

app.UseCors("AllowLocalhost");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();