using System.Data;
using Dapper;

namespace FaizMawaid.Data
{
    /// <summary>
    /// Dapper does not reliably support DateOnly for MySqlConnector parameters/results
    /// out of the box -- it throws "The member X of type System.DateOnly cannot be used
    /// as a parameter value" the moment a DateOnly property is used as a query
    /// parameter. This handler converts DateOnly to/from the DateTime that
    /// MySqlConnector actually sends/returns for a DATE column. Registered once in
    /// Program.cs; also covers DateOnly? parameters, since Dapper resolves a nullable
    /// value type to its underlying type when looking up a registered handler.
    /// </summary>
    public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value)
        {
            return value switch
            {
                DateTime dt => DateOnly.FromDateTime(dt),
                DateOnly d => d,
                _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
            };
        }

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }
}
