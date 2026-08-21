using System.Net.WebSockets;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Services.Protect;

public interface IProtectWebSocketClient
{
    /// <summary>
    /// The <c>newUpdateId</c> from the most recently decoded update, or null if the client
    /// hasn't received one yet this run. Callers can use this after a disconnect to judge how
    /// large a gap might need backfilling.
    /// </summary>
    string? LastUpdateId { get; }

    /// <summary>
    /// Connects to the Protect realtime updates websocket and invokes <paramref name="onUpdate"/>
    /// for every decoded update, reconnecting with backoff on any failure, until
    /// <paramref name="cancellationToken"/> is cancelled. Never throws for a single malformed
    /// frame or a failing <paramref name="onUpdate"/> call - both are logged and the loop
    /// continues, since this is a long-lived background connection.
    /// </summary>
    Task RunAsync(Func<ProtectUpdate, Task> onUpdate, CancellationToken cancellationToken);
}

public class ProtectWebSocketClient : IProtectWebSocketClient
{
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private const int ReceiveBufferSize = 16 * 1024;

    private readonly IUnifiProtectService _protectService;
    private readonly ILogger<ProtectWebSocketClient> _logger;

    public string? LastUpdateId { get; private set; }

    public ProtectWebSocketClient(IUnifiProtectService protectService, ILogger<ProtectWebSocketClient> logger)
    {
        _protectService = protectService;
        _logger = logger;
    }

    public async Task RunAsync(Func<ProtectUpdate, Task> onUpdate, CancellationToken cancellationToken)
    {
        var backoff = MinBackoff;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReceiveAsync(onUpdate, cancellationToken);
                backoff = MinBackoff; // a clean disconnect resets backoff for the next attempt
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Protect realtime updates websocket failed, retrying in {Backoff}", backoff);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(backoff, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
        }
    }

    private async Task ConnectAndReceiveAsync(Func<ProtectUpdate, Task> onUpdate, CancellationToken cancellationToken)
    {
        var session = await _protectService.GetSessionForWebSocketAsync()
            ?? throw new InvalidOperationException("No authenticated Protect session available for the realtime updates websocket.");

        using var socket = new ClientWebSocket();
        socket.Options.Cookies = session.Cookies;
        // Local Protect consoles use a self-signed certificate - same trust decision the
        // HttpClientHandler in UnifiProtectService already makes for the REST API.
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        if (!string.IsNullOrEmpty(session.AuthToken))
        {
            socket.Options.SetRequestHeader("Authorization", $"Bearer {session.AuthToken}");
        }
        if (!string.IsNullOrEmpty(session.CsrfToken))
        {
            socket.Options.SetRequestHeader("X-CSRF-Token", session.CsrfToken);
        }

        var uri = BuildWebSocketUri(session.BaseUrl);
        _logger.LogInformation("Connecting to Protect realtime updates websocket at {Uri}", uri);
        await socket.ConnectAsync(uri, cancellationToken);
        _logger.LogInformation("Protect realtime updates websocket connected");

        var buffer = new byte[ReceiveBufferSize];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveFullMessageAsync(socket, buffer, cancellationToken);
            if (message == null)
            {
                // Server-initiated close.
                return;
            }
            if (message.Length == 0)
            {
                continue;
            }

            ProtectUpdate update;
            try
            {
                update = ProtectWebSocketFrameCodec.Decode(message);
            }
            catch (ProtectFrameFormatException ex)
            {
                // Never let one unrecognized frame take down the whole realtime feed - this
                // protocol is unofficial and reverse-engineered, so an unexpected frame shape
                // (e.g. after a console firmware update) is expected to happen eventually.
                _logger.LogWarning(ex, "Failed to decode a Protect realtime update frame ({Length} bytes), skipping it", message.Length);
                continue;
            }

            LastUpdateId = update.Action.NewUpdateId;

            try
            {
                await onUpdate(update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Protect update {ModelKey}/{Action}", update.Action.ModelKey, update.Action.Action);
            }
        }
    }

    private static async Task<byte[]?> ReceiveFullMessageAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        using var messageStream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            messageStream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return messageStream.ToArray();
    }

    private static Uri BuildWebSocketUri(string baseUrl)
    {
        var httpUri = new Uri(baseUrl);
        var wsScheme = httpUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var builder = new UriBuilder(httpUri)
        {
            Scheme = wsScheme,
            Path = "/proxy/protect/ws/updates",
        };
        return builder.Uri;
    }
}
