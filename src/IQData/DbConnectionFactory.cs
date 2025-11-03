using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IQData;

public sealed class DbConnectionFactory
{
    private readonly string _cs;
    public DbConnectionFactory(IOptions<DbSettings> opt) => _cs = opt.Value.Default!;
    public DbConnection Create() => new SqlConnection(_cs);
}