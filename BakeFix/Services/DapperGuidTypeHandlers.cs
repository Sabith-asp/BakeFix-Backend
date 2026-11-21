using Dapper;
using System.Data;

namespace BakeFix.Services
{
    /// <summary>
    /// MySql.Data's Dapper integration transparently converted CHAR(36) columns
    /// to <see cref="Guid"/>; Npgsql returns them as plain <see cref="string"/>,
    /// which Dapper cannot convert on its own (throws InvalidCastException).
    /// Registered once at startup in Program.cs.
    /// </summary>
    public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value.ToString();

        public override Guid Parse(object value) => value switch
        {
            Guid g => g,
            string s => Guid.Parse(s),
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType().Name} to Guid")
        };
    }

    public class NullableGuidTypeHandler : SqlMapper.TypeHandler<Guid?>
    {
        public override void SetValue(IDbDataParameter parameter, Guid? value) =>
            parameter.Value = value is null ? DBNull.Value : value.Value.ToString();

        public override Guid? Parse(object value) => value switch
        {
            null or DBNull => null,
            Guid g => g,
            string s => Guid.Parse(s),
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType().Name} to Guid")
        };
    }
}
