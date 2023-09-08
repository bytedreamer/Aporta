using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;

using Aporta.Shared.Models;

namespace Aporta.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CredentialsController
{
    private readonly CredentialService _credentialService;

    public CredentialsController(CredentialService credentialService)
    {
        _credentialService = credentialService;
    }
    
    [HttpGet]
    public async Task<IEnumerable<Credential>> Get()
    {
        return await _credentialService.GetAll();
    }
    
    [HttpGet("{credentialId:int}")]
    public async Task<Credential> Get(int credentialId)
    {
        return await _credentialService.Get(credentialId);
    }

    [HttpPut]
    public async Task Put([FromBody]Credential credential)
    {
        await _credentialService.Insert(credential);
    }
    
    [HttpDelete("{credentialId:int}")]
    public async Task Delete(int credentialId)
    {
        await _credentialService.Delete(credentialId);
    }
    
    [HttpGet("assigned")]
    public async Task<IEnumerable<Credential>> GetAssigned()
    {
        return await _credentialService.GetAssigned();
    }
    
    [HttpGet("unassigned")]
    public async Task<IEnumerable<Credential>> GetUnassigned()
    {
        return await _credentialService.GetUnassigned();
    }
    
    [HttpPost("{credentialId:int}/enroll/{personId:int}")]
    public async Task Enroll(int credentialId, int personId)
    {
        await _credentialService.Enroll(credentialId, personId);
    }
}