using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("flex/doorAccessPriv")]
public class FlexDoorAccessPrivController : FlexCrudControllerBase<Priv, FlexPriv>
{
    private readonly PrivRepository _repository;

    public FlexDoorAccessPrivController(IDataAccess dataAccess)
    {
        _repository = new PrivRepository(dataAccess);
    }

    protected override ProtoJsonRepository<Priv> Repository => _repository;
    protected override FlexPriv ToFlex(Priv proto) => FlexMapper.ToFlex(proto);
    protected override Priv ToProto(FlexPriv flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexPriv flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(Priv proto) => proto.Uuid;
}
