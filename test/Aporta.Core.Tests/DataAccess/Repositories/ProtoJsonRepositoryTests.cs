using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using NUnit.Framework;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.DataAccess.Repositories;

[TestFixture]
public class DataFormatRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new DataFormatRepository(_dataAccess);
        var dataFormat = new DataFormat
        {
            Unid = 1,
            Name = "Standard 26-bit"
        };

        await repository.Upsert(dataFormat);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Standard 26-bit"));
    }

    [Test]
    public async Task Upsert_ExistingRecord_Updates()
    {
        var repository = new DataFormatRepository(_dataAccess);
        var original = new DataFormat { Unid = 1, Name = "Original" };
        var updated = new DataFormat { Unid = 1, Name = "Updated" };

        await repository.Upsert(original);
        await repository.Upsert(updated);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved.Name, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task Get_NonExistentId_ReturnsNull()
    {
        var repository = new DataFormatRepository(_dataAccess);
        var retrieved = await repository.Get(999);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task GetAll_MultipleRecords_ReturnsAll()
    {
        var repository = new DataFormatRepository(_dataAccess);
        await repository.Upsert(new DataFormat { Unid = 1, Name = "Format1" });
        await repository.Upsert(new DataFormat { Unid = 2, Name = "Format2" });
        await repository.Upsert(new DataFormat { Unid = 3, Name = "Format3" });

        var all = (await repository.GetAll()).ToList();

        Assert.That(all.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task Delete_ExistingRecord_Removes()
    {
        var repository = new DataFormatRepository(_dataAccess);
        await repository.Upsert(new DataFormat { Unid = 1, Name = "ToDelete" });

        await repository.Delete(1);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task DeleteAll_ClearsTable()
    {
        var repository = new DataFormatRepository(_dataAccess);
        await repository.Upsert(new DataFormat { Unid = 1, Name = "Format1" });
        await repository.Upsert(new DataFormat { Unid = 2, Name = "Format2" });

        await repository.DeleteAll();
        var count = await repository.Count();

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task DataFormat_WithBinaryFormat_PreservesExtension()
    {
        var repository = new DataFormatRepository(_dataAccess);
        var dataFormat = new DataFormat
        {
            Unid = 1,
            Name = "Standard 26-bit Wiegand",
            DataFormatType = DataFormatType.Binary,
            ExtBinaryFormat = new BinaryFormat
            {
                MinBits = 26,
                MaxBits = 26,
                SupportReverseRead = false
            }
        };

        await repository.Upsert(dataFormat);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved.DataFormatType, Is.EqualTo(DataFormatType.Binary));
        Assert.That(retrieved.ExtBinaryFormat, Is.Not.Null);
        Assert.That(retrieved.ExtBinaryFormat.MinBits, Is.EqualTo(26));
        Assert.That(retrieved.ExtBinaryFormat.MaxBits, Is.EqualTo(26));
        Assert.That(retrieved.ExtBinaryFormat.SupportReverseRead, Is.False);
    }
}

[TestFixture]
public class DataLayoutRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new DataLayoutRepository(_dataAccess);
        var dataLayout = new DataLayout
        {
            Unid = 1,
            Name = "Standard Layout"
        };

        await repository.Upsert(dataLayout);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Standard Layout"));
    }

    [Test]
    public async Task DataLayout_WithBasicDataLayout_PreservesExtension()
    {
        var repository = new DataLayoutRepository(_dataAccess);
        var dataLayout = new DataLayout
        {
            Unid = 1,
            Name = "Basic Layout",
            LayoutType = DataLayoutType.Basic,
            ExtBasicDataLayout = new BasicDataLayout
            {
                DataFormatUnid = 10
            }
        };

        await repository.Upsert(dataLayout);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved.LayoutType, Is.EqualTo(DataLayoutType.Basic));
        Assert.That(retrieved.ExtBasicDataLayout, Is.Not.Null);
        Assert.That(retrieved.ExtBasicDataLayout.DataFormatUnid, Is.EqualTo(10));
    }
}

[TestFixture]
public class CredTemplateRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new CredTemplateRepository(_dataAccess);
        var credTemplate = new CredTemplate
        {
            Unid = 1,
            Name = "Standard Card Template"
        };

        await repository.Upsert(credTemplate);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Standard Card Template"));
    }

    [Test]
    public async Task CredTemplate_WithCardPinTemplate_PreservesNested()
    {
        var repository = new CredTemplateRepository(_dataAccess);
        var credTemplate = new CredTemplate
        {
            Unid = 1,
            Name = "Card Template",
            CardPinTemplate = new CardPinTemplate
            {
                DataLayoutUnid = 5,
                AnyDataLayout = false
            }
        };

        await repository.Upsert(credTemplate);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved.CardPinTemplate, Is.Not.Null);
        Assert.That(retrieved.CardPinTemplate.DataLayoutUnid, Is.EqualTo(5));
    }
}

