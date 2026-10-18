using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

using SpendSense.Common.Security;

namespace SpendSense.Common.Data.Interceptors;

/// <summary>
/// Applies the device key as the first statement on every connection EF opens (ADR 0004). It runs on every
/// logical open, including pooled handles, which is cheap with a raw key.
/// </summary>
public sealed class DatabaseKeyInterceptor(DatabaseKey key) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SqliteCipher.KeyPragma(key.Value);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqliteCipher.KeyPragma(key.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
