using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Hubs;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Z9.Protobuf;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("cred")]
public class FlexCredController : FlexCrudControllerBase<Cred, FlexCred>
{
    private readonly Z9CredRepository _repository;
    private readonly CredTemplateRepository _credTemplateRepository;
    private readonly Z9EvtRepository _evtRepository;
    private readonly Z9DevRepository _devRepository;
    private readonly IHubContext<DataChangeNotificationHub> _hubContext;

    public FlexCredController(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext)
    {
        _repository = new Z9CredRepository(dataAccess);
        _credTemplateRepository = new CredTemplateRepository(dataAccess);
        _evtRepository = new Z9EvtRepository(dataAccess);
        _devRepository = new Z9DevRepository(dataAccess);
        _hubContext = hubContext;
    }

    protected override ProtoJsonRepository<Cred> Repository => _repository;
    protected override FlexCred ToFlex(Cred proto) => FlexMapper.ToFlex(proto);
    protected override Cred ToProto(FlexCred flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexCred flex) => flex.Unid ?? 0;
    protected override void SetUnid(FlexCred flex, int unid) => flex.Unid = unid;
    protected override string GetUuidFromProto(Cred proto) => proto.Uuid;

    public override async Task<IActionResult> Save([FromBody] FlexCred body)
    {
        if (body.Unid == null || body.Unid == 0)
            body.Unid = await _repository.NextId();

        if (!string.IsNullOrEmpty(body.Name))
        {
            await EnsureDefaultCredTemplate();
            body.CredTemplate ??= new FlexObjRef { Unid = 1 };
        }

        var proto = ToProto(body);
        SpCoreProtoUtil.InitRequired(proto);
        await _repository.Upsert(proto);

        if (!string.IsNullOrEmpty(body.Name))
            await _hubContext.Clients.All.SendAsync(Methods.PersonInserted, body.Unid);

        return Ok(new FlexInstanceResponse<FlexCred> { Instance = ToFlex(proto) });
    }

    public override async Task<IActionResult> Delete(string id)
    {
        var result = await base.Delete(id);

        if (int.TryParse(id, out var unid))
            await _hubContext.Clients.All.SendAsync(Methods.PersonDeleted, unid);

        return result;
    }

    [HttpPost("{credentialId:int}/enroll/{personId:int}")]
    public async Task<IActionResult> Enroll(int credentialId, int personId)
    {
        var swipeZ9Cred = await _repository.Get(credentialId);
        var personZ9Cred = await _repository.Get(personId);

        if (swipeZ9Cred == null || personZ9Cred == null)
            return NotFound();

        if (swipeZ9Cred.CardPin != null)
            personZ9Cred.CardPin = swipeZ9Cred.CardPin;

        personZ9Cred.PrivBindings.Add(swipeZ9Cred.PrivBindings);

        await _repository.Delete(credentialId);
        await _repository.Upsert(personZ9Cred);

        await _hubContext.Clients.All.SendAsync(Methods.PersonUpdated, personId);

        return Ok(new FlexVoid());
    }

    [HttpPost("{personId:int}/enroll-from-read/{evtId:int}")]
    public async Task<IActionResult> EnrollFromRead(int personId, int evtId)
    {
        var personZ9Cred = await _repository.Get(personId);
        if (personZ9Cred == null)
            return NotFound("Person credential not found");

        var evt = await _evtRepository.Get(evtId);
        if (evt == null || evt.EvtCode != EvtCode.RawCredRead)
            return NotFound("Raw read event not found");

        var cardData = evt.Data;
        if (string.IsNullOrEmpty(cardData))
            return BadRequest("Raw read event has no card data");

        personZ9Cred.CardPin = new CardPin
        {
            CredNum = SpCoreProtoUtil.ToBigIntegerData(BigInteger.Parse(cardData))
        };

        if (evt.EvtDevRef?.UnidCase == EvtDevRef.UnidOneofCase.Unid)
        {
            var readerDev = await _devRepository.Get(evt.EvtDevRef.Unid);
            if (readerDev?.LogicalParentUnidCase == Dev.LogicalParentUnidOneofCase.LogicalParentUnid)
            {
                var doorDevUnid = readerDev.LogicalParentUnid;
                var parentDev = await _devRepository.Get(doorDevUnid);
                if (parentDev?.LogicalParentUnidCase == Dev.LogicalParentUnidOneofCase.LogicalParentUnid)
                {
                    var grandparent = await _devRepository.Get(parentDev.LogicalParentUnid);
                    if (grandparent?.DevType == DevType.Door)
                        doorDevUnid = grandparent.Unid;
                }

                personZ9Cred.PrivBindings.Add(new CredPrivBinding
                {
                    DevAsDoorAccessPrivUnid = doorDevUnid
                });
            }
        }

        SpCoreProtoUtil.InitRequired(personZ9Cred);
        await _repository.Upsert(personZ9Cred);

        await _evtRepository.MarkConsumed(evtId);

        await _hubContext.Clients.All.SendAsync(Methods.PersonUpdated, personId);

        return Ok(new FlexVoid());
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
