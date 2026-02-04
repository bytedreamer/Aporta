using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Aporta.Migration.Tests;

[TestFixture]
public class MigrationTests
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        // Create in-memory database
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public async Task Migrate_FromVersion10_TransformsAllTables()
    {
        // Arrange - Create old schema with test data
        await CreateOldSchemaVersion10();
        await InsertTestData();

        // Act - Run migration
        var migrator = new Migrator(_connection, dryRun: false);
        await migrator.MigrateAsync();

        // Assert - Verify each table was transformed correctly
        await VerifyExtensionTable();
        await VerifyEndpointTable();
        await VerifyOutputTable();
        await VerifyInputTable();
        await VerifyDoorTable();
        await VerifyGlobalSettingTable();
        await VerifyPersonTable();
        await VerifyCredentialTable();
        await VerifyCredentialAssignmentTable();
        await VerifyEventTable();
        await VerifySchemaInfo();
    }

    [Test]
    public async Task Migrate_FromVersion5_AppliesRemainingMigrationsFirst()
    {
        // Arrange - Create partial old schema (version 5)
        await CreateOldSchemaVersion5();

        // Act - Run migration
        var migrator = new Migrator(_connection, dryRun: false);
        await migrator.MigrateAsync();

        // Assert - All tables should exist with JSON schema
        var tables = await _connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name != 'sqlite_sequence'");

        Assert.That(tables, Does.Contain("person"));
        Assert.That(tables, Does.Contain("credential"));
        Assert.That(tables, Does.Contain("credential_assignment"));
        Assert.That(tables, Does.Contain("event"));
    }

    [Test]
    public async Task Migrate_DryRun_MakesNoChanges()
    {
        // Arrange
        await CreateOldSchemaVersion10();
        await InsertTestData();

        // Act - Run migration in dry-run mode
        var migrator = new Migrator(_connection, dryRun: true);
        await migrator.MigrateAsync();

        // Assert - Old schema should still exist (person has first_name column, not data)
        var columns = await _connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('person')");
        Assert.That(columns, Does.Contain("first_name"));
        Assert.That(columns, Does.Not.Contain("data"));
    }

    [Test]
    public async Task Migrate_AlreadyJsonSchema_DoesNothing()
    {
        // Arrange - Create new JSON schema with test data
        await CreateNewJsonSchema();
        var testData = "{\"firstName\":\"Test\",\"lastName\":\"User\",\"enabled\":true}";
        await _connection.ExecuteAsync(
            "INSERT INTO person (id, data) VALUES (1, @data)", new { data = testData });

        // Act
        var migrator = new Migrator(_connection, dryRun: false);
        await migrator.MigrateAsync();

        // Assert - Data should be unchanged
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM person WHERE id = 1");
        Assert.That(result.Data, Is.EqualTo(testData));

        // Schema version should still be 0
        var version = await _connection.QueryFirstAsync<int>("SELECT MAX(id) FROM schema_info");
        Assert.That(version, Is.EqualTo(0));
    }

    private async Task CreateOldSchemaVersion10()
    {
        await _connection.ExecuteAsync(@"
            CREATE TABLE schema_info (id INTEGER PRIMARY KEY, name TEXT NOT NULL, timestamp DATETIME NOT NULL);
            INSERT INTO schema_info VALUES (0, 'Initial', '2024-01-01');
            INSERT INTO schema_info VALUES (1, 'Extension', '2024-01-01');
            INSERT INTO schema_info VALUES (2, 'Endpoint', '2024-01-01');
            INSERT INTO schema_info VALUES (3, 'Output', '2024-01-01');
            INSERT INTO schema_info VALUES (4, 'Input', '2024-01-01');
            INSERT INTO schema_info VALUES (5, 'Door', '2024-01-01');
            INSERT INTO schema_info VALUES (6, 'GlobalSetting', '2024-01-01');
            INSERT INTO schema_info VALUES (7, 'Credential', '2024-01-01');
            INSERT INTO schema_info VALUES (8, 'Person', '2024-01-01');
            INSERT INTO schema_info VALUES (9, 'Event', '2024-01-01');
            INSERT INTO schema_info VALUES (10, 'LastEvent', '2024-01-01');

            CREATE TABLE extension (id TEXT PRIMARY KEY, name TEXT NOT NULL, enabled INTEGER NOT NULL, configuration TEXT NOT NULL);
            CREATE TABLE endpoint (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, driver_id TEXT NOT NULL, endpoint_type INTEGER NOT NULL, extension_id TEXT NOT NULL);
            CREATE TABLE output (id INTEGER PRIMARY KEY AUTOINCREMENT, endpoint_id INTEGER NOT NULL, name TEXT NOT NULL);
            CREATE TABLE input (id INTEGER PRIMARY KEY AUTOINCREMENT, endpoint_id INTEGER NOT NULL, name TEXT NOT NULL);
            CREATE TABLE door (id INTEGER PRIMARY KEY AUTOINCREMENT, in_access_endpoint_id INTEGER, out_access_endpoint_id INTEGER, door_contact_endpoint_id INTEGER, request_to_exit_endpoint_id INTEGER, door_strike_endpoint_id INTEGER, name TEXT NOT NULL);
            CREATE TABLE global_setting (name TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE person (id INTEGER PRIMARY KEY AUTOINCREMENT, first_name TEXT, last_name TEXT, enabled INTEGER NOT NULL);
            CREATE TABLE credential (id INTEGER PRIMARY KEY AUTOINCREMENT, number TEXT NOT NULL, last_event INTEGER);
            CREATE TABLE credential_assignment (person_id INTEGER NOT NULL, credential_id INTEGER PRIMARY KEY, enabled INTEGER NOT NULL);
            CREATE TABLE event (id INTEGER PRIMARY KEY AUTOINCREMENT, endpoint_id INTEGER NOT NULL, timestamp DATETIME NOT NULL, event_type INTEGER NOT NULL, data TEXT NOT NULL);
        ");
    }

    private async Task CreateOldSchemaVersion5()
    {
        await _connection.ExecuteAsync(@"
            CREATE TABLE schema_info (id INTEGER PRIMARY KEY, name TEXT NOT NULL, timestamp DATETIME NOT NULL);
            INSERT INTO schema_info VALUES (0, 'Initial', '2024-01-01');
            INSERT INTO schema_info VALUES (1, 'Extension', '2024-01-01');
            INSERT INTO schema_info VALUES (2, 'Endpoint', '2024-01-01');
            INSERT INTO schema_info VALUES (3, 'Output', '2024-01-01');
            INSERT INTO schema_info VALUES (4, 'Input', '2024-01-01');
            INSERT INTO schema_info VALUES (5, 'Door', '2024-01-01');

            CREATE TABLE extension (id TEXT PRIMARY KEY, name TEXT NOT NULL, enabled INTEGER NOT NULL, configuration TEXT NOT NULL);
            CREATE TABLE endpoint (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, driver_id TEXT NOT NULL, endpoint_type INTEGER NOT NULL, extension_id TEXT NOT NULL);
            CREATE TABLE output (id INTEGER PRIMARY KEY AUTOINCREMENT, endpoint_id INTEGER NOT NULL, name TEXT NOT NULL);
            CREATE TABLE input (id INTEGER PRIMARY KEY AUTOINCREMENT, endpoint_id INTEGER NOT NULL, name TEXT NOT NULL);
            CREATE TABLE door (id INTEGER PRIMARY KEY AUTOINCREMENT, in_access_endpoint_id INTEGER, out_access_endpoint_id INTEGER, door_contact_endpoint_id INTEGER, request_to_exit_endpoint_id INTEGER, door_strike_endpoint_id INTEGER, name TEXT NOT NULL);
        ");
    }

    private async Task CreateNewJsonSchema()
    {
        await _connection.ExecuteAsync(@"
            CREATE TABLE schema_info (id INTEGER PRIMARY KEY, name TEXT NOT NULL, timestamp DATETIME NOT NULL);
            INSERT INTO schema_info VALUES (0, 'Initial create with JSON document storage', '2024-01-01');

            CREATE TABLE extension (id TEXT PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE endpoint (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE output (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE input (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE door (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE global_setting (name TEXT PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE person (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE credential (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE credential_assignment (id INTEGER PRIMARY KEY, data TEXT NOT NULL);
            CREATE TABLE event (id INTEGER PRIMARY KEY AUTOINCREMENT, data TEXT NOT NULL);
        ");
    }

    private async Task InsertTestData()
    {
        await _connection.ExecuteAsync(@"
            INSERT INTO extension VALUES ('550e8400-e29b-41d4-a716-446655440000', 'Virtual Driver', 1, '{""readers"":[]}');
            INSERT INTO endpoint VALUES (1, 'Reader 1', 'R1', 0, '550e8400-e29b-41d4-a716-446655440000');
            INSERT INTO endpoint VALUES (2, 'Output 1', 'O1', 1, '550e8400-e29b-41d4-a716-446655440000');
            INSERT INTO output VALUES (1, 2, 'Door Strike');
            INSERT INTO input VALUES (1, 1, 'Door Contact');
            INSERT INTO door VALUES (1, 1, NULL, NULL, NULL, 2, 'Front Door');
            INSERT INTO global_setting VALUES ('timezone', 'America/New_York');
            INSERT INTO person VALUES (1, 'John', 'Doe', 1);
            INSERT INTO person VALUES (2, 'Jane', 'Smith', 0);
            INSERT INTO credential VALUES (1, '12345678', NULL);
            INSERT INTO credential VALUES (2, '87654321', 5);
            INSERT INTO credential_assignment VALUES (1, 1, 1);
            INSERT INTO credential_assignment VALUES (2, 2, 0);
            INSERT INTO event VALUES (1, 1, '2024-01-15 10:30:00', 1, '{""card"":""12345678""}');
        ");
    }

    private async Task VerifyExtensionTable()
    {
        var result = await _connection.QueryFirstAsync<(string Id, string Data)>(
            "SELECT id, data FROM extension WHERE id = @id",
            new { id = "550e8400-e29b-41d4-a716-446655440000" });

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Virtual Driver"));
        Assert.That(json.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
    }

    private async Task VerifyEndpointTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM endpoint WHERE id = 1");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Reader 1"));
        Assert.That(json.RootElement.GetProperty("driverEndpointId").GetString(), Is.EqualTo("R1"));
        Assert.That(json.RootElement.GetProperty("type").GetInt32(), Is.EqualTo(0));
    }

    private async Task VerifyOutputTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM output WHERE id = 1");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Door Strike"));
        Assert.That(json.RootElement.GetProperty("endpointId").GetInt32(), Is.EqualTo(2));
    }

    private async Task VerifyInputTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM input WHERE id = 1");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Door Contact"));
        Assert.That(json.RootElement.GetProperty("endpointId").GetInt32(), Is.EqualTo(1));
    }

    private async Task VerifyDoorTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM door WHERE id = 1");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Front Door"));
        Assert.That(json.RootElement.GetProperty("inAccessEndpointId").GetInt32(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("doorStrikeEndpointId").GetInt32(), Is.EqualTo(2));
    }

    private async Task VerifyGlobalSettingTable()
    {
        var result = await _connection.QueryFirstAsync<(string Name, string Data)>(
            "SELECT name, data FROM global_setting WHERE name = 'timezone'");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("value").GetString(), Is.EqualTo("America/New_York"));
    }

    private async Task VerifyPersonTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM person WHERE id = 1");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("firstName").GetString(), Is.EqualTo("John"));
        Assert.That(json.RootElement.GetProperty("lastName").GetString(), Is.EqualTo("Doe"));
        Assert.That(json.RootElement.GetProperty("enabled").GetBoolean(), Is.True);
    }

    private async Task VerifyCredentialTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM credential WHERE id = 2");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("number").GetString(), Is.EqualTo("87654321"));
        Assert.That(json.RootElement.GetProperty("lastEvent").GetInt32(), Is.EqualTo(5));
    }

    private async Task VerifyCredentialAssignmentTable()
    {
        // Old schema: credential_id was PK, now we have our own id
        var results = await _connection.QueryAsync<(int Id, string Data)>(
            "SELECT id, data FROM credential_assignment");

        Assert.That(results.Count(), Is.EqualTo(2));

        var first = results.First();
        var json = JsonDocument.Parse(first.Data);
        Assert.That(json.RootElement.GetProperty("credentialId").GetInt32(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("personId").GetInt32(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("enabled").GetInt32(), Is.EqualTo(1));
    }

    private async Task VerifyEventTable()
    {
        var result = await _connection.QueryFirstAsync<(int Id, string Data)>(
            "SELECT id, data FROM event WHERE id = 1");

        var json = JsonDocument.Parse(result.Data);
        Assert.That(json.RootElement.GetProperty("endpointId").GetInt32(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("type").GetInt32(), Is.EqualTo(1));
    }

    private async Task VerifySchemaInfo()
    {
        var version = await _connection.QueryFirstAsync<int>("SELECT MAX(id) FROM schema_info");
        Assert.That(version, Is.EqualTo(0)); // New JSON schema is version 0
    }
}
