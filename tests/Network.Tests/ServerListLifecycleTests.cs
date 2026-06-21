using Preagonal.GServer.Protocol;
using Xunit;

namespace Preagonal.GServer.Network.Tests;

public sealed class ServerListLifecycleTests
{
    [Fact]
    public void ConnectServerSuccessUsesConfirmedRegistrationPacketOrderAndCodecSwitch()
    {
        var socket = new RecordingServerListSocket
        {
            LocalIp = "10.0.0.5"
        };
        var lifecycle = new ServerListLifecycle(socket);

        var result = lifecycle.ConnectServer(new ServerListConnectOptions(
            ListIp: "list.example.test",
            ListPort: "14900",
            Name: "Classic",
            Description: "Desc",
            Language: "English",
            Version: "3.0.9-beta",
            Url: "http://example.test/",
            ServerIp: "AUTO",
            ServerPort: "14900",
            LocalIp: "AUTO",
            HqPassword: "secret",
            HqLevel: 3,
            OnlyStaff: false,
            AllowedVersions: ["G3D0311C"]));

        Assert.True(result.Connected);
        Assert.Equal(
            [
                "init:list.example.test:14900",
                "connect",
                "register",
                "clear",
                "codec:Gen1:0",
                "sendNow:>3.0.9-beta\n",
                "codec:Gen2:0",
                "send:7secret\n",
                "send:6'Classic$Desc'English*3.0.9-beta4http://example.test/$AUTO%14900(10.0.0.5\n",
                "sendNow:8#\n",
                "sendNow:?Listserver,settings,allowedversions,G3D0311C\n",
                "sendNow:'\n"
            ],
            socket.Events);
    }

    [Theory]
    [InlineData("", "127.0.0.1", "")]
    [InlineData("AUTO", "127.0.1.1", "")]
    [InlineData("192.168.1.9", "127.0.0.1", "192.168.1.9")]
    public void ConnectServerUsesSocketLocalIpUnlessLoopbackOrExplicitLocalIp(
        string configuredLocalIp,
        string socketLocalIp,
        string expectedLocalIp)
    {
        var socket = new RecordingServerListSocket
        {
            LocalIp = socketLocalIp
        };
        var lifecycle = new ServerListLifecycle(socket);

        _ = lifecycle.ConnectServer(DefaultOptions() with
        {
            LocalIp = configuredLocalIp
        });

        Assert.Contains(
            $"send:6'Classic$Desc'English*3.0.9-beta4http://example.test/$AUTO%14900{(char)(expectedLocalIp.Length + 32)}{expectedLocalIp}\n",
            socket.Events);
    }

    [Fact]
    public void ConnectServerReturnsFalseWhenSocketInitializeFailsAndDoesNotRegister()
    {
        var socket = new RecordingServerListSocket
        {
            InitializeResult = false
        };
        var lifecycle = new ServerListLifecycle(socket);

        var result = lifecycle.ConnectServer(DefaultOptions());

        Assert.False(result.Connected);
        Assert.Equal(["init:list.example.test:14900"], socket.Events);
    }

    private static ServerListConnectOptions DefaultOptions() =>
        new(
            ListIp: "list.example.test",
            ListPort: "14900",
            Name: "Classic",
            Description: "Desc",
            Language: "English",
            Version: "3.0.9-beta",
            Url: "http://example.test/",
            ServerIp: "AUTO",
            ServerPort: "14900",
            LocalIp: "AUTO",
            HqPassword: "secret",
            HqLevel: 3,
            OnlyStaff: false,
            AllowedVersions: ["G3D0311C"]);

    private sealed class RecordingServerListSocket : IServerListSocket
    {
        public bool IsConnected { get; private set; }
        public string LocalIp { get; init; } = "";
        public bool InitializeResult { get; init; } = true;
        public bool ConnectResult { get; init; } = true;
        public List<string> Events { get; } = [];

        public bool Initialize(string host, string port)
        {
            Events.Add($"init:{host}:{port}");
            return InitializeResult;
        }

        public bool Connect()
        {
            Events.Add("connect");
            IsConnected = ConnectResult;
            return ConnectResult;
        }

        public void Register()
        {
            Events.Add("register");
        }

        public void ClearOutgoingBuffers()
        {
            Events.Add("clear");
        }

        public void SetCodec(EncryptionGeneration generation, byte key)
        {
            Events.Add($"codec:{generation}:{key}");
        }

        public void SendPacket(byte[] packetBody, bool sendNow)
        {
            var packet = packetBody[^1] == (byte)'\n'
                ? packetBody
                : [..packetBody, (byte)'\n'];
            Events.Add($"{(sendNow ? "sendNow" : "send")}:{System.Text.Encoding.ASCII.GetString(packet)}");
        }
    }
}
