using System.Text.Json.Serialization;
using System.Text.Json;

namespace UnifiCameraDashboard.Models;

// Custom JsonConverter for flexible String/Number conversion
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetInt64().ToString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => reader.GetString()
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

// Unifi Protect API Response Models

public class UnifiProtectBootstrapResponse
{
    [JsonPropertyName("cameras")]
    public List<UnifiProtectCamera> Cameras { get; set; } = new();

    [JsonPropertyName("nvr")]
    public UnifiNvr? Nvr { get; set; }
}

public class UnifiProtectCamera
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("mac")]
    public string Mac { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("connectionHost")]
    public string ConnectionHost { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; }

    [JsonPropertyName("firmwareVersion")]
    public string FirmwareVersion { get; set; } = string.Empty;

    [JsonPropertyName("channels")]
    public List<UnifiCameraChannel> Channels { get; set; } = new();

    [JsonPropertyName("lastSeen")]
    public long LastSeen { get; set; }

    [JsonPropertyName("isProbingForWifi")]
    public bool IsProbingForWifi { get; set; }

    [JsonPropertyName("modelKey")]
    public string ModelKey { get; set; } = string.Empty;
}

public class UnifiCameraChannel
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("isRtspEnabled")]
    public bool IsRtspEnabled { get; set; }

    [JsonPropertyName("rtspAlias")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string RtspAlias { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("fps")]
    public int Fps { get; set; }

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; set; }
}

public class UnifiNvr
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;
}

public class UnifiAuthResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}
