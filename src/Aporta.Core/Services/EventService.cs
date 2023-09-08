using System.Threading.Tasks;

using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models;

namespace Aporta.Core.Services;

public class EventService
{
    private readonly EventRepository _eventRepository;

    public EventService(IDataAccess dataAccess)
    {
        _eventRepository = new EventRepository(dataAccess);
    }
    
    public async Task<Event> Get(int eventId)
    {
        return await _eventRepository.Get(eventId);
    }
}