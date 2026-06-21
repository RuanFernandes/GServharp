namespace Preagonal.GServer.Protocol;

public sealed record GraalWebSocketFrameUnwrapResult(int Code, byte[] Payload);

public static class GraalWebSocketFrame
{
    public static byte[] WrapOutgoingBinary(ReadOnlySpan<byte> payload)
    {
        var headerLength = payload.Length <= 125 ? 2 : payload.Length <= 65535 ? 4 : 10;
        var frame = new byte[headerLength + payload.Length];
        frame[0] = 0x82;

        if (payload.Length <= 125)
        {
            frame[1] = (byte)payload.Length;
        }
        else if (payload.Length <= 65535)
        {
            frame[1] = 0x7E;
            frame[2] = (byte)(payload.Length >> 8);
            frame[3] = (byte)payload.Length;
        }
        else
        {
            frame[1] = 0x7F;
            var length = (long)payload.Length;
            frame[2] = (byte)(length >> 56);
            frame[3] = (byte)(length >> 48);
            frame[4] = (byte)(length >> 40);
            frame[5] = (byte)(length >> 32);
            frame[6] = (byte)(length >> 24);
            frame[7] = (byte)(length >> 16);
            frame[8] = (byte)(length >> 8);
            frame[9] = (byte)length;
        }

        payload.CopyTo(frame.AsSpan(headerLength));
        return frame;
    }

    public static GraalWebSocketFrameUnwrapResult UnwrapIncoming(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2)
            return new GraalWebSocketFrameUnwrapResult(-1, frame.ToArray());

        var opcode = frame[0] & 0x0F;
        if (opcode != 0x02)
            return new GraalWebSocketFrameUnwrapResult(-1, frame.ToArray());

        var lengthCode = frame[1] & 0x7F;
        var maskOffset = lengthCode <= 125 ? 2 : lengthCode == 126 ? 4 : 10;
        if (frame.Length < maskOffset + 4)
            return new GraalWebSocketFrameUnwrapResult(-1, frame.ToArray());

        var dataOffset = maskOffset + 4;
        var payloadLength = frame.Length - dataOffset;
        var payload = new byte[payloadLength];
        var mask = frame.Slice(maskOffset, 4);

        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(frame[dataOffset + i] ^ mask[i % 4]);

        return new GraalWebSocketFrameUnwrapResult(payload.Length, payload);
    }
}
