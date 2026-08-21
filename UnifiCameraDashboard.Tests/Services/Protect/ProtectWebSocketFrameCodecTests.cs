using System.IO.Compression;
using System.Text;
using UnifiCameraDashboard.Services.Protect;

namespace UnifiCameraDashboard.Tests.Services.Protect;

public class ProtectWebSocketFrameCodecTests
{
    [Fact]
    public void Decode_ValidJsonActionAndDataFrames_ReturnsParsedUpdate()
    {
        var packet = BuildPacket(
            actionHeader: (packetType: 1, format: 1, deflate: false),
            actionPayload: Encoding.UTF8.GetBytes("""{"action":"update","id":"evt-1","modelKey":"event","newUpdateId":"upd-1"}"""),
            dataHeader: (packetType: 2, format: 1, deflate: false),
            dataPayload: Encoding.UTF8.GetBytes("""{"start":123,"score":87}"""));

        var update = ProtectWebSocketFrameCodec.Decode(packet);

        Assert.Equal("update", update.Action.Action);
        Assert.Equal("evt-1", update.Action.Id);
        Assert.Equal("event", update.Action.ModelKey);
        Assert.Equal("upd-1", update.Action.NewUpdateId);
        Assert.NotNull(update.JsonData);
        Assert.Equal(87, update.JsonData!.Value.GetProperty("score").GetInt32());
        Assert.Null(update.RawData);
    }

    [Fact]
    public void Decode_DeflatedDataFrame_InflatesBeforeParsing()
    {
        var packet = BuildPacket(
            actionHeader: (1, 1, false),
            actionPayload: Encoding.UTF8.GetBytes("""{"action":"add","id":"evt-2","modelKey":"event","newUpdateId":"upd-2"}"""),
            dataHeader: (2, 1, true),
            dataPayload: Deflate(Encoding.UTF8.GetBytes("""{"type":"motion"}""")));

        var update = ProtectWebSocketFrameCodec.Decode(packet);

        Assert.Equal("motion", update.JsonData!.Value.GetProperty("type").GetString());
    }

    [Fact]
    public void Decode_Utf8StringDataFrame_ReturnsRawBytes()
    {
        const string text = "hello";
        var packet = BuildPacket(
            actionHeader: (1, 1, false),
            actionPayload: Encoding.UTF8.GetBytes("""{"action":"update","id":"evt-3","modelKey":"nvr","newUpdateId":"upd-3"}"""),
            dataHeader: (2, 2, false),
            dataPayload: Encoding.UTF8.GetBytes(text));

        var update = ProtectWebSocketFrameCodec.Decode(packet);

        Assert.Null(update.JsonData);
        Assert.Equal(text, Encoding.UTF8.GetString(update.RawData!));
    }

    [Fact]
    public void Decode_TruncatedHeader_Throws()
    {
        var buffer = new byte[4]; // shorter than the required 8-byte header

        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(buffer));
    }

    [Fact]
    public void Decode_DeclaredLengthExceedsBuffer_Throws()
    {
        // Header claims a 1000-byte payload but none follows.
        var buffer = BuildHeader(packetType: 1, format: 1, deflate: false, length: 1000);

        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(buffer));
    }

    [Fact]
    public void Decode_ActionFrameMissingRequiredField_Throws()
    {
        var packet = BuildPacket(
            actionHeader: (1, 1, false),
            actionPayload: Encoding.UTF8.GetBytes("""{"action":"update","id":"evt-4","modelKey":"event"}"""), // no newUpdateId
            dataHeader: (2, 1, false),
            dataPayload: Encoding.UTF8.GetBytes("{}"));

        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(packet));
    }

    [Fact]
    public void Decode_FirstFrameNotActionType_Throws()
    {
        var packet = BuildPacket(
            actionHeader: (2, 1, false), // wrong: data-frame type where an action frame is required
            actionPayload: Encoding.UTF8.GetBytes("""{"action":"update","id":"1","modelKey":"event","newUpdateId":"1"}"""),
            dataHeader: (2, 1, false),
            dataPayload: Encoding.UTF8.GetBytes("{}"));

        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(packet));
    }

    [Fact]
    public void Decode_SecondFrameNotDataType_Throws()
    {
        var packet = BuildPacket(
            actionHeader: (1, 1, false),
            actionPayload: Encoding.UTF8.GetBytes("""{"action":"update","id":"1","modelKey":"event","newUpdateId":"1"}"""),
            dataHeader: (1, 1, false), // wrong: action-frame type where a data frame is required
            dataPayload: Encoding.UTF8.GetBytes("{}"));

        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(packet));
    }

    private static byte[] BuildPacket(
        (int packetType, int format, bool deflate) actionHeader,
        byte[] actionPayload,
        (int packetType, int format, bool deflate) dataHeader,
        byte[] dataPayload)
    {
        using var stream = new MemoryStream();
        stream.Write(BuildHeader(actionHeader.packetType, actionHeader.format, actionHeader.deflate, actionPayload.Length));
        stream.Write(actionPayload);
        stream.Write(BuildHeader(dataHeader.packetType, dataHeader.format, dataHeader.deflate, dataPayload.Length));
        stream.Write(dataPayload);
        return stream.ToArray();
    }

    private static byte[] BuildHeader(int packetType, int format, bool deflate, int length)
    {
        return
        [
            (byte)packetType,
            (byte)format,
            (byte)(deflate ? 1 : 0),
            0,
            (byte)(length >> 24),
            (byte)(length >> 16),
            (byte)(length >> 8),
            (byte)length,
        ];
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }
}
