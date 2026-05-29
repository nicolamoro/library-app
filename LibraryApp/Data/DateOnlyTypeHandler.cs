using System.Data;
using Dapper;

namespace LibraryApp.Data;

// Npgsql returns `date` columns as DateTime by default, which Dapper cannot
// cast to DateOnly. This handler bridges DateOnly <-> the DB date type for both
// reading and writing.
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
}
