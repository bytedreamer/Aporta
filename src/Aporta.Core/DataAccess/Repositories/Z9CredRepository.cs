using System.Threading.Tasks;
using Dapper;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.DataAccess.Repositories;

/// <summary>
/// Repository for storing the full Z9 Cred proto messages (including privBindings).
/// Overrides Upsert to maintain the denormalized cred_num column.
/// </summary>
public class Z9CredRepository : ProtoJsonRepository<Cred>
{
    private readonly IDataAccess _dataAccess;

    public Z9CredRepository(IDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    protected override IDataAccess DataAccess => _dataAccess;
    protected override string TableName => "z9_cred";
    protected override int GetUnid(Cred message) => message.Unid;

    public new async Task Upsert(Cred message)
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        var id = GetUnid(message);
        var json = Formatter.Format(message);
        var credNum = ExtractCredNum(message);

        await connection.ExecuteAsync(
            "INSERT INTO z9_cred (id, data, cred_num) VALUES (@id, @data, @credNum) " +
            "ON CONFLICT(id) DO UPDATE SET data = @data, cred_num = @credNum",
            new { id, data = json, credNum });
    }

    /// <summary>
    /// Extracts the credential number as a decimal string from a Cred's CardPin.CredNum.
    /// Returns null if no CredNum is present.
    /// </summary>
    public static string ExtractCredNum(Cred cred)
    {
        if (cred.CardPin?.CredNum == null || cred.CardPin.CredNum.BytesCase == BigIntegerData.BytesOneofCase.None)
            return null;

        return SpCoreProtoUtil.ToBigInteger(cred.CardPin.CredNum).ToString();
    }

    /// <summary>
    /// Returns the next available ID (MAX(id) + 1, or 1 if empty).
    /// Used by PeopleService for person creation.
    /// </summary>
    public async Task<int> NextId()
    {
        using var connection = DataAccess.CreateDbConnection();
        connection.Open();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(id), 0) + 1 FROM z9_cred");
    }
}
