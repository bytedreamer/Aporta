using System;
using System.Data;
using Dapper;

namespace Aporta.Core.DataAccess.Repositories
{
    internal abstract class SqLiteTypeHandler<T> : SqlMapper.TypeHandler<T>
    {
        public override void SetValue(IDbDataParameter parameter, T value)
            => parameter.Value = value;
    }
        
    internal class GuidHandler : SqLiteTypeHandler<Guid>
    {
        public override Guid Parse(object value)
            => Guid.Parse((string)value);
    }
}