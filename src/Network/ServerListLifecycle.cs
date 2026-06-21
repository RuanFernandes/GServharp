using Preagonal.GServer.Protocol;

namespace Preagonal.GServer.Network;

public sealed record ServerListConnectOptions(
    string ListIp,
    string ListPort,
    string Name,
    string Description,
    string Language,
    string Version,
    string Url,
    string ServerIp,
    string ServerPort,
    string LocalIp,
    string HqPassword,
    int HqLevel,
    bool OnlyStaff,
    IReadOnlyList<string> AllowedVersions);

public sealed record ServerListConnectResult(bool Connected);

public interface IServerListSocket
{
    bool IsConnected { get; }
    string LocalIp { get; }
    bool Initialize(string host, string port);
    bool Connect();
    void Register();
    void ClearOutgoingBuffers();
    void SetCodec(EncryptionGeneration generation, byte key);
    void SendPacket(byte[] packetBody, bool sendNow = false);
}

public sealed class ServerListLifecycle
{
    private readonly IServerListSocket _socket;

    public ServerListLifecycle(IServerListSocket socket)
    {
        _socket = socket;
    }

    public ServerListConnectResult ConnectServer(ServerListConnectOptions options)
    {
        if (_socket.IsConnected)
            return new ServerListConnectResult(true);

        if (!_socket.Initialize(options.ListIp, options.ListPort))
            return new ServerListConnectResult(false);

        if (!_socket.Connect())
            return new ServerListConnectResult(false);

        _socket.Register();

        var localIp = ResolveLocalIp(options.LocalIp, _socket.LocalIp);

        _socket.ClearOutgoingBuffers();
        _socket.SetCodec(EncryptionGeneration.Gen1, key: 0);
        _socket.SendPacket(ServerListAuthPackets.RegisterV3(options.Version), sendNow: true);
        _socket.SetCodec(EncryptionGeneration.Gen2, key: 0);
        _socket.SendPacket(ServerListAuthPackets.ServerHqPass(options.HqPassword));
        _socket.SendPacket(ServerListAuthPackets.NewServer(
            options.Name,
            options.Description,
            options.Language,
            options.Version,
            options.Url,
            options.ServerIp,
            options.ServerPort,
            localIp));
        _socket.SendPacket(ServerListAuthPackets.ServerHqLevel(options.OnlyStaff, options.HqLevel), sendNow: true);
        _socket.SendPacket(ServerListAuthPackets.AllowedVersionsText(options.AllowedVersions), sendNow: true);
        _socket.SendPacket(ServerListAuthPackets.SetPlayers(), sendNow: true);

        return new ServerListConnectResult(_socket.IsConnected);
    }

    private static string ResolveLocalIp(string configuredLocalIp, string socketLocalIp)
    {
        var localIp = string.IsNullOrEmpty(configuredLocalIp) || configuredLocalIp == "AUTO"
            ? socketLocalIp
            : configuredLocalIp;

        return localIp is "127.0.1.1" or "127.0.0.1" ? string.Empty : localIp;
    }
}
