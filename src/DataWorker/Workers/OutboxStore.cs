using Microsoft.Data.Sqlite;

namespace DataWorker.Workers;

public sealed class OutboxStore
{
    private readonly string _connectionString;
    private readonly ILogger<OutboxStore> _logger;

    public OutboxStore(IConfiguration config, ILogger<OutboxStore> logger)
    {
        string dbPath = config["OUTBOX_DB_PATH"] ?? "outbox.db";
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;

        this.InitialiseSchema();
    }

    private void InitialiseSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS outbox (
                Id        TEXT NOT NULL PRIMARY KEY,
                Payload   TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                SentAt    TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_outbox_unsent ON outbox (SentAt)
                WHERE SentAt IS NULL;
            """;
      
        cmd.ExecuteNonQuery();
        _logger.LogInformation("Outbox schema ready at {CS}", _connectionString);
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Insert(string id, string payload)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO outbox (Id, Payload, CreatedAt)
            VALUES ($id, $payload, $createdAt);
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$payload", payload);
        cmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<OutboxEntry> GetUnsent()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Payload, CreatedAt
            FROM   outbox
            WHERE  SentAt IS NULL
            ORDER  BY CreatedAt ASC;
            """;

        var entries = new List<OutboxEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new OutboxEntry(
                Id: reader.GetString(0),
                Payload: reader.GetString(1),
                CreatedAt: reader.GetString(2)
            ));
        }
        return entries;
    }

    public void MarkSent(string id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE outbox
            SET    SentAt = $sentAt
            WHERE  Id     = $id;
            """;
        cmd.Parameters.AddWithValue("$sentAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}

public record OutboxEntry(string Id, string Payload, string CreatedAt);