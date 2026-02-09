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
        return await _credentialRepository.Unassigned();
    }

    public async Task Enroll(int credentialId, int personId)
    {
        // Get the swipe credential (has Number)
        var swipeCredential = await _credentialRepository.Get(credentialId);
        if (swipeCredential == null) return;

        // Get the person credential (may not have a Number yet)
        var personCredential = await _credentialRepository.Get(personId);
        if (personCredential == null) return;

        // Get Z9 Creds for both before deleting
        var swipeZ9Cred = await _z9CredRepository.Get(credentialId);
        var personZ9Cred = await _z9CredRepository.Get(personId);

        // Delete the swipe credential first (unique index on Number prevents duplicate)
        await _z9CredRepository.Delete(credentialId);
        await _credentialRepository.Delete(credentialId);

        // Transfer Number from swipe credential to person credential
        personCredential.Number = swipeCredential.Number;
        await _credentialRepository.Update(personCredential);

        // Merge card data from swipe Z9 Cred into person Z9 Cred
        if (personZ9Cred != null && swipeZ9Cred != null)
        {
            if (swipeZ9Cred.CardPin != null)
            {
                personZ9Cred.CardPin = swipeZ9Cred.CardPin;
            }
            personZ9Cred.PrivBindings.Add(swipeZ9Cred.PrivBindings);
            await _z9CredRepository.Upsert(personZ9Cred);
        }

        await _hubContext.Clients.All.SendAsync(Methods.PersonUpdated, personId);
    }

    public async Task<IEnumerable<Credential>> GetAssigned()
    {
        return await _credentialRepository.Assigned();
    }
}
