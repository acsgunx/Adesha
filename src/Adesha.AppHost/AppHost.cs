var builder = DistributedApplication.CreateBuilder(args);

// Persistent data volume: migrations and seeded data survive restarts.
var postgres = builder.AddPostgres("adesha-db")
    .WithDataVolume("adesha-db-data")
    .WithLifetime(ContainerLifetime.Persistent);

var adeshaDb = postgres.AddDatabase("adesha");

var redis = builder.AddRedis("adesha-redis")
    .WithLifetime(ContainerLifetime.Persistent);

// JWT signing key: generated once, persisted to AppHost user secrets — never in the repo
// (Rule 3). `aspire run` on a clean clone works without manual secret setup.
var jwtSigningKey = builder.AddParameter(
    "jwt-signing-key",
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true,
    persist: true);

var api = builder.AddProject<Projects.Adesha_Api>("adesha-api")
    .WithReference(adeshaDb)
    .WithReference(redis)
    .WithEnvironment("Adesha__Jwt__SigningKey", jwtSigningKey)
    .WaitFor(adeshaDb)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("adesha-web", "../Adesha.Web", "start")
    .WithHttpEndpoint(targetPort: 4200)
    .WithEnvironment("ADESHA_API_URL", api.GetEndpoint("http"))
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
