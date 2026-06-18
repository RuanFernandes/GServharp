namespace Preagonal.GServer.Scripting;

public sealed record SourceCodeSlices(
    string Source,
    string ClientSide,
    string ServerSide,
    string ClientGs1,
    string ClientGs2)
{
    public static SourceCodeSlices Parse(string source, bool gs2Default, bool serverSideVm)
    {
        var clientSide = string.Empty;
        var serverSide = string.Empty;
        var clientGs1 = string.Empty;
        var clientGs2 = string.Empty;

        if (serverSideVm)
        {
            var clientSep = source.IndexOf("//#CLIENTSIDE", StringComparison.Ordinal);
            if (clientSep != -1)
            {
                clientSide = source[clientSep..];
                serverSide = source[..clientSep];
            }
            else
            {
                serverSide = source;
            }
        }
        else
        {
            clientSide = source;
        }

        if (clientSide.Length != 0)
        {
            var separator = gs2Default ? "//#GS1" : "//#GS2";
            var codeSeparatorLoc = clientSide.IndexOf(separator, StringComparison.Ordinal);
            if (codeSeparatorLoc != -1)
            {
                var originalCode = clientSide[..codeSeparatorLoc];
                var otherCode = clientSide[codeSeparatorLoc..];
                clientGs2 = gs2Default ? originalCode : otherCode;
                clientGs1 = gs2Default ? otherCode : originalCode;
            }
            else if (gs2Default)
            {
                clientGs2 = clientSide;
            }
            else
            {
                clientGs1 = clientSide;
            }
        }

        return new SourceCodeSlices(source, clientSide, serverSide, clientGs1, clientGs2);
    }
}
