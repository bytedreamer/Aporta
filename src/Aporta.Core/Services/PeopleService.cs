using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Core.Services;

public class PeopleService
{
    private readonly CredentialRepository _credentialRepository;
    private readonly Z9CredRepository _z9CredRepository;
    private readonly CredTemplateRepository _credTemplateRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    public PeopleService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext)
    {
        _hubContext = hubContext;
        _credentialRepository = new CredentialRepository(dataAccess);
        _z9CredRepository = new Z9CredRepository(dataAccess);
        _credTemplateRepository = new CredTemplateRepository(dataAccess);
    }


    public async Task<IEnumerable<Person>> GetAll()
    {
        return await _credentialRepository.Named();
    }

    public async Task<Person> Get(int personId)
    {
        var z9Cred = await _z9CredRepository.Get(personId);
        if (z9Cred == null) return null;

        var enabled = z9Cred.EnabledCase == Cred.EnabledOneofCase.Enabled ? z9Cred.Enabled : true;
        return CredentialRepository.ParsePersonFromName(z9Cred.Name, personId, enabled);
    }

    public async Task Insert(Person person)
    {
        // Get next available ID
        var credentialId = await _z9CredRepository.NextId();

        // Ensure default cred template exists
        await EnsureDefaultCredTemplate();

        // Create Z9 Cred with name="Last, First" and enabled=true
        var name = !string.IsNullOrWhiteSpace(person.LastName)
            ? $"{person.LastName}, {person.FirstName}"
            : person.FirstName;

        var cred = new Cred
        {
            Unid = credentialId,
            Name = name,
            Enabled = person.Enabled,
            CredTemplateUnid = 1
        };
        SpCoreProtoUtil.InitRequired(cred);
        await _z9CredRepository.Upsert(cred);

        person.Id = credentialId;
        await _hubContext.Clients.All.SendAsync(Methods.PersonInserted, person.Id);
    }

    public async Task Delete(int personId)
    {
        await _z9CredRepository.Delete(personId);

        await _hubContext.Clients.All.SendAsync(Methods.PersonDeleted, personId);
    }

    private async Task EnsureDefaultCredTemplate()
    {
        var existing = await _credTemplateRepository.Get(1);
        if (existing != null) return;

        var credTemplate = new CredTemplate
        {
            Unid = 1,
            Name = "Card (Auto)",
            Priority = 0,
            CardPinTemplate = new CardPinTemplate
            {
                CredComponentPresence = CredComponentPresence.Required,
                CredNumPresence = CredComponentPresence.Required,
                PinPresence = CredComponentPresence.Absent,
                AnyDataLayout = true
            }
        };
        SpCoreProtoUtil.InitRequired(credTemplate);
        await _credTemplateRepository.Upsert(credTemplate);
    }
}
