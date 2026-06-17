namespace GServ.Protocol;

public static class GraalVersionCatalog
{
    private static readonly Dictionary<string, ClientVersionId> ClientVersions = new(StringComparer.Ordinal)
    {
        ["SERV009"] = ClientVersionId.Client125,
        ["SERV011"] = ClientVersionId.Client127,
        ["SERV013"] = ClientVersionId.Client128,
        ["SERV016"] = ClientVersionId.Client131,
        ["SERV018"] = ClientVersionId.Client132,
        ["GNW20020"] = ClientVersionId.Client133,
        ["GNW22030"] = ClientVersionId.Client134,
        ["GNW13040"] = ClientVersionId.Client135,
        ["GNW10050"] = ClientVersionId.Client136,
        ["GNW21050"] = ClientVersionId.Client137,
        ["GNW22060"] = ClientVersionId.Client138,
        ["GNW07080"] = ClientVersionId.Client139,
        ["GNW13110"] = ClientVersionId.Client1411,
        ["GNW31101"] = ClientVersionId.Client21,
        ["GNW01012"] = ClientVersionId.Client212,
        ["GNW23012"] = ClientVersionId.Client213,
        ["GNW30042"] = ClientVersionId.Client214,
        ["GNW19052"] = ClientVersionId.Client215,
        ["GNW20052"] = ClientVersionId.Client2151,
        ["GNW12102"] = ClientVersionId.Client216,
        ["GNW22122"] = ClientVersionId.Client217,
        ["GNW21033"] = ClientVersionId.Client218,
        ["GNW15053"] = ClientVersionId.Client219,
        ["GNW28063"] = ClientVersionId.Client22,
        ["GNW01113"] = ClientVersionId.Client221,
        ["GNW03014"] = ClientVersionId.Client222,
        ["GNW14015"] = ClientVersionId.Client23,
        ["GNW28015"] = ClientVersionId.Client231,
        ["G3D16053"] = ClientVersionId.Client3,
        ["G3D27063"] = ClientVersionId.Client301,
        ["G3D03014"] = ClientVersionId.Client3041,
        ["G3D28095"] = ClientVersionId.Client40211,
        ["G3D09125"] = ClientVersionId.Client4034,
        ["G3D17026"] = ClientVersionId.Client4042,
        ["G3D26076"] = ClientVersionId.Client4110,
        ["G3D20126"] = ClientVersionId.Client4208,
        ["G3D22067"] = ClientVersionId.Client507,
        ["G3D14097"] = ClientVersionId.Client512,
        ["G3D26090"] = ClientVersionId.Client531x,
        ["G3D3007A"] = ClientVersionId.Client6015,
        ["G3D2505C"] = ClientVersionId.Client6034,
        ["G3D0311C"] = ClientVersionId.Client6037,
        ["G3D0511C"] = ClientVersionId.Client6037Linux,
        ["G3D04048"] = ClientVersionId.ClientIphone11,
        ["G3D18010"] = ClientVersionId.ClientIphone15,
        ["G3D29090"] = ClientVersionId.ClientIphone111,
        ["G3D2504D"] = ClientVersionId.ClientIphoneBelote15,
        ["G3D2204D"] = ClientVersionId.ClientWorlds
    };

    private static readonly Dictionary<string, RemoteControlVersionId> RemoteControlVersions = new(StringComparer.Ordinal)
    {
        ["GSERV023"] = RemoteControlVersionId.Rc1010,
        ["GSERV024"] = RemoteControlVersionId.Rc11,
        ["GSERV025"] = RemoteControlVersionId.Rc2
    };

    private static readonly Dictionary<string, NpcControlVersionId> NpcControlVersions = new(StringComparer.Ordinal)
    {
        ["NCL11012"] = NpcControlVersionId.Nc11,
        ["NCL21075"] = NpcControlVersionId.Nc21
    };

    public static ClientVersionId GetClientVersionId(string token) =>
        ClientVersions.GetValueOrDefault(token, ClientVersionId.Unknown);

    public static RemoteControlVersionId GetRemoteControlVersionId(string token) =>
        RemoteControlVersions.GetValueOrDefault(token, RemoteControlVersionId.Unknown);

    public static NpcControlVersionId GetNpcControlVersionId(string token) =>
        NpcControlVersions.GetValueOrDefault(token, NpcControlVersionId.Unknown);
}
