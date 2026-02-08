using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Models.Flex;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;

namespace Aporta.Controllers.Flex;

public abstract class FlexCrudControllerBase<TProto, TFlex> : ControllerBase
    where TProto : IMessage<TProto>, new()
{
    protected abstract ProtoJsonRepository<TProto> Repository { get; }
    protected abstract TFlex ToFlex(TProto proto);
    protected abstract TProto ToProto(TFlex flex);
    protected abstract int GetUnid(TFlex flex);

    protected virtual async Task<TProto> FindByIdString(string id)
    {
        if (int.TryParse(id, out var unid))
            return await Repository.Get(unid);

        // Try uuid lookup
        var all = await Repository.GetAll();
        return all.FirstOrDefault(item => GetUuidFromProto(item) == id);
    }

    protected virtual string GetUuidFromProto(TProto proto) => null;

    [HttpGet("list")]
    public virtual async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int max = 50)
    {
        var all = (await Repository.GetAll()).ToList();
        var count = all.Count;
        var page = all.Skip(offset).Take(max).Select(ToFlex).ToList();

        return Ok(new FlexListResponse<TFlex>
        {
            Offset = offset,
            Max = max,
            Count = count,
            InstanceList = page,
        });
    }

    [HttpGet("show/{id}")]
    public virtual async Task<IActionResult> Show(string id)
    {
        var proto = await FindByIdString(id);
        if (proto == null)
            return NotFound();

        return Ok(new FlexInstanceResponse<TFlex> { Instance = ToFlex(proto) });
    }

    [HttpPost("save")]
    public virtual async Task<IActionResult> Save([FromBody] TFlex body)
    {
        var proto = ToProto(body);
        await Repository.Upsert(proto);
        return Ok(new FlexInstanceResponse<TFlex> { Instance = ToFlex(proto) });
    }

    [HttpPost("update/{id}")]
    public virtual async Task<IActionResult> Update(string id, [FromBody] TFlex body)
    {
        var existing = await FindByIdString(id);
        if (existing == null)
            return NotFound();

        var proto = ToProto(body);
        await Repository.Upsert(proto);
        return Ok(new FlexInstanceResponse<TFlex> { Instance = ToFlex(proto) });
    }

    [HttpPost("delete/{id}")]
    public virtual async Task<IActionResult> Delete(string id)
    {
        if (int.TryParse(id, out var unid))
        {
            await Repository.Delete(unid);
            return Ok(new FlexVoid());
        }

        var proto = await FindByIdString(id);
        if (proto == null)
            return NotFound();

        await Repository.Delete(GetUnid(ToFlex(proto)));
        return Ok(new FlexVoid());
    }
}
