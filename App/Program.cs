using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connectionString =
    $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
    $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
    $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
    $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
    $"Password={Environment.GetEnvironmentVariable("DB_PASS")};";

app.MapGet("/", async () =>
{
    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT message FROM greetings LIMIT 1", conn);
        var greeting = (string?)await cmd.ExecuteScalarAsync();

        return Results.Ok(new
        {
            type = "dotnet",
            greeting = greeting ?? "Hello from Zerops!",
            status = new { database = "OK" }
        });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new
            {
                type = "dotnet",
                greeting = (string?)null,
                status = new { database = $"ERROR: {ex.Message}" }
            },
            statusCode: 503);
    }
});

app.Run();
