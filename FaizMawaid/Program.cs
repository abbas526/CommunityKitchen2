using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Repositories;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Dapper: our IsActive-style TINYINT UNSIGNED columns come back as a raw numeric type,
// not bool, now that the deprecated TINYINT(1) display width isn't used -- this handler
// lets model properties stay a clean `bool` regardless. Registered once, globally.
SqlMapper.AddTypeHandler(new BooleanTypeHandler());
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // Serialize enums (RegistrationStatus, ThaaliCancellationStatus, ...) as their
        // string names ("Pending", "Active", ...) instead of raw numbers -- much easier
        // for the wwwroot UI (and anyone poking at Swagger) to work with.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data access
builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();

// Repositories -- controllers depend only on these interfaces, never on Dapper/SQL directly.
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IThaaliSizeRepository, ThaaliSizeRepository>();
builder.Services.AddScoped<IFamilyRepository, FamilyRepository>();
builder.Services.AddScoped<IFamilySizeHistoryRepository, FamilySizeHistoryRepository>();
builder.Services.AddScoped<INonServingDayRepository, NonServingDayRepository>();
builder.Services.AddScoped<IThaaliCancellationRepository, ThaaliCancellationRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IMealPlanRepository, MealPlanRepository>();
builder.Services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IMealPlanTemplateRepository, MealPlanTemplateRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IAreaRepository, AreaRepository>();
builder.Services.AddScoped<IDeliveryPersonRepository, DeliveryPersonRepository>();
builder.Services.AddScoped<IAddressChangeRequestRepository, AddressChangeRequestRepository>();
builder.Services.AddScoped<IThaaliSizeChangeRequestRepository, ThaaliSizeChangeRequestRepository>();

// Auth services (2026-09-24) -- see Services/PasswordHasherService.cs and Services/TokenService.cs.
builder.Services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Token-based authentication. IMPORTANT: Jwt:Key in appsettings.json is a development
// placeholder -- it MUST be replaced with a real secret (an environment variable or
// `dotnet user-secrets`, never committed to source control) before this is deployed
// anywhere reachable by the public. See CLAUDE.md.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured (see appsettings.json's Jwt section).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FaizMawaidCommunityKitchen";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? jwtIssuer;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();   // makes "/" load index.html
app.UseStaticFiles();    // serves files from wwwroot

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
