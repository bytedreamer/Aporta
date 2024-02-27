using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace Aporta.Core.Services;

public class PeopleService
{
    private readonly PersonRepository _personRepository;
    private readonly CredentialRepository _credentialRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    public PeopleService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext)
    {
        _hubContext = hubContext;
        _personRepository = new PersonRepository(dataAccess);
        _credentialRepository = new CredentialRepository(dataAccess);
    }
    
    public async Task<IEnumerable<Person>> GetAll()
    {
        return await _personRepository.GetAll();
    }
    
    public async Task<Person> Get(int personId)
    {
        return await _personRepository.Get(personId);
    }
    
    public async Task Insert(Person person)
    {
        await _personRepository.Insert(person);
            
        await _hubContext.Clients.All.SendAsync(Methods.PersonInserted, person.Id);
    }
    
    public async Task Delete(int personId)
    {
        var credentialsAssignedToPerson = await _credentialRepository.CredentialsAssignedToPerson(personId);
        foreach (var credential in credentialsAssignedToPerson)
        {
            await _credentialRepository.UnassignPerson(credential.Id, personId);
        }
        
        await _personRepository.Delete(personId);
            
        await _hubContext.Clients.All.SendAsync(Methods.PersonDeleted, personId);
    }
}