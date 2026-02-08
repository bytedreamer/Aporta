using System.Data;
using System.Text.Json;
using Dapper;
using Google.Protobuf;
using Microsoft.Data.Sqlite;
using Z9.Spcore.Proto;

namespace Aporta.Migration;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Aporta Database Migration Tool");
            Console.WriteLine("Migrates from column-based schema to JSON document storage.");
            Console.WriteLine();
            Console.WriteLine("Usage: Aporta.Migration <database-path> [--dry-run]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --dry-run    Show what would be done without making changes");
            return 1;
        }

        var dbPath = args[0];
        var dryRun = args.Contains("--dry-run");

        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Error: Database file not found: {dbPath}");
            return 1;
        }

        // Create backup
        var backupPath = $"{dbPath}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
        if (!dryRun)
        {
            Console.WriteLine($"Creating backup: {backupPath}");
            File.Copy(dbPath, backupPath);
        }

        try
        {
            var connectionString = $"Data Source={dbPath}";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var migrator = new Migrator(connection, dryRun);
            await migrator.MigrateAsync();

            Console.WriteLine();
            Console.WriteLine(dryRun ? "Dry run complete. No changes made." : "Migration complete!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during migration: {ex.Message}");
            Console.WriteLine();
            if (!dryRun)
            {
                Console.WriteLine($"Restoring from backup: {backupPath}");
                File.Copy(backupPath, dbPath, overwrite: true);
            }
            return 1;
        }
    }
}

public class Migrator
{
    private const int OldSchemaVersion = 10;

    private readonly IDbConnection _connection;
    private readonly bool _dryRun;

    public Migrator(IDbConnection connection, bool dryRun)
    {
        _connection = connection;
        _dryRun = dryRun;
    }

    public async Task MigrateAsync()
    {
        var currentVersion = await GetCurrentVersionAsync();
        Console.WriteLine($"Current schema version: {currentVersion}");

        if (currentVersion == -1)
        {
            Console.WriteLine("Error: No schema_info table found. Is this an Aporta database?");
            throw new Exception("Invalid database");
        }

        // Check if already migrated to JSON schema
        if (await IsJsonSchemaAsync())
        {
            Console.WriteLine("Database is already using JSON schema. Nothing to do.");
            return;
        }

        // Step 1: Apply old migrations to get to version 10
        if (currentVersion < OldSchemaVersion)
        {
            Console.WriteLine($"Applying old migrations to reach version {OldSchemaVersion}...");
            await ApplyOldMigrationsAsync(currentVersion);
        }

        // Step 2: Transform to JSON schema
        Console.WriteLine("Transforming to JSON document storage...");
        await TransformToJsonSchemaAsync();
    }

    private async Task<int> GetCurrentVersionAsync()
    {
        var tableExists = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_info'");

        if (tableExists == 0) return -1;

        return await _connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COALESCE(MAX(id), -1) FROM schema_info");
    }

    private async Task<bool> IsJsonSchemaAsync()
    {
        // Check if person table has 'data' column (JSON schema) vs 'first_name' column (old schema)
        var columns = await _connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('person')");
        return columns.Contains("data");
    }

    private async Task ApplyOldMigrationsAsync(int currentVersion)
    {
        var migrations = GetOldMigrations();

        foreach (var (version, name, sql) in migrations.Where(m => m.Version > currentVersion))
        {
            Console.WriteLine($"  Applying migration {version}: {name}");
            if (!_dryRun)
            {
                using var transaction = _connection.BeginTransaction();
                await _connection.ExecuteAsync(sql, transaction: transaction);
                await _connection.ExecuteAsync(
                    "INSERT INTO schema_info (id, name, timestamp) VALUES (@id, @name, @timestamp)",
                    new { id = version, name, timestamp = DateTime.UtcNow },
                    transaction);
                transaction.Commit();
            }
        }
    }

    private async Task TransformToJsonSchemaAsync()
    {
        using var transaction = _dryRun ? null : _connection.BeginTransaction();

        // Transform each table
        await TransformExtensionTableAsync(transaction);
        await TransformEndpointTableAsync(transaction);
        await TransformOutputTableAsync(transaction);
        await TransformInputTableAsync(transaction);
        await TransformDoorTableAsync(transaction);
        await TransformGlobalSettingTableAsync(transaction);
        await TransformPersonTableAsync(transaction);
        await TransformCredentialTableAsync(transaction);
        await TransformCredentialAssignmentTableAsync(transaction);
        await TransformEventTableAsync(transaction);

        // Update schema_info
        if (!_dryRun)
        {
            // Clear old schema records and insert new (version 100 = JSON schema)
            await _connection.ExecuteAsync("DELETE FROM schema_info", transaction: transaction);
            await _connection.ExecuteAsync(
                "INSERT INTO schema_info (id, name, timestamp) VALUES (@id, @name, @timestamp)",
                new { id = 100, name = "Initial create with JSON document storage", timestamp = DateTime.UtcNow },
                transaction: transaction);
        }

        transaction?.Commit();
    }

    private async Task TransformExtensionTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming extension table...");
        if (_dryRun) return;

        // extension: id (text), name, enabled, configuration -> id (text), data (json)
        var oldData = await _connection.QueryAsync<(string Id, string Name, int Enabled, string Configuration)>(
            "SELECT id, name, enabled, configuration FROM extension", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE extension_new (id TEXT NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                name = row.Name,
                enabled = row.Enabled != 0,
                configuration = row.Configuration
            });
            await _connection.ExecuteAsync(
                "INSERT INTO extension_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE extension", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE extension_new RENAME TO extension", transaction: transaction);
    }

