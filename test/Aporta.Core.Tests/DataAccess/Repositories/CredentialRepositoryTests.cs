using System;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Tests.DataAccess.Repositories;

public class CredentialRepositoryTests
{
    private readonly IDataAccess _dataAccess = new SqLiteDataAccess(true);
    private IDbConnection _persistConnection;
    private Guid _extensionId;

    [SetUp]
    public async Task Setup()
    {
        _persistConnection = _dataAccess.CreateDbConnection();
        _persistConnection.Open();

        await _dataAccess.UpdateSchema();

        _extensionId = Guid.NewGuid();
        var extensions = new[]
        {
            new ExtensionHost {Id = _extensionId, Name = "ExtensionTest", Enabled = false}
        };

        var extensionRepository = new ExtensionRepository(_dataAccess);
        foreach (var extension in extensions)
        {
            await extensionRepository.Insert(extension);
        }
    }

    [TearDown]
    public void TearDown()
    {
        _persistConnection?.Close();
        _persistConnection?.Dispose();
    }

    private static Cred CreateZ9Cred(int unid, string name, bool enabled, BigInteger? credNum = null)
    {
        var cred = new Cred
        {
            Unid = unid,
            Enabled = enabled,
            CredTemplateUnid = 1
        };
        if (name != null)
        {
            cred.Name = name;
        }
        if (credNum.HasValue)
        {
            cred.CardPin = new CardPin
            {
                CredNum = SpCoreProtoUtil.ToBigIntegerData(credNum.Value)
            };
        }
        SpCoreProtoUtil.InitRequired(cred);
        return cred;
    }

    [Test]
    public async Task Get_CredentialWithZ9Cred()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var cred = CreateZ9Cred(1, "Smith, John", true, new BigInteger(5345234));
        await z9CredRepository.Upsert(cred);

        // Act
        var actual = await credentialRepository.Get(1);

