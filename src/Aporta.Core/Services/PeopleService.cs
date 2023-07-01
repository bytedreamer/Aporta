using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Services;

public class PeopleService
{
    private readonly PersonRepository _personRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;
    private readonly ILogger<PeopleService> _logger;

    public PeopleService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
        ILogger<PeopleService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _personRepository = new PersonRepository(dataAccess);
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
    
    public async Task Delete(int id)
    {
        await _personRepository.Delete(id);
            
        await _hubContext.Clients.All.SendAsync(Methods.PersonDeleted, id);
    }
}