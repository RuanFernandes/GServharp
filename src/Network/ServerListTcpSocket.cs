using System.Net;
using System.Net.Sockets;
using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed class ServerListTcpSocket : IServerListSocket, IServerListGateway, IDisposable
{
    private readonly GraalFileQueue _queue = new();
    private readonly ServerListReceiveBuffer _receiveBuffer = new();
    private readonly byte[] _readBuffer = new byte[0x8000];
    private string _host = "";
    private int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public bool IsConnected => _client?.Connected == true;

    public string LocalIp =>
        _client?.Client.LocalEndPoint is IPEndPoint endpoint
            ? endpoint.Address.ToString()
            : string.Empty;

    public bool Initialize(string host, string port)
    {
        if (!int.TryParse(port, out var parsedPort) || parsedPort < IPEndPoint.MinPort || parsedPort > IPEndPoint.MaxPort)
            return false;

        _host = host;
        _port = parsedPort;
        return true;
    }

    public bool Connect()
    {
        if (IsConnected)
            return true;

        try
        {
            _client = new TcpClient();
            _client.NoDelay = true;
            _client.Connect(_host, _port);
            _stream = _client.GetStream();
            return true;
        }
        catch (SocketException)
        {
            DisposeClient();
            return false;
        }
        catch (IOException)
        {
            DisposeClient();
            return false;
        }
    }

    public void Register()
    {
    }

    public void ClearOutgoingBuffers()
    {
    }

    public void SetCodec(EncryptionGeneration generation, byte key)
    {
        _queue.SetCodec(generation, key);
    }

    public void SendPacket(byte[] packetBody, bool sendNow = false)
    {
        if (_stream is null)
            throw new InvalidOperationException("Server-list socket must be connected before sending packets.");

        var packet = packetBody.Length > 0 && packetBody[^1] == (byte)'\n'
            ? packetBody
            : [..packetBody, (byte)'\n'];
        _queue.AddRawPacket(packet);
        var bytes = _queue.FlushSocket(forceSendFiles: sendNow);
        if (bytes.Length == 0)
            return;

        _stream.Write(bytes);
    }

    public void SendLoginPacketForPlayer(byte[] packetBody)
    {
        SendPacket(packetBody, sendNow: true);
    }

    public void SendPlayerAdd(byte[] packetBody)
    {
        SendPacket(packetBody, sendNow: true);
    }

    public void SendPlayerRemove(byte[] packetBody)
    {
        SendPacket(packetBody, sendNow: true);
    }

    public void SendServerInfoForPlayer(byte[] packetBody)
    {
        SendPacket(packetBody, sendNow: true);
    }

    public async ValueTask<IReadOnlyList<byte[]>> ReceivePacketsAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
            throw new InvalidOperationException("Server-list socket must be connected before receiving packets.");

        var read = await _stream.ReadAsync(_readBuffer, cancellationToken);
        if (read == 0)
        {
            DisposeClient();
            return [];
        }

        _receiveBuffer.Append(_readBuffer.AsSpan(0, read));
        return _receiveBuffer.DrainPackets();
    }

    public void Dispose()
    {
        DisposeClient();
    }

    private void DisposeClient()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }
}
