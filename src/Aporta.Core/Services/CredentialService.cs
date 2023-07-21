using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace Aporta.Core.Services;

public class CredentialService
{
    private readonly CredentialRepository _credentialRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    public CredentialService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext)
    {
        _hubContext = hubContext;
        _credentialRepository = new CredentialRepository(dataAccess);
    }
    
    public async Task<IEnumerable<Credential>> GetAll()
    {
        return await _credentialRepository.GetAll();
    }
    
    public async Task<Credential> Get(int credentialId)
    {
        return await _credentialRepository.Get(credentialId);
    }
    
    public async Task Insert(Credential credential)
    {
        await _credentialRepository.Insert(credential);
            
        await _hubContext.Clients.All.SendAsync(Methods.CredentialInserted, credential.Id);
    }
    
    public async Task Delete(int id)
    {
        await _credentialRepository.Delete(id);
            
        await _hubContext.Clients.All.SendAsync(Methods.CredentialDeleted, id);
    }

    public async Task<IEnumerable<Credential>> GetUnassigned()
    {
        return await _credentialRepository.UnassignedCredentials();
    }
}