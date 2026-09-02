using Adesha.Api.Auth;
using Adesha.Api.Broker;
using Adesha.Api.Configuration;
using Adesha.Api.Middleware;
using Adesha.Api.SystemStatus;
using Adesha.Application.Auditing;
using Adesha.Application.Brokers;
using Adesha.Application.Configuration;
using Adesha.Brokers.Abstractions;
using Adesha.Brokers.MStock;
using Adesha.Infrastructure.Auditing;
using Adesha.Infrastructure.Brokers;
using Adesha.Infrastructure.Identity;
using Adesha.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
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

// Broker: m.Stock adapter with Rule 11-compliant resilience (read-only in WO2).
// The API key comes from User Secrets / Aspire parameters; the adapter is registered
// as IBrokerAdapter so the application layer never depends on the concrete type.
builder.Services.AddMStockBroker(builder.Configuration);
builder.Services.AddMStockBrokerAdapter();

// Broker session store: Redis-backed, with TTL matching session expiry.
builder.Services.AddSingleton<IBrokerSessionStore, RedisBrokerSessionStore>();
builder.Services.AddSingleton<IBrokerLoginStateStore, RedisBrokerLoginStateStore>();

// Instrument master service: fetches, versions, and caches the broker instrument
// list in Redis with stable InstrumentId mapping across daily refreshes.
builder.Services.AddSingleton<IInstrumentMasterService, InstrumentMasterService>();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("adesha-redis")
        ?? throw new InvalidOperationException("Connection string 'adesha-redis' is missing.");
    options.InstanceName = "adesha:";
});

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
app.MapBrokerEndpoints();
app.MapSystemEndpoints();

app.Run();

public partial class Program;
