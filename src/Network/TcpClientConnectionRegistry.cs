using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Preagonal.GServer.Network;

public sealed class TcpClientConnectionRegistry
{
    private readonly ConcurrentDictionary<ushort, NetworkStream> _streams = [];

    public void Register(ushort playerId, NetworkStream stream)
    {
        _streams[playerId] = stream;
    }

    public void Unregister(ushort playerId)
    {
        _streams.TryRemove(playerId, out _);
    }

    public async ValueTask<bool> SendAsync(
        ushort playerId,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        if (!_streams.TryGetValue(playerId, out var stream))
            return false;

        await stream.WriteAsync(bytes, cancellationToken);
        return true;
    }
}
