using GServ.Protocol;

namespace GServ.Network;

public sealed record ProductionServerListConnectOptions(
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

public sealed record ProductionServerListConnectResult(bool Connected);

public interface IProductionServerListSocket
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

public sealed class ProductionServerListLifecycle
{
    private readonly IProductionServerListSocket _socket;

    public ProductionServerListLifecycle(IProductionServerListSocket socket)
    {
        _socket = socket;
    }

    public ProductionServerListConnectResult ConnectServer(ProductionServerListConnectOptions options)
    {
        if (_socket.IsConnected)
            return new ProductionServerListConnectResult(true);

        if (!_socket.Initialize(options.ListIp, options.ListPort))
            return new ProductionServerListConnectResult(false);

        if (!_socket.Connect())
            return new ProductionServerListConnectResult(false);

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
        _socket.SendPacket(ServerListAuthPackets.ServerHqLevel(options.OnlyStaff, options.HqLevel));
        _socket.SendPacket(ServerListAuthPackets.AllowedVersionsText(options.AllowedVersions));
        _socket.SendPacket(ServerListAuthPackets.SetPlayers());

        return new ProductionServerListConnectResult(_socket.IsConnected);
    }

    private static string ResolveLocalIp(string configuredLocalIp, string socketLocalIp)
    {
        var localIp = string.IsNullOrEmpty(configuredLocalIp) || configuredLocalIp == "AUTO"
            ? socketLocalIp
            : configuredLocalIp;

        return localIp is "127.0.1.1" or "127.0.0.1" ? string.Empty : localIp;
    }
}