    private async Task TransformEndpointTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming endpoint table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, string Name, string DriverId, int EndpointType, string ExtensionId)>(
            "SELECT id, name, driver_id, endpoint_type, extension_id FROM endpoint", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE endpoint_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                name = row.Name,
                driverEndpointId = row.DriverId,
                type = row.EndpointType,
                extensionId = row.ExtensionId
            });
            await _connection.ExecuteAsync(
                "INSERT INTO endpoint_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE endpoint", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE endpoint_new RENAME TO endpoint", transaction: transaction);
    }

    private async Task TransformOutputTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming output table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, int EndpointId, string Name)>(
            "SELECT id, endpoint_id, name FROM output", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE output_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                endpointId = row.EndpointId,
                name = row.Name
            });
            await _connection.ExecuteAsync(
                "INSERT INTO output_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE output", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE output_new RENAME TO output", transaction: transaction);
    }

    private async Task TransformInputTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming input table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, int EndpointId, string Name)>(
            "SELECT id, endpoint_id, name FROM input", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE input_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                endpointId = row.EndpointId,
                name = row.Name
            });
            await _connection.ExecuteAsync(
                "INSERT INTO input_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE input", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE input_new RENAME TO input", transaction: transaction);
    }

    private async Task TransformDoorTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming door table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, int? InAccessEndpointId, int? OutAccessEndpointId,
            int? DoorContactEndpointId, int? RequestToExitEndpointId, int? DoorStrikeEndpointId, string Name)>(
            @"SELECT id, in_access_endpoint_id, out_access_endpoint_id, door_contact_endpoint_id,
              request_to_exit_endpoint_id, door_strike_endpoint_id, name FROM door", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE door_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                name = row.Name,
                inAccessEndpointId = row.InAccessEndpointId,
                outAccessEndpointId = row.OutAccessEndpointId,
                doorContactEndpointId = row.DoorContactEndpointId,
                requestToExitEndpointId = row.RequestToExitEndpointId,
                doorStrikeEndpointId = row.DoorStrikeEndpointId
            });
            await _connection.ExecuteAsync(
                "INSERT INTO door_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE door", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE door_new RENAME TO door", transaction: transaction);
    }

    private async Task TransformGlobalSettingTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming global_setting table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(string Name, string Value)>(
            "SELECT name, value FROM global_setting", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE global_setting_new (name TEXT NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new { value = row.Value });
            await _connection.ExecuteAsync(
                "INSERT INTO global_setting_new (name, data) VALUES (@name, @data)",
                new { name = row.Name, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE global_setting", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE global_setting_new RENAME TO global_setting", transaction: transaction);
    }

    private async Task TransformPersonTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming person table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, string? FirstName, string? LastName, int Enabled)>(
            "SELECT id, first_name, last_name, enabled FROM person", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE person_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                firstName = row.FirstName,
                lastName = row.LastName,
                enabled = row.Enabled != 0
            });
            await _connection.ExecuteAsync(
                "INSERT INTO person_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE person", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE person_new RENAME TO person", transaction: transaction);
    }

    private async Task TransformCredentialTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming credential table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, string Number, int? LastEvent)>(
            "SELECT id, number, last_event FROM credential", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE credential_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                number = row.Number,
                lastEvent = row.LastEvent
            });
            await _connection.ExecuteAsync(
                "INSERT INTO credential_new (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE credential", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE credential_new RENAME TO credential", transaction: transaction);
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX credential_number_uindex ON credential (json_extract(data, '$.number'))",
            transaction: transaction);
    }

    private async Task TransformCredentialAssignmentTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Transforming credential_assignment table...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int PersonId, int CredentialId, int Enabled)>(
            "SELECT person_id, credential_id, enabled FROM credential_assignment", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE credential_assignment_new (id INTEGER NOT NULL PRIMARY KEY, data TEXT NOT NULL)",
            transaction: transaction);

        var id = 1;
        foreach (var row in oldData)
        {
            var json = JsonSerializer.Serialize(new
            {
                credentialId = row.CredentialId,
                personId = row.PersonId,
                enabled = row.Enabled
            });
            await _connection.ExecuteAsync(
                "INSERT INTO credential_assignment_new (id, data) VALUES (@id, @data)",
                new { id = id++, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE credential_assignment", transaction: transaction);
        await _connection.ExecuteAsync("ALTER TABLE credential_assignment_new RENAME TO credential_assignment", transaction: transaction);
        await _connection.ExecuteAsync(
            "CREATE INDEX idx_ca_credential ON credential_assignment (json_extract(data, '$.credentialId'))",
            transaction: transaction);
        await _connection.ExecuteAsync(
            "CREATE INDEX idx_ca_person ON credential_assignment (json_extract(data, '$.personId'))",
            transaction: transaction);
    }

    private static readonly JsonFormatter ProtoFormatter = new(JsonFormatter.Settings.Default);

    private async Task TransformEventTableAsync(IDbTransaction? transaction)
    {
        Console.WriteLine("  Migrating event table to z9_evt...");
        if (_dryRun) return;

        var oldData = await _connection.QueryAsync<(int Id, int EndpointId, DateTime Timestamp, int EventType, string Data)>(
            "SELECT id, endpoint_id, timestamp, event_type, data FROM event", transaction: transaction);

        await _connection.ExecuteAsync(
            @"CREATE TABLE z9_evt (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, data TEXT NOT NULL)",
            transaction: transaction);

        foreach (var row in oldData)
        {
            var millis = new DateTimeOffset(row.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var isGranted = row.EventType == 0; // EventType.AccessGranted = 0

            var evt = new Evt
            {
                EvtCode = isGranted ? EvtCode.DoorAccessGranted : EvtCode.DoorAccessDenied,
                HwTime = new DateTimeData { Millis = millis },
                DbTime = new DateTimeData { Millis = millis },
                Consumed = false,
                Priority = 0,
                Data = row.Data
            };

            // Parse EventData to get EventReason for denied events
            if (!isGranted && !string.IsNullOrEmpty(row.Data))
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.Data);
                    if (doc.RootElement.TryGetProperty("EventReason", out var reasonProp) ||
                        doc.RootElement.TryGetProperty("eventReason", out reasonProp))
                    {
                        var reasonInt = reasonProp.GetInt32();
                        var subCode = reasonInt switch
                        {
                            4 => EvtSubCode.AccessDeniedUnknownCredNum,     // CredentialNotEnrolled
                            11 => EvtSubCode.AccessDeniedUnknownCredNumFormat, // NoCredentialTemplate
                            5 => EvtSubCode.AccessDeniedInactive,            // CredentialDisabled
                            6 => EvtSubCode.AccessDeniedNotEffective,        // CredentialNotYetEffective
                            7 => EvtSubCode.AccessDeniedExpired,             // CredentialExpired
                            8 => EvtSubCode.AccessDeniedNoPriv,              // NoPrivilege
                            3 => EvtSubCode.AccessDeniedNoPriv,              // AccessNotAssigned
                            9 => EvtSubCode.AccessDeniedOutsideSched,        // OutsideSchedule
                            10 => EvtSubCode.AccessDeniedDoorModeStaticLocked, // DoorLocked
                            _ => (EvtSubCode?)null
                        };
                        if (subCode.HasValue)
                            evt.EvtSubCode = subCode.Value;
                    }
                }
                catch (JsonException)
                {
                    // Old data may not be parseable — leave without sub-code
                }
            }

            var json = ProtoFormatter.Format(evt);
            await _connection.ExecuteAsync(
                "INSERT INTO z9_evt (id, data) VALUES (@id, @data)",
                new { id = row.Id, data = json },
                transaction: transaction);
        }

        await _connection.ExecuteAsync("DROP TABLE event", transaction: transaction);
    }

    private static List<(int Version, string Name, string Sql)> GetOldMigrations()
    {
        return new List<(int, string, string)>
        {
            (1, "Add extension table", @"
                CREATE TABLE extension (
                    id TEXT NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    enabled INTEGER DEFAULT 0 NOT NULL,
                    configuration TEXT NOT NULL
                );
                CREATE UNIQUE INDEX extension_id_uindex ON extension (id);"),

            (2, "Add endpoint table", @"
                CREATE TABLE endpoint (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    driver_id TEXT NOT NULL,
                    endpoint_type INTEGER NOT NULL,
                    extension_id TEXT NOT NULL REFERENCES extension ON UPDATE CASCADE ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX endpoint_id_uindex ON endpoint (id);"),

            (3, "Add output table", @"
                CREATE TABLE output (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    endpoint_id INTEGER NOT NULL REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    name TEXT NOT NULL
                );
                CREATE UNIQUE INDEX output_id_uindex ON output (id);"),

            (4, "Add input table", @"
                CREATE TABLE input (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    endpoint_id INTEGER NOT NULL REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    name TEXT NOT NULL
                );
                CREATE UNIQUE INDEX input_id_uindex ON input (id);"),

            (5, "Add door table", @"
                CREATE TABLE door (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    in_access_endpoint_id INTEGER REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    out_access_endpoint_id INTEGER REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    door_contact_endpoint_id INTEGER REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    request_to_exit_endpoint_id INTEGER REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    door_strike_endpoint_id INTEGER REFERENCES endpoint ON UPDATE CASCADE ON DELETE CASCADE,
                    name TEXT NOT NULL
                );
                CREATE UNIQUE INDEX door_id_uindex ON door (id);"),

            (6, "Add global_setting table", @"
                CREATE TABLE global_setting (
                    name TEXT NOT NULL PRIMARY KEY,
                    value TEXT NOT NULL
                );
                CREATE UNIQUE INDEX global_setting_name_uindex ON global_setting (name);"),

            (7, "Add credential table", @"
                CREATE TABLE credential (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    number TEXT NOT NULL,
                    enroll_date DATETIME NOT NULL
                );
                CREATE UNIQUE INDEX credential_id_uindex ON credential (id);"),

            (8, "Add person table", @"
                CREATE TABLE person (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    first_name TEXT,
                    last_name TEXT,
                    enabled INTEGER DEFAULT 0 NOT NULL
                );
                CREATE UNIQUE INDEX person_id_uindex ON person (id);

                CREATE TABLE credential_assignment (
                    person_id INTEGER NOT NULL REFERENCES person,
                    credential_id INTEGER NOT NULL PRIMARY KEY REFERENCES credential,
                    enabled INTEGER DEFAULT 0 NOT NULL
                );
                CREATE UNIQUE INDEX credential_assignment_credential_id_uindex ON credential_assignment (credential_id);"),

            (9, "Add event table", @"
                CREATE TABLE event (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    endpoint_id INTEGER NOT NULL REFERENCES endpoint,
                    timestamp DATETIME NOT NULL,
                    event_type INTEGER NOT NULL,
                    data TEXT NOT NULL
                );
                CREATE UNIQUE INDEX event_id_uindex ON event (id);
                CREATE UNIQUE INDEX credential_number_uindex ON credential (number);"),

            (10, "Add last_event to credential table", @"
                CREATE TABLE credential_dg_tmp (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    number TEXT NOT NULL,
                    last_event INTEGER
                );
                INSERT INTO credential_dg_tmp(id, number) SELECT id, number FROM credential;
                DROP TABLE credential;
                ALTER TABLE credential_dg_tmp RENAME TO credential;
                CREATE UNIQUE INDEX credential_id_uindex ON credential (id);
                CREATE UNIQUE INDEX credential_number_uindex ON credential (number);")
        };
    }
}
