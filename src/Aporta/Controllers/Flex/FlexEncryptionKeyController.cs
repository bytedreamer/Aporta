using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Shared.Models.Flex;
using Aporta.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Z9.Spcore.Proto;

namespace Aporta.Controllers.Flex;

[ApiController]
[Route("encryptionKey")]
public class FlexEncryptionKeyController : FlexCrudControllerBase<EncryptionKey, FlexEncryptionKey>
{
    private readonly EncryptionKeyRepository _repository;

    public FlexEncryptionKeyController(IDataAccess dataAccess)
    {
        _repository = new EncryptionKeyRepository(dataAccess);
    }

    protected override ProtoJsonRepository<EncryptionKey> Repository => _repository;
    protected override FlexEncryptionKey ToFlex(EncryptionKey proto) => FlexMapper.ToFlex(proto);
    protected override EncryptionKey ToProto(FlexEncryptionKey flex) => FlexMapper.ToProto(flex);
    protected override int GetUnid(FlexEncryptionKey flex) => flex.Unid ?? 0;
    protected override void SetUnid(FlexEncryptionKey flex, int unid) => flex.Unid = unid;
    protected override string GetUuidFromProto(EncryptionKey proto) => proto.Uuid;
}
