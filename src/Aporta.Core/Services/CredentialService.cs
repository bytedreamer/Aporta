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
    private readonly Z9CredRepository _z9CredRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    public CredentialService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext)
    {
        _hubContext = hubContext;
        _credentialRepository = new CredentialRepository(dataAccess);
        _z9CredRepository = new Z9CredRepository(dataAccess);
    }

    public async Task<IEnumerable<Credential>> GetAll()
    {
        return await _credentialRepository.GetAll();
    }

    public async Task<Credential> Get(int credentialId)
    {
        return await _credentialRepository.Get(credentialId);
    }

    public async Task<IEnumerable<Credential>> GetUnassigned()
    {
        return await _credentialRepository.Unassigned();
    }

    public async Task Enroll(int credentialId, int personId)
    {
        // Get Z9 Creds for both
        var swipeZ9Cred = await _z9CredRepository.Get(credentialId);
        var personZ9Cred = await _z9CredRepository.Get(personId);

        if (swipeZ9Cred == null || personZ9Cred == null) return;

        // Transfer CardPin from swipe to person
        if (swipeZ9Cred.CardPin != null)
        {
            personZ9Cred.CardPin = swipeZ9Cred.CardPin;
        }

        // Merge PrivBindings
        personZ9Cred.PrivBindings.Add(swipeZ9Cred.PrivBindings);

        // Delete swipe z9_cred first (frees the unique cred_num)
        await _z9CredRepository.Delete(credentialId);

        // Upsert person z9_cred (sets cred_num from transferred CardPin)
        await _z9CredRepository.Upsert(personZ9Cred);

        await _hubContext.Clients.All.SendAsync(Methods.PersonUpdated, personId);
    }

    public async Task<IEnumerable<Credential>> GetAssigned()
    {
        return await _credentialRepository.Assigned();
    }
}
