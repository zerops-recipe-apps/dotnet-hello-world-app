using Npgsql;

var connectionString =
    $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
    $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
    $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
    $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
    $"Password={Environment.GetEnvironmentVariable("DB_PASS")};";

Console.WriteLine("Connecting to database...");

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

Console.WriteLine("Running migrations...");

await using var cmd = conn.CreateCommand();
cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS greetings (
        id      INTEGER PRIMARY KEY,
        message TEXT    NOT NULL
    );
    INSERT INTO greetings (id, message)
        VALUES (1, 'Hello from Zerops!')
        ON CONFLICT (id) DO NOTHING;";
await cmd.ExecuteNonQueryAsync();

Console.WriteLine("Migration complete.");
