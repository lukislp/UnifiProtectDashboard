using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using UnifiCameraDashboard.Services.Protect;

namespace UnifiCameraDashboard.Tests.Services.Protect;

/// <summary>
/// Property-based tests (FsCheck) for the realtime-updates frame decoder. The decoder sits
/// directly behind a websocket fed by an appliance speaking an undocumented protocol, so it is
/// thrown thousands of generated buffers here: pure garbage, structurally valid frames with
/// random payloads, and well-formed updates that must come back out exactly as they went in.
/// </summary>
public class ProtectWebSocketFrameCodecPropertyTests
{
    private const byte PacketTypeAction = 1;
    private const byte PacketTypeData = 2;
    private const byte FormatJson = 1;

    [Property(MaxTest = 1000)]
    public bool Garbage_is_rejected_with_a_format_error_and_never_anything_else(byte[] buffer)
    {
        return DecodeIsWellBehaved(buffer);
    }

    [Property(MaxTest = 1000)]
    public bool Structurally_valid_frames_with_random_payloads_never_escape_the_format_errors(
        byte actionFormat, bool actionDeflated, byte[] actionPayload, byte dataFormat, bool dataDeflated, byte[] dataPayload)
    {
        // Correct headers and lengths, arbitrary bytes behind them: this reaches the JSON and
        // zlib paths that pure random buffers almost never get to.
        var buffer = Frame(PacketTypeAction, actionFormat, actionDeflated, actionPayload)
            .Concat(Frame(PacketTypeData, dataFormat, dataDeflated, dataPayload))
            .ToArray();
        return DecodeIsWellBehaved(buffer);
    }

    [Property(MaxTest = 300)]
    public bool A_well_formed_update_comes_back_out_exactly_as_it_went_in(
        NonNull<string> action, NonNull<string> id, NonNull<string> modelKey, NonNull<string> newUpdateId,
        bool actionDeflated, byte dataFormatSeed, bool dataDeflated, int number, NonNull<string> text)
    {
        if (!new[] { action.Get, id.Get, modelKey.Get, newUpdateId.Get, text.Get }.All(IsWellFormed))
            return true;

        var actionJson = JsonSerializer.SerializeToUtf8Bytes(
            new { action = action.Get, id = id.Get, modelKey = modelKey.Get, newUpdateId = newUpdateId.Get });
        var dataFormat = (byte)((dataFormatSeed % 3) + 1); // 1 = JSON, 2 = UTF-8 string, 3 = raw buffer
        var data = dataFormat == FormatJson
            ? JsonSerializer.SerializeToUtf8Bytes(new { number, text = text.Get })
            : Encoding.UTF8.GetBytes(text.Get);

        var buffer = Frame(PacketTypeAction, FormatJson, actionDeflated, Deflate(actionJson, actionDeflated))
            .Concat(Frame(PacketTypeData, dataFormat, dataDeflated, Deflate(data, dataDeflated)))
            .ToArray();

        var update = ProtectWebSocketFrameCodec.Decode(buffer);

        if (update.Action != new ProtectActionFrame(action.Get, id.Get, modelKey.Get, newUpdateId.Get))
            return false;
        return dataFormat == FormatJson
            ? update.RawData is null
                && update.JsonData is { } json
                && json.GetProperty("number").GetInt32() == number
                && json.GetProperty("text").GetString() == text.Get
            : update.JsonData is null && update.RawData is not null && update.RawData.SequenceEqual(data);
    }

    // The counterexample CI found on the first run: a JSON action payload that is a number.
    [Theory]
    [InlineData("0")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"action\"")]
    public void A_non_object_action_payload_is_a_malformed_frame(string actionJson)
    {
        var buffer = Frame(PacketTypeAction, FormatJson, false, Encoding.UTF8.GetBytes(actionJson))
            .Concat(Frame(PacketTypeData, FormatJson, false, Encoding.UTF8.GetBytes("{}")))
            .ToArray();
        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(buffer));
    }

    // The counterexample CI found on 2026-09-12: a deflated payload with a valid zlib header
    // followed by garbage, which the native routine rejects with ZLibException instead of
    // InvalidDataException.
    [Fact]
    public void A_deflated_payload_zlib_rejects_outright_is_a_malformed_frame()
    {
        var buffer = Frame(PacketTypeAction, 0, true, [40, 238, 0, 0, 0, 0])
            .Concat(Frame(PacketTypeData, 0, false, []))
            .ToArray();
        Assert.Throws<ProtectFrameFormatException>(() => ProtectWebSocketFrameCodec.Decode(buffer));
    }

    private static bool DecodeIsWellBehaved(byte[] buffer)
    {
        try
        {
            ProtectWebSocketFrameCodec.Decode(buffer);
        }
        catch (ProtectFrameFormatException)
        {
            // The only exception the websocket loop handles - broken JSON and bad zlib
            // streams must surface as this one too.
        }
        return true;
    }

    private static byte[] Frame(byte packetType, byte payloadFormat, bool deflated, byte[] payload)
    {
        var frame = new byte[8 + payload.Length];
        frame[0] = packetType;
        frame[1] = payloadFormat;
        frame[2] = deflated ? (byte)1 : (byte)0;
        frame[4] = (byte)(payload.Length >> 24);
        frame[5] = (byte)(payload.Length >> 16);
        frame[6] = (byte)(payload.Length >> 8);
        frame[7] = (byte)payload.Length;
        payload.CopyTo(frame, 8);
        return frame;
    }

    private static byte[] Deflate(byte[] payload, bool deflated)
    {
        if (!deflated)
            return payload;
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(payload);
        }
        return output.ToArray();
    }

    private static bool IsWellFormed(string value) =>
        Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value)) == value;
}
