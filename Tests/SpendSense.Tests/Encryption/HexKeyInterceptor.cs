using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SpendSense.Tests.Encryption;

/// <summary>
/// Spike stand-in for a real device-key interceptor (Milo's VaultKeyInterceptor): applies a raw 256-bit key with
/// <c>PRAGMA hexkey</c> as the first statement on every connection EF opens.
/// </summary>
sealed class HexKeyInterceptor(string hexKey) : DbConnectionInterceptor
{
    public int Applied { get; private set; }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = SqlCipher.HexKey(hexKey);
        command.ExecuteNonQuery();
        Applied++;
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlCipher.HexKey(hexKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
        Applied++;
    }
}
