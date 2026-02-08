using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("flex/dataFormat")]
public class FlexDataFormatController : FlexCrudControllerBase<DataFormat, FlexDataFormat>
{
    private readonly DataFormatRepository _repository;

    public FlexDataFormatController(IDataAccess dataAccess)
    {
        _repository = new DataFormatRepository(dataAccess);
    }

    protected override ProtoJsonRepository<DataFormat> Repository => _repository;
    protected override FlexDataFormat ToFlex(DataFormat proto) => FlexMapper.ToFlex(proto);
    protected override DataFormat ToProto(FlexDataFormat flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexDataFormat flex) => flex.Unid ?? 0;
    protected override string GetUuidFromProto(DataFormat proto) => proto.Uuid;
}
