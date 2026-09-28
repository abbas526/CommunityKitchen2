using System.Data;
using Dapper;

namespace FaizMawaid.Data
{
    /// <summary>
    /// MySqlConnector hands back our IsActive-style TINYINT UNSIGNED columns as a raw
    /// numeric type (byte/sbyte/long depending on context), not bool -- MySQL only gets
    /// auto-mapped to bool for the old, now-deprecated TINYINT(1) display-width syntax,
    /// which this schema deliberately does not use. This handler lets model properties
    /// stay a clean `bool` regardless of exactly which numeric type comes back.
    /// Registered once in Program.cs via SqlMapper.AddTypeHandler.
    /// </summary>
    public class BooleanTypeHandler : SqlMapper.TypeHandler<bool>
    {
        public override bool Parse(object value)
        {
            return value switch
            {
                bool b => b,
                null => false,
                _ => Convert.ToInt64(value) != 0
            };
        }

        public override void SetValue(IDbDataParameter parameter, bool value)
        {
            parameter.Value = value ? 1 : 0;
            parameter.DbType = DbType.Int32;
        }
    }
}
