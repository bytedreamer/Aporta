using System;
using System.Data;
using System.Linq;
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

    [Test]
    public async Task Get_CredentialWithZ9Cred()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var credential = new Credential { Number = "5345234" };
        await credentialRepository.Insert(credential);

        var cred = new Cred
        {
            Unid = credential.Id,
            Name = "Smith, John",
            Enabled = true,
            CredTemplateUnid = 1
        };
        SpCoreProtoUtil.InitRequired(cred);
        await z9CredRepository.Upsert(cred);

        // Act
        var actual = await credentialRepository.Get(credential.Id);

        // Assert
        Assert.That(actual.Number, Is.EqualTo("5345234"));
        Assert.That(actual.Enabled, Is.True);
    }

    [Test]
    public async Task Get_CredentialWithoutZ9Cred()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);

        var credential = new Credential { Number = "5345234" };
        await credentialRepository.Insert(credential);

        // Act
        var actual = await credentialRepository.Get(credential.Id);

        // Assert
        Assert.That(actual.Number, Is.EqualTo("5345234"));
        Assert.That(actual.Enabled, Is.Null);
    }

    [Test]
    public async Task Insert()
    {
        // Arrange
        var credentials = new[]
        {
            new Credential {Number = "2345342", LastEvent = 4},
            new Credential {Number = "5345234", LastEvent = 5},
        };

        var credentialRepository = new CredentialRepository(_dataAccess);
        foreach (var credential in credentials)
        {
            await credentialRepository.Insert(credential);
        }

        // Act
        var actualCredential = await credentialRepository.Get(2);

        // Assert
        Assert.That(credentials[1].Id, Is.EqualTo(2));
        Assert.That(actualCredential.Id, Is.EqualTo(2));
        Assert.That(actualCredential.LastEvent, Is.EqualTo(5));
        Assert.That(actualCredential.Number, Is.EqualTo("5345234"));
    }

    [Test]
    public async Task Insert_DuplicateCardNumber()
    {
        // Arrange
        var credential = new Credential {Number = "2345342"};

        var credentialRepository = new CredentialRepository(_dataAccess);

        await credentialRepository.Insert(credential);

        // Assert
        async Task InsertCredential()
        {
            await credentialRepository.Insert(credential);
        }

        // Assert
        Assert.ThrowsAsync<SqliteException>(InsertCredential);
    }

    [Test]
    public async Task Delete()
    {
        // Arrange
        var credentials = new[]
        {
            new Credential {Number = "2345342"},
            new Credential {Number = "5345234"},
        };

        var credentialRepository = new CredentialRepository(_dataAccess);
        foreach (var credential in credentials)
        {
            await credentialRepository.Insert(credential);
        }

        // Act
        await credentialRepository.Delete(2);

        // Assert
        var actualCredentials = await credentialRepository.GetAll();
        Assert.That(actualCredentials.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task AssignedCredential_NoZ9CredName()
    {
        // Arrange - credential exists but no z9_cred with a name
        var credentialRepository = new CredentialRepository(_dataAccess);

        var credential = new Credential { Number = "5345234" };
        await credentialRepository.Insert(credential);

        // Act
        var actual = await credentialRepository.AssignedCredential("5345234");

        // Assert - Person should be null (not enrolled)
        Assert.That(actual.Person, Is.Null);
    }

    [Test]
    public async Task AssignedCredential_WithZ9CredName_Enabled()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var credential = new Credential { Number = "5345234" };
        await credentialRepository.Insert(credential);

        var cred = new Cred
        {
            Unid = credential.Id,
            Name = "Smith, John",
            Enabled = true,
            CredTemplateUnid = 1
        };
        SpCoreProtoUtil.InitRequired(cred);
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
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var credential = new Credential { Number = "5345234" };
        await credentialRepository.Insert(credential);

        var cred = new Cred
        {
            Unid = credential.Id,
            Name = "Jones, Jane",
            Enabled = false,
            CredTemplateUnid = 1
        };
        SpCoreProtoUtil.InitRequired(cred);
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
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        // Credential with name AND number (enrolled)
        var enrolled = new Credential { Number = "1111" };
        await credentialRepository.Insert(enrolled);
        var enrolledCred = new Cred { Unid = enrolled.Id, Name = "Smith, John", Enabled = true, CredTemplateUnid = 1 };
        SpCoreProtoUtil.InitRequired(enrolledCred);
        await z9CredRepository.Upsert(enrolledCred);

        // Credential with name but no number (person not yet enrolled)
        var personOnly = new Credential();
        await credentialRepository.Insert(personOnly);
        var personCred = new Cred { Unid = personOnly.Id, Name = "Doe, Jane", Enabled = true, CredTemplateUnid = 1 };
        SpCoreProtoUtil.InitRequired(personCred);
        await z9CredRepository.Upsert(personCred);

        // Credential with number but no name (raw swipe)
        var swipe = new Credential { Number = "2222" };
        await credentialRepository.Insert(swipe);

        // Act
        var assigned = (await credentialRepository.Assigned()).ToList();

        // Assert - only the enrolled credential
        Assert.That(assigned, Has.Exactly(1).Items);
        Assert.That(assigned[0].Id, Is.EqualTo(enrolled.Id));
        Assert.That(assigned[0].Number, Is.EqualTo("1111"));
    }

    [Test]
    public async Task Unassigned_ReturnsCredentialsWithoutZ9CredName()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        // Named credential (not unassigned)
        var named = new Credential { Number = "1111" };
        await credentialRepository.Insert(named);
        var namedCred = new Cred { Unid = named.Id, Name = "Smith, John", Enabled = true, CredTemplateUnid = 1 };
        SpCoreProtoUtil.InitRequired(namedCred);
        await z9CredRepository.Upsert(namedCred);

        // Raw swipe (no z9_cred name → unassigned)
        var swipe = new Credential { Number = "2222" };
        await credentialRepository.Insert(swipe);

        // Act
        var unassigned = (await credentialRepository.Unassigned()).ToList();

        // Assert
        Assert.That(unassigned, Has.Exactly(1).Items);
        Assert.That(unassigned[0].Id, Is.EqualTo(swipe.Id));
    }

    [Test]
    public async Task Named_ReturnsAllPersonDTOs()
    {
        // Arrange
        var credentialRepository = new CredentialRepository(_dataAccess);
        var z9CredRepository = new Z9CredRepository(_dataAccess);

        var cred1 = new Credential();
        await credentialRepository.Insert(cred1);
        var z9Cred1 = new Cred { Unid = cred1.Id, Name = "Smith, John", Enabled = true, CredTemplateUnid = 1 };
        SpCoreProtoUtil.InitRequired(z9Cred1);
        await z9CredRepository.Upsert(z9Cred1);

        var cred2 = new Credential();
        await credentialRepository.Insert(cred2);
        var z9Cred2 = new Cred { Unid = cred2.Id, Name = "Doe, Jane", Enabled = false, CredTemplateUnid = 1 };
        SpCoreProtoUtil.InitRequired(z9Cred2);
        await z9CredRepository.Upsert(z9Cred2);

        // Raw swipe (no name)
        var swipe = new Credential { Number = "9999" };
        await credentialRepository.Insert(swipe);

        // Act
        var people = (await credentialRepository.Named()).ToList();

        // Assert - only 2 named people
        Assert.That(people, Has.Exactly(2).Items);
        Assert.That(people.Any(p => p.FirstName == "John" && p.LastName == "Smith" && p.Enabled), Is.True);
        Assert.That(people.Any(p => p.FirstName == "Jane" && p.LastName == "Doe" && !p.Enabled), Is.True);
    }

    [Test]
    public async Task UpdateLastEvent()
    {
        // Arrange
        var credential = new Credential { Number = "2345342", LastEvent = 4 };
        var credentialRepository = new CredentialRepository(_dataAccess);
        int credentialId = await credentialRepository.Insert(credential);

        // Act
        await credentialRepository.UpdateLastEvent(credentialId, 5);

        // Assert
        var actualCredential = await credentialRepository.Get(credentialId);
        Assert.That(actualCredential.LastEvent, Is.EqualTo(5));
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
