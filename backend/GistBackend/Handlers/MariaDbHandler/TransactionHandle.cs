using System.Data.Common;

namespace GistBackend.Handlers.MariaDbHandler;

public class TransactionHandle(DbConnection connection, DbTransaction transaction)
{
    public DbConnection Connection { get; } = connection;
    public DbTransaction Transaction { get; } = transaction;

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
