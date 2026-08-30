using Adesha.Api.Auth;
using Adesha.Api.Configuration;
using Adesha.Api.Middleware;
using Adesha.Api.SystemStatus;
using Adesha.Application.Auditing;
using Adesha.Application.Configuration;
using Adesha.Infrastructure.Auditing;
using Adesha.Infrastructure.Identity;
using Adesha.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Aspire's WithReference injects "ConnectionStrings:adesha". We use explicit
// Npgsql registration so test factories and the AppHost both control the string
// from config without taking a hidden runtime dependency path.
builder.Services.AddDbContext<AdeshaDbContext>((sp, options) =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("adesha")
        ?? throw new InvalidOperationException("Connection string 'adesha' is missing.");
    options.UseNpgsql(cs);
    options.AddInterceptors(new AppendOnlyAuditInterceptor());
});

builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(
        sp.GetRequiredService<IConfiguration>().GetConnectionString("adesha-redis")
            ?? throw new InvalidOperationException("Connection string 'adesha-redis' is missing.")));

builder.Services.AddOptions<AdeshaOptions>()
    .BindConfiguration(AdeshaOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AdeshaOptions>, AdeshaOptionsValidator>();

builder.Services.AddIdentityCore<AdeshaUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<AdeshaDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
builder.Services.AddAuthorization();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IAuditWriter, EfAuditWriter>();

builder.Services.AddOpenApi();
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    // API consumers send camelCase JSON from Angular and curl; accept both.
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// Rule 2: log the active trading mode at startup, every startup.
var tradingMode = app.Services.GetRequiredService<IOptions<AdeshaOptions>>().Value.TradingMode;
app.Logger.LogInformation("Adesha starting with TradingMode={TradingMode}", tradingMode);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Dev-only convenience; production migration strategy is decided in Work Order 6.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdeshaDbContext>().Database.MigrateAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapSystemEndpoints();

app.Run();

public partial class Program;