        // Assert
        Assert.That(actual.Number, Is.EqualTo("5345234"));
        Assert.That(actual.Enabled, Is.True);
    }

    [Test]
    public async Task Get_CredentialWithoutCredNum()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var cred = CreateZ9Cred(1, "Smith, John", true);
        await z9CredRepository.Upsert(cred);

        // Act
        var actual = await credentialRepository.Get(1);

        // Assert
        Assert.That(actual.Number, Is.Null);
        Assert.That(actual.Enabled, Is.True);
    }

    [Test]
    public async Task Get_NonExistent_ReturnsNull()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);

        // Act
        var actual = await credentialRepository.Get(999);

        // Assert
        Assert.That(actual, Is.Null);
    }

    [Test]
    public async Task DuplicateCredNum_ThrowsSqliteException()
    {
        // Arrange
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var cred1 = CreateZ9Cred(1, null, true, new BigInteger(2345342));
        await z9CredRepository.Upsert(cred1);

        var cred2 = CreateZ9Cred(2, null, true, new BigInteger(2345342));

        // Assert
        Assert.ThrowsAsync<SqliteException>(() => z9CredRepository.Upsert(cred2));
    }

    [Test]
    public async Task AssignedCredential_NoZ9CredName()
    {
        // Arrange - z9_cred exists with cred_num but no name
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var credentialRepository = new CredentialRepository(_dataAccess);

        var cred = CreateZ9Cred(1, null, true, new BigInteger(5345234));
        await z9CredRepository.Upsert(cred);

        // Act
        var actual = await credentialRepository.AssignedCredential("5345234");

        // Assert - Person should be null (not enrolled)
        Assert.That(actual.Person, Is.Null);
    }

    [Test]
    public async Task AssignedCredential_WithZ9CredName_Enabled()
    {
        // Arrange
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var credentialRepository = new CredentialRepository(_dataAccess);

        var cred = CreateZ9Cred(1, "Smith, John", true, new BigInteger(5345234));
        await z9CredRepository.Upsert(cred);

        // Act
        var actual = await credentialRepository.AssignedCredential("5345234");

        // Assert
        Assert.That(actual.Number, Is.EqualTo("5345234"));
        Assert.That(actual.Enabled, Is.True);
        Assert.That(actual.Person, Is.Not.Null);
        Assert.That(actual.Person.FirstName, Is.EqualTo("John"));
        Assert.That(actual.Person.LastName, Is.EqualTo("Smith"));
        Assert.That(actual.Person.Enabled, Is.True);
    }

    [Test]
    public async Task AssignedCredential_WithZ9CredName_Disabled()
    {
        // Arrange
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var credentialRepository = new CredentialRepository(_dataAccess);

        var cred = CreateZ9Cred(1, "Jones, Jane", false, new BigInteger(5345234));
        await z9CredRepository.Upsert(cred);

        // Act
        var actual = await credentialRepository.AssignedCredential("5345234");

        // Assert
        Assert.That(actual.Number, Is.EqualTo("5345234"));
        Assert.That(actual.Enabled, Is.False);
        Assert.That(actual.Person, Is.Not.Null);
        Assert.That(actual.Person.FirstName, Is.EqualTo("Jane"));
        Assert.That(actual.Person.LastName, Is.EqualTo("Jones"));
        Assert.That(actual.Person.Enabled, Is.False);
    }

    [Test]
    public async Task Assigned_ReturnsOnlyNamedCredentialsWithNumber()
    {
        // Arrange
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var credentialRepository = new CredentialRepository(_dataAccess);

        // Enrolled: has name AND cred_num
        var enrolled = CreateZ9Cred(1, "Smith, John", true, new BigInteger(1111));
        await z9CredRepository.Upsert(enrolled);

        // Person only: has name but no cred_num
        var personOnly = CreateZ9Cred(2, "Doe, Jane", true);
        await z9CredRepository.Upsert(personOnly);

        // Raw swipe: has cred_num but no name
        var swipe = CreateZ9Cred(3, null, true, new BigInteger(2222));
        await z9CredRepository.Upsert(swipe);

        // Act
        var assigned = (await credentialRepository.Assigned()).ToList();

        // Assert - only the enrolled credential
        Assert.That(assigned, Has.Exactly(1).Items);
        Assert.That(assigned[0].Id, Is.EqualTo(1));
        Assert.That(assigned[0].Number, Is.EqualTo("1111"));
    }

    [Test]
    public async Task Unassigned_ReturnsCredentialsWithoutZ9CredName()
    {
        // Arrange
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var credentialRepository = new CredentialRepository(_dataAccess);

        // Named credential (not unassigned)
        var named = CreateZ9Cred(1, "Smith, John", true, new BigInteger(1111));
        await z9CredRepository.Upsert(named);

        // Raw swipe (no name → unassigned)
        var swipe = CreateZ9Cred(2, null, true, new BigInteger(2222));
        await z9CredRepository.Upsert(swipe);

        // Act
        var unassigned = (await credentialRepository.Unassigned()).ToList();

        // Assert
        Assert.That(unassigned, Has.Exactly(1).Items);
        Assert.That(unassigned[0].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task Named_ReturnsAllPersonDTOs()
    {
        // Arrange
        var z9CredRepository = new Z9CredRepository(_dataAccess);
        var credentialRepository = new CredentialRepository(_dataAccess);

        var cred1 = CreateZ9Cred(1, "Smith, John", true);
        await z9CredRepository.Upsert(cred1);

        var cred2 = CreateZ9Cred(2, "Doe, Jane", false);
        await z9CredRepository.Upsert(cred2);

        // Raw swipe (no name)
        var swipe = CreateZ9Cred(3, null, true, new BigInteger(9999));
        await z9CredRepository.Upsert(swipe);

        // Act
        var people = (await credentialRepository.Named()).ToList();

        // Assert - only 2 named people
        Assert.That(people, Has.Exactly(2).Items);
        Assert.That(people.Any(p => p.FirstName == "John" && p.LastName == "Smith" && p.Enabled), Is.True);
        Assert.That(people.Any(p => p.FirstName == "Jane" && p.LastName == "Doe" && !p.Enabled), Is.True);
    }

    [Test]
    public void ParsePersonFromName_WithComma()
    {
        var person = CredentialRepository.ParsePersonFromName("Smith, John", 42, true);
        Assert.That(person.Id, Is.EqualTo(42));
        Assert.That(person.FirstName, Is.EqualTo("John"));
        Assert.That(person.LastName, Is.EqualTo("Smith"));
        Assert.That(person.Enabled, Is.True);
    }

    [Test]
    public void ParsePersonFromName_WithoutComma()
    {
        var person = CredentialRepository.ParsePersonFromName("JohnOnly", 42, false);
        Assert.That(person.FirstName, Is.EqualTo("JohnOnly"));
        Assert.That(person.LastName, Is.Null);
        Assert.That(person.Enabled, Is.False);
    }
}
