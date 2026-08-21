using System.IO.Compression;
using System.Text.Json;

namespace UnifiCameraDashboard.Services.Protect;

/// <summary>
/// A single decoded "action" frame from the UniFi Protect realtime updates websocket.
/// Every update carries these four fields; UniFi may add more, which callers can read
/// straight off <see cref="ProtectUpdate.ActionJson"/> if they ever need them.
/// </summary>
public readonly record struct ProtectActionFrame(
    string Action,
    string Id,
    string ModelKey,
    string NewUpdateId);

/// <summary>
/// A fully decoded update: the action frame plus its accompanying data frame, in whichever
/// shape the data frame's payload format declared (JSON object, UTF-8 text, or raw bytes).
/// </summary>
public sealed class ProtectUpdate
{
    public required ProtectActionFrame Action { get; init; }
    public required JsonElement ActionJson { get; init; }
    public JsonElement? JsonData { get; init; }
    public byte[]? RawData { get; init; }
}

public sealed class ProtectFrameFormatException(string message) : Exception(message);

/// <summary>
/// Decodes UniFi Protect's unofficial, reverse-engineered realtime updates websocket protocol.
/// Every update message is two frames back to back — an 8-byte header followed by its payload,
/// twice: [header][action payload][header][data payload]. Header layout (big-endian):
/// byte 0 = packet type (1 = action, 2 = data), byte 1 = payload format (1 = JSON,
/// 2 = UTF-8 string, 3 = raw buffer), byte 2 = deflate flag (zlib), byte 3 = reserved,
/// bytes 4-7 = payload length. There is no official specification for this protocol; this
/// implementation follows the format documented by the actively maintained
/// hjdhjd/unifi-protect (homebridge-unifi-protect) decoder.
/// </summary>
public static class ProtectWebSocketFrameCodec
{
    private const int HeaderLength = 8;
    private const byte PacketTypeAction = 1;
    private const byte PacketTypeData = 2;
    private const byte FormatJson = 1;
    private const byte FormatUtf8String = 2;
    private const byte FormatBuffer = 3;

    public static ProtectUpdate Decode(byte[] buffer)
    {
        var (actionHeader, actionPayload, dataOffset) = ReadFrame(buffer, 0);
        if (actionHeader.PacketType != PacketTypeAction)
        {
            throw new ProtectFrameFormatException(
                $"Expected an action frame (packet type {PacketTypeAction}) first, got {actionHeader.PacketType}.");
        }
        if (actionHeader.PayloadFormat != FormatJson)
        {
            throw new ProtectFrameFormatException("Action frame payload must be JSON.");
        }

        using var actionDoc = JsonDocument.Parse(actionPayload);
        var actionRoot = actionDoc.RootElement.Clone();
        var action = ParseActionFrame(actionRoot);

        var (dataHeader, dataPayload, _) = ReadFrame(buffer, dataOffset);
        if (dataHeader.PacketType != PacketTypeData)
        {
            throw new ProtectFrameFormatException(
                $"Expected a data frame (packet type {PacketTypeData}) second, got {dataHeader.PacketType}.");
        }

        JsonElement? jsonData = null;
        byte[]? rawData = null;
        switch (dataHeader.PayloadFormat)
        {
            case FormatJson:
                using (var dataDoc = JsonDocument.Parse(dataPayload))
                {
                    jsonData = dataDoc.RootElement.Clone();
                }
                break;
            case FormatUtf8String:
            case FormatBuffer:
                rawData = dataPayload;
                break;
            default:
                throw new ProtectFrameFormatException($"Unknown data frame payload format {dataHeader.PayloadFormat}.");
        }

        return new ProtectUpdate
        {
            Action = action,
            ActionJson = actionRoot,
            JsonData = jsonData,
            RawData = rawData,
        };
    }

    private readonly record struct FrameHeader(byte PacketType, byte PayloadFormat, bool Deflated);

    private static (FrameHeader Header, byte[] Payload, int NextOffset) ReadFrame(byte[] buffer, int offset)
    {
        if (buffer.Length - offset < HeaderLength)
        {
            throw new ProtectFrameFormatException(
                $"Buffer too short to contain a frame header at offset {offset} (have {buffer.Length - offset} bytes, need {HeaderLength}).");
        }

        var packetType = buffer[offset];
        var payloadFormat = buffer[offset + 1];
        var deflated = buffer[offset + 2] != 0;
        // buffer[offset + 3] is reserved.
        var length = (buffer[offset + 4] << 24) | (buffer[offset + 5] << 16) | (buffer[offset + 6] << 8) | buffer[offset + 7];

        var payloadStart = offset + HeaderLength;
        if (length < 0 || buffer.Length - payloadStart < length)
        {
            throw new ProtectFrameFormatException(
                $"Declared payload length {length} at offset {offset} exceeds the available buffer ({buffer.Length - payloadStart} bytes remaining).");
        }

        var raw = new byte[length];
        Array.Copy(buffer, payloadStart, raw, 0, length);
        var payload = deflated ? Inflate(raw) : raw;

        return (new FrameHeader(packetType, payloadFormat, deflated), payload, payloadStart + length);
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static ProtectActionFrame ParseActionFrame(JsonElement root)
    {
        return new ProtectActionFrame(
            Action: RequireString(root, "action"),
            Id: RequireString(root, "id"),
            ModelKey: RequireString(root, "modelKey"),
            NewUpdateId: RequireString(root, "newUpdateId"));
    }

    private static string RequireString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ProtectFrameFormatException($"Action frame is missing required string field '{propertyName}'.");
        }
        return value.GetString()!;
    }
}
