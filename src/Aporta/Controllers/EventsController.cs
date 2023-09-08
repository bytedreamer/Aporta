using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Aporta.Shared.Models;
using Aporta.Core.Services;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController
{
    private readonly EventService _eventService;

    public EventsController(EventService eventService)
    {
        _eventService = eventService;
    }
    
    [HttpGet("{eventId:int}")]
    public async Task<Event> Get(int eventId)
    {
        return await _eventService.Get(eventId);
    }
}