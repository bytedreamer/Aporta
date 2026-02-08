using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("flex/credTemplate")]
public class FlexCredTemplateController : FlexCrudControllerBase<CredTemplate, FlexCredTemplate>
{
    private readonly CredTemplateRepository _repository;

    public FlexCredTemplateController(IDataAccess dataAccess)
    {
        _repository = new CredTemplateRepository(dataAccess);
    }

    protected override ProtoJsonRepository<CredTemplate> Repository => _repository;
    protected override FlexCredTemplate ToFlex(CredTemplate proto) => FlexMapper.ToFlex(proto);
    protected override CredTemplate ToProto(FlexCredTemplate flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexCredTemplate flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(CredTemplate proto) => proto.Uuid;
}