[TestFixture]
public class PrivRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new PrivRepository(_dataAccess);
        var priv = new Priv
        {
            Unid = 1,
            Name = "Front Door Access"
        };

        await repository.Upsert(priv);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Front Door Access"));
    }

    [Test]
    public async Task Priv_WithDoorAccessPriv_PreservesExtension()
    {
        var repository = new PrivRepository(_dataAccess);
        var priv = new Priv
        {
            Unid = 1,
            Name = "Door Access",
            PrivType = PrivType.Door,
            ExtDoorAccessPriv = new DoorAccessPriv
            {
                DoorUnid = 10
            }
        };

        await repository.Upsert(priv);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved.PrivType, Is.EqualTo(PrivType.Door));
        Assert.That(retrieved.ExtDoorAccessPriv, Is.Not.Null);
        Assert.That(retrieved.ExtDoorAccessPriv.DoorUnid, Is.EqualTo(10));
    }
}

[TestFixture]
public class SchedRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new SchedRepository(_dataAccess);
        var sched = new Sched
        {
            Unid = 1,
            Name = "Business Hours"
        };

        await repository.Upsert(sched);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Business Hours"));
    }

    [Test]
    public async Task Sched_WithElements_PreservesRepeated()
    {
        var repository = new SchedRepository(_dataAccess);
        var sched = new Sched
        {
            Unid = 1,
            Name = "Work Week"
        };
        sched.Elements.Add(new SchedElement
        {
            DayMask = 0x1F, // Mon-Fri
            StartMinuteOfDay = 480, // 8:00 AM
            EndMinuteOfDay = 1020 // 5:00 PM
        });

        await repository.Upsert(sched);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved.Elements.Count, Is.EqualTo(1));
        Assert.That(retrieved.Elements[0].DayMask, Is.EqualTo(0x1F));
        Assert.That(retrieved.Elements[0].StartMinuteOfDay, Is.EqualTo(480));
    }
}

[TestFixture]
public class HolRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new HolRepository(_dataAccess);
        var hol = new Hol
        {
            Unid = 1,
            Name = "Christmas"
        };

        await repository.Upsert(hol);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Christmas"));
    }
}

[TestFixture]
public class HolCalRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new HolCalRepository(_dataAccess);
        var holCal = new HolCal
        {
            Unid = 1,
            Name = "US Federal Holidays"
        };

        await repository.Upsert(holCal);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("US Federal Holidays"));
    }
}

[TestFixture]
public class HolTypeRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();
        await _dataAccess.UpdateSchema();
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    [Test]
    public async Task Upsert_NewRecord_Stores()
    {
        var repository = new HolTypeRepository(_dataAccess);
        var holType = new HolType
        {
            Unid = 1,
            Name = "Federal Holiday"
        };

        await repository.Upsert(holType);
        var retrieved = await repository.Get(1);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Unid, Is.EqualTo(1));
        Assert.That(retrieved.Name, Is.EqualTo("Federal Holiday"));
    }
}
