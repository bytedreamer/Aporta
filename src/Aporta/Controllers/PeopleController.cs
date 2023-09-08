using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;

using Aporta.Shared.Models;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PeopleController
{
    private readonly PeopleService _peopleService;

    public PeopleController(PeopleService peopleService)
    {
        _peopleService = peopleService;
    }
    
    [HttpGet]
    public async Task<IEnumerable<Person>> Get()
    {
        return await _peopleService.GetAll();
    }
    
    [HttpGet("{personId}")]
    public async Task<Person> Get(int personId)
    {
        return await _peopleService.Get(personId);
    }

    [HttpPut]
    public async Task Put([FromBody]Person person)
    {
        await _peopleService.Insert(person);
    }
    
    [HttpDelete("{personId:int}")]
    public async Task Delete(int personId)
    {
        await _peopleService.Delete(personId);
    }
}