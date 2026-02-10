using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("sched")]
public class FlexSchedController : FlexCrudControllerBase<Sched, FlexSched>
{
    private readonly SchedRepository _repository;

    public FlexSchedController(IDataAccess dataAccess)
    {
        _repository = new SchedRepository(dataAccess);
    }

    protected override ProtoJsonRepository<Sched> Repository => _repository;
    protected override FlexSched ToFlex(Sched proto) => FlexMapper.ToFlex(proto);
    protected override Sched ToProto(FlexSched flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexSched flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(Sched proto) => proto.Uuid;
}
