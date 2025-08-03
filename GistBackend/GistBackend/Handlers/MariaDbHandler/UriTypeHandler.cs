using System.Data;
using Dapper;

namespace GistBackend.Handlers.MariaDbHandler;

public class UriTypeHandler : SqlMapper.TypeHandler<Uri>
{
    public override Uri? Parse(object value) =>
        value is string str && !string.IsNullOrEmpty(str) ? new Uri(str) : null;

    public override void SetValue(IDbDataParameter parameter, Uri? value) =>
        parameter.Value = value?.ToString();
}
