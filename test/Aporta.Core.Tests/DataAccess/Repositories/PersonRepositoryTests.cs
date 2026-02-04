using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models;
using Aporta.Shared.Models;
using NUnit.Framework;

namespace Aporta.Core.Tests.DataAccess.Repositories;

public class PersonRepositoryTests
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
    public async Task Insert()
    {
        // Arrange
        var people = new[]
        {
            new Person {FirstName = "First1", LastName = "Last1", Enabled = false},
            new Person {FirstName = "First2", LastName = "Last2", Enabled = true},
        };

        var personRepository = new PersonRepository(_dataAccess);
        foreach (var person in people)
        {
            await personRepository.Insert(person);
        }

        // Act 
        var actualPerson = await personRepository.Get(2);

        // Assert
        Assert.That(people[1].Id, Is.EqualTo(2));
        Assert.That(actualPerson.Id, Is.EqualTo(2));
        Assert.That(actualPerson.FirstName, Is.EqualTo("First2"));
        Assert.That(actualPerson.LastName, Is.EqualTo("Last2"));
        Assert.That(actualPerson.Enabled, Is.True);
    }

    [Test]
    public async Task Delete()
    {
        // Arrange
        var people = new[]
        {
            new Person {FirstName = "First1", LastName = "Last1", Enabled = false},
            new Person {FirstName = "First2", LastName = "Last2", Enabled = true},
        };

        var personRepository = new PersonRepository(_dataAccess);
        foreach (var person in people)
        {
            await personRepository.Insert(person);
        }

        // Act
        await personRepository.Delete(2);

        // Assert
        var actualPeople = await personRepository.GetAll();
        Assert.That(1, Is.EqualTo(actualPeople.Count()));
    }

    [Test]
    public async Task Insert_WithExplicitId()
    {
        // Arrange
        var personRepository = new PersonRepository(_dataAccess);
        var person = new Person { FirstName = "Explicit", LastName = "IdPerson", Enabled = true };

        // Act
        int id = await personRepository.Insert(person, explicitId: 100);

        // Assert
        Assert.That(id, Is.EqualTo(100));
        Assert.That(person.Id, Is.EqualTo(100));

        var retrieved = await personRepository.Get(100);
        Assert.That(retrieved.FirstName, Is.EqualTo("Explicit"));
        Assert.That(retrieved.LastName, Is.EqualTo("IdPerson"));
    }

    [Test]
    public async Task Insert_WithExplicitIds_OutOfOrder()
    {
        // Arrange - simulates upstream data arriving out of order
        var personRepository = new PersonRepository(_dataAccess);

        // Act - insert IDs out of sequence
        await personRepository.Insert(new Person { FirstName = "Third", LastName = "Person", Enabled = true }, explicitId: 300);
        await personRepository.Insert(new Person { FirstName = "First", LastName = "Person", Enabled = true }, explicitId: 100);
        await personRepository.Insert(new Person { FirstName = "Second", LastName = "Person", Enabled = true }, explicitId: 200);

        // Assert
        var all = (await personRepository.GetAll()).ToList();
        Assert.That(all.Count, Is.EqualTo(3));
        Assert.That((await personRepository.Get(100)).FirstName, Is.EqualTo("First"));
        Assert.That((await personRepository.Get(200)).FirstName, Is.EqualTo("Second"));
        Assert.That((await personRepository.Get(300)).FirstName, Is.EqualTo("Third"));
    }

    [Test]
    public async Task Upsert_InsertsNewRecord()
    {
        // Arrange
        var personRepository = new PersonRepository(_dataAccess);
        var person = new Person { FirstName = "New", LastName = "Person", Enabled = true };

        // Act
        await personRepository.Upsert(person, 50);

        // Assert
        var retrieved = await personRepository.Get(50);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.FirstName, Is.EqualTo("New"));
        Assert.That(person.Id, Is.EqualTo(50));
    }

    [Test]
    public async Task Upsert_UpdatesExistingRecord()
    {
        // Arrange
        var personRepository = new PersonRepository(_dataAccess);
        var original = new Person { FirstName = "Original", LastName = "Name", Enabled = false };
        await personRepository.Insert(original, explicitId: 75);

        // Act
        var updated = new Person { FirstName = "Updated", LastName = "Name", Enabled = true };
        await personRepository.Upsert(updated, 75);

        // Assert
        var retrieved = await personRepository.Get(75);
        Assert.That(retrieved.FirstName, Is.EqualTo("Updated"));
        Assert.That(retrieved.Enabled, Is.True);

        var all = await personRepository.GetAll();
        Assert.That(all.Count(), Is.EqualTo(1));
    }
}