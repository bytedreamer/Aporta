using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;

using Aporta.Shared.Models;

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