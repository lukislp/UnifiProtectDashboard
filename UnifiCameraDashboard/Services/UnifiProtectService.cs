using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Services;

public interface IUnifiProtectService
{
    Task<bool> AuthenticateAsync(string url, string username, string password);
    Task<List<UnifiCamera>> DiscoverCamerasAsync();
    Task<UnifiProtectBootstrapResponse?> GetBootstrapDataAsync();
    Task<bool> TestConnectionAsync(string url);
    Task<Dictionary<string, bool>> GetCameraStatusAsync();
    Task<(byte[] Data, string ContentType)?> GetSnapshotAsync(string cameraId, int? width = null, int? height = null);
    bool IsAuthenticated { get; }
    Task<string> GetAuthenticationDebugInfoAsync(string url, string username, string password);
}

public class UnifiProtectService : IUnifiProtectService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<UnifiProtectService> _logger;

    // Static shared auth state so all scoped instances share the same session
    private static string? _authToken;
    private static string? _csrfToken;
    private static CookieContainer _cookieContainer = new();
    private static string? _currentBaseUrl;
    private static HttpClient? _authenticatedClient;
    private static readonly SemaphoreSlim _authLock = new(1, 1);
    private static bool _isAuthenticated = false;
    private static DateTime _lastAuthTime = DateTime.MinValue;
    private static readonly TimeSpan _sessionDuration = TimeSpan.FromHours(6);

    // Authenticated when login succeeded AND session hasn't expired
    public bool IsAuthenticated => _isAuthenticated && _authenticatedClient != null
        && DateTime.UtcNow - _lastAuthTime < _sessionDuration;

    public UnifiProtectService(
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        ILogger<UnifiProtectService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _logger = logger;
    }

    public void Dispose()
    {
        // _authenticatedClient is static/shared - do not dispose here
    }

    private HttpClient CreateAuthenticatedClient(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = false
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    public async Task<string> GetAuthenticationDebugInfoAsync(string url, string username, string password)
    {
        var debug = new StringBuilder();
        debug.AppendLine("=== Unifi Protect Authentication Debug ===");
        debug.AppendLine($"URL: {url}");
        debug.AppendLine($"Username: {username}");
        debug.AppendLine($"Password Length: {password?.Length ?? 0}");
        debug.AppendLine();

        try
        {
            _currentBaseUrl = url;
            _cookieContainer = new CookieContainer();
            _authenticatedClient?.Dispose();
            _authenticatedClient = CreateAuthenticatedClient(url);

            // Test 1: Basic connection
            debug.AppendLine("Test 1: Testing basic connection...");
            try
            {
                var pingResponse = await _authenticatedClient.GetAsync("/");
                debug.AppendLine($"Status: {pingResponse.StatusCode}");
                debug.AppendLine($"  Response headers: {string.Join(", ", pingResponse.Headers.Select(h => h.Key))}");
            }
            catch (Exception ex)
            {
                debug.AppendLine($"Error: {ex.Message}");
            }
            debug.AppendLine();

            // Test 2: Login
            debug.AppendLine("Test 2: Login request...");
            var loginData = new
            {
                username = username,
                password = password,
                rememberMe = true
            };

            var json = JsonSerializer.Serialize(loginData);
            debug.AppendLine($"Login Payload: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var loginResponse = await _authenticatedClient.PostAsync("/api/auth/login", content);

            debug.AppendLine($"Status Code: {loginResponse.StatusCode}");
            debug.AppendLine($"Is Success: {loginResponse.IsSuccessStatusCode}");

            var responseBody = await loginResponse.Content.ReadAsStringAsync();
            debug.AppendLine($"Response Body: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}");
            debug.AppendLine();

            // Test 3: Extracting cookies...
            debug.AppendLine("Test 3: Extracting cookies...");
            var cookies = _cookieContainer.GetCookies(new Uri(_authenticatedClient.BaseAddress!, "/api/auth/login"));
            debug.AppendLine($"Cookie Count: {cookies.Count}");

            foreach (Cookie cookie in cookies)
            {
                debug.AppendLine($"  - {cookie.Name}: {cookie.Value.Substring(0, Math.Min(50, cookie.Value.Length))}...");

                if (cookie.Name == "TOKEN" || cookie.Name == "token")
                {
                    _authToken = cookie.Value;
                    debug.AppendLine($"    Auth token found!");
                }
                else if (cookie.Name.ToLower().Contains("csrf"))
                {
                    _csrfToken = cookie.Value;
                    debug.AppendLine($"    CSRF token found!");
                }
            }
            debug.AppendLine();

            // Test 4: Checking response headers...
            debug.AppendLine("Test 4: Checking response headers...");
            foreach (var header in loginResponse.Headers)
            {
                debug.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");

                if (header.Key.ToLower().Contains("csrf"))
                {
                    _csrfToken = header.Value.FirstOrDefault();
                    debug.AppendLine($"    CSRF token from header!");
                }
            }
            debug.AppendLine();

            // Test 5: Call bootstrap API (if authenticated)
            if (!string.IsNullOrEmpty(_authToken) || loginResponse.IsSuccessStatusCode)
            {
                debug.AppendLine("Test 5: Testing bootstrap API...");

                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    _authenticatedClient.DefaultRequestHeaders.Remove("X-CSRF-Token");
                    _authenticatedClient.DefaultRequestHeaders.Add("X-CSRF-Token", _csrfToken);
                    debug.AppendLine($"  CSRF token added to header");
                }

                try
                {
                    var bootstrapResponse = await _authenticatedClient.GetAsync("/proxy/protect/api/bootstrap");
                    debug.AppendLine($"  Status: {bootstrapResponse.StatusCode}");

                    if (bootstrapResponse.IsSuccessStatusCode)
                    {
                        var bootstrapJson = await bootstrapResponse.Content.ReadAsStringAsync();
                        var preview = bootstrapJson.Substring(0, Math.Min(200, bootstrapJson.Length));
                        debug.AppendLine($"  Bootstrap data received: {preview}...");

                        // Try to count cameras
                        if (bootstrapJson.Contains("\"cameras\""))
                        {
                            try
                            {
                                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                var bootstrap = JsonSerializer.Deserialize<UnifiProtectBootstrapResponse>(bootstrapJson, options);
                                debug.AppendLine($"  {bootstrap?.Cameras.Count ?? 0} cameras found!");
                            }
                            catch
                            {
                                debug.AppendLine($"  JSON parsing failed");
                            }
                        }
                    }
                    else
                    {
                        var errorBody = await bootstrapResponse.Content.ReadAsStringAsync();
                        debug.AppendLine($"  Error: {errorBody.Substring(0, Math.Min(200, errorBody.Length))}");
                    }
                }
                catch (Exception ex)
                {
                    debug.AppendLine($"  Exception: {ex.Message}");
                }
            }
            else
            {
                debug.AppendLine("Test 5: Skipped (not authenticated)");
            }
            debug.AppendLine();

            // Zusammenfassung
            debug.AppendLine("=== Summary ===");
            debug.AppendLine($"Authenticated: {(!string.IsNullOrEmpty(_authToken) || loginResponse.IsSuccessStatusCode ? "[OK] YES" : "[X] NO")}");
            debug.AppendLine($"Auth token: {(!string.IsNullOrEmpty(_authToken) ? "[OK] Present" : "[X] Missing")}");
            debug.AppendLine($"CSRF token: {(!string.IsNullOrEmpty(_csrfToken) ? "[OK] Present" : "[!] Optional")}");
        }
        catch (Exception ex)
        {
            debug.AppendLine($"\nCRITICAL ERROR: {ex.Message}");
            debug.AppendLine($"Stack Trace: {ex.StackTrace}");
        }

        return debug.ToString();
    }

    public async Task<bool> AuthenticateAsync(string url, string username, string password)
    {
        await _authLock.WaitAsync();
        try
        {
            return await AuthenticateCoreAsync(url, username, password);
        }
        finally
        {
            _authLock.Release();
        }
    }

    // Lock-free inner implementation — must only be called while _authLock is held.
    private async Task<bool> AuthenticateCoreAsync(string url, string username, string password)
    {
        try
        {
            _logger.LogInformation("Authenticating with Unifi Protect: {Url}", url);

            _currentBaseUrl = url;
            _cookieContainer = new CookieContainer();
            _authenticatedClient?.Dispose();
            _authenticatedClient = CreateAuthenticatedClient(url);

            var loginData = new
            {
                username = username,
                password = password,
                rememberMe = true
            };

            var json = JsonSerializer.Serialize(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _authenticatedClient.PostAsync("/api/auth/login", content);

            _logger.LogInformation("Login Response Status: {Status}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                // UniFi Protect returns a Bearer token in the Authorization response header
                if (response.Headers.Contains("Authorization"))
                {
                    var authHeader = response.Headers.GetValues("Authorization").FirstOrDefault();
                    if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        _authToken = authHeader.Substring(7);
                        _logger.LogInformation("Bearer token extracted from Authorization header");
                    }
                }

                // Also check X-CSRF-Token header
                if (response.Headers.Contains("X-CSRF-Token"))
                {
                    _csrfToken = response.Headers.GetValues("X-CSRF-Token").FirstOrDefault();
                    _logger.LogInformation("CSRF token extracted from header");
                }

                // Fallback: scan cookies for TOKEN and csrf
                var baseUri = new Uri(url);
                var paths = new[] { "/api/auth/login", "/", "/api", "/proxy" };
                foreach (var path in paths)
                {
                    try
                    {
                        var cookies = _cookieContainer.GetCookies(new Uri(baseUri, path));
                        foreach (Cookie cookie in cookies)
                        {
                            _logger.LogDebug("Cookie [{Path}]: {Name}", path, cookie.Name);
                            if (string.IsNullOrEmpty(_authToken) && cookie.Name.Equals("TOKEN", StringComparison.OrdinalIgnoreCase))
                                _authToken = cookie.Value;
                            else if (cookie.Name.Contains("csrf", StringComparison.OrdinalIgnoreCase))
                                _csrfToken = cookie.Value;
                        }
                    }
                    catch { }
                }

                _logger.LogInformation("Cookie count after login: {Count}", _cookieContainer.Count);

                // Apply Bearer token to all future requests
                if (!string.IsNullOrEmpty(_authToken))
                {
                    _authenticatedClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                    _logger.LogInformation("Bearer token set on authenticated client");
                }

                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    _authenticatedClient.DefaultRequestHeaders.Remove("X-CSRF-Token");
                    _authenticatedClient.DefaultRequestHeaders.Add("X-CSRF-Token", _csrfToken);
                }

                _logger.LogInformation("Authentication successful");
                _isAuthenticated = true;
                _lastAuthTime = DateTime.UtcNow;
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Authentication failed: {Status} - {Error}", response.StatusCode, error);
                _isAuthenticated = false;
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            _isAuthenticated = false;
            return false;
        }
    }

    public async Task<UnifiProtectBootstrapResponse?> GetBootstrapDataAsync()
    {
        try
        {
            if (!IsAuthenticated)
            {
                await _authLock.WaitAsync();
                try
                {
                    if (!IsAuthenticated)
                    {
                        var url = await _settingsService.GetUnifiProtectUrlAsync();
                        var username = await _settingsService.GetUsernameAsync();
                        var password = await _settingsService.GetPasswordAsync();

                        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                        {
                            _logger.LogError("Credentials not configured");
                            return null;
                        }

                        var authenticated = await AuthenticateCoreAsync(url, username, password);
                        if (!authenticated)
                        {
                            _logger.LogError("Authentication failed");
                            return null;
                        }
                    }
                }
                finally
                {
                    _authLock.Release();
                }
            }

            if (_authenticatedClient == null)
            {
                _logger.LogError("No authenticated client available");
                return null;
            }

            // Add CSRF token per-request to avoid concurrent header mutation
            if (!string.IsNullOrEmpty(_csrfToken))
            {
                _authenticatedClient.DefaultRequestHeaders.Remove("X-CSRF-Token");
                _authenticatedClient.DefaultRequestHeaders.Add("X-CSRF-Token", _csrfToken);
            }

            _logger.LogInformation("Retrieving bootstrap data...");
            var response = await _authenticatedClient.GetAsync("/proxy/protect/api/bootstrap");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var bootstrap = JsonSerializer.Deserialize<UnifiProtectBootstrapResponse>(json, options);
                _logger.LogInformation("Bootstrap data retrieved successfully: {Count} cameras found",
                   bootstrap?.Cameras.Count ?? 0);

                return bootstrap;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error retrieving bootstrap data: {Status} - {Error}",
                        response.StatusCode, error);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bootstrap data");
            return null;
        }
    }

    public async Task<List<UnifiCamera>> DiscoverCamerasAsync()
    {
        try
        {
            var bootstrap = await GetBootstrapDataAsync();
            if (bootstrap == null || !bootstrap.Cameras.Any())
            {
                _logger.LogWarning("No cameras found");
                return new List<UnifiCamera>();
            }

            var cameras = new List<UnifiCamera>();
            var baseUrl = _currentBaseUrl ?? await _settingsService.GetUnifiProtectUrlAsync() ?? "https://192.168.2.20";

            foreach (var protectCamera in bootstrap.Cameras)
            {
                var channel = protectCamera.Channels.FirstOrDefault(c => c.IsRtspEnabled)
              ?? protectCamera.Channels.FirstOrDefault();

                if (channel == null) continue;

                // Determine online status correctly
                // Home Assistant logic: isConnected OR (state == CONNECTED && not isProbingForWifi)
                var isOnline = protectCamera.IsConnected ||
           (protectCamera.State.Equals("CONNECTED", StringComparison.OrdinalIgnoreCase) &&
           !protectCamera.IsProbingForWifi);

                var camera = new UnifiCamera
                {
                    Id = protectCamera.Id,
                    Name = protectCamera.Name,
                    MacAddress = protectCamera.Mac,
                    Model = protectCamera.ModelKey,
                    FirmwareVersion = protectCamera.FirmwareVersion,
                    IsOnline = isOnline,
                    Width = channel.Width,
                    Height = channel.Height,

                    // Snapshot URL via Unifi Protect API
                    SnapshotUrl = $"{baseUrl}/proxy/protect/api/cameras/{protectCamera.Id}/snapshot",

                    // RTSP URL (if available)
                    RtspUrl = channel.IsRtspEnabled
               ? $"rtsp://{protectCamera.ConnectionHost}:7447/{channel.RtspAlias}"
                       : $"rtsp://{protectCamera.Host}:554/s0"
                };

                cameras.Add(camera);
                _logger.LogInformation("Camera discovered: {Name} ({Id}) - Status: IsConnected={IsConnected}, State={State}, IsProbingForWifi={IsProbingForWifi} -> IsOnline={IsOnline}",
                       camera.Name, camera.Id, protectCamera.IsConnected, protectCamera.State, protectCamera.IsProbingForWifi, isOnline);
            }

            return cameras;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering cameras");
            return new List<UnifiCamera>();
        }
    }

    public async Task<bool> TestConnectionAsync(string url)
    {
        try
        {
            var testClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            })
            {
                BaseAddress = new Uri(url),
                Timeout = TimeSpan.FromSeconds(10)
            };

            var response = await testClient.GetAsync("/");
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for {Url}", url);
            return false;
        }
    }

    public async Task<(byte[] Data, string ContentType)?> GetSnapshotAsync(string cameraId, int? width = null, int? height = null)
    {
        try
        {
            // Ensure we have a valid session; serialize auth to avoid races when
            // multiple cameras request snapshots simultaneously at startup.
            if (!IsAuthenticated)
            {
                await _authLock.WaitAsync();
                try
                {
                    if (!IsAuthenticated) // double-check inside lock
                    {
                        var url = await _settingsService.GetUnifiProtectUrlAsync();
                        var username = await _settingsService.GetUsernameAsync();
                        var password = await _settingsService.GetPasswordAsync();

                        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                        {
                            _logger.LogError("Credentials not configured");
                            return null;
                        }

                        if (!await AuthenticateCoreAsync(url, username, password))
                        {
                            _logger.LogError("Re-authentication failed, cannot load snapshot");
                            return null;
                        }
                    }
                }
                finally
                {
                    _authLock.Release();
                }
            }

            // Build a per-request message to avoid mutating shared DefaultRequestHeaders concurrently
            var snapshotUrl = $"/proxy/protect/api/cameras/{cameraId}/snapshot";
            if (width.HasValue || height.HasValue)
                snapshotUrl += $"?w={width ?? 0}&h={height ?? 0}";

            var request = new HttpRequestMessage(HttpMethod.Get, snapshotUrl);
            if (!string.IsNullOrEmpty(_csrfToken))
                request.Headers.TryAddWithoutValidation("X-CSRF-Token", _csrfToken);

            var response = await _authenticatedClient!.SendAsync(request);

            // Session expired — force re-auth once and retry (401 Unauthorized only)
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Snapshot 401 for camera {CameraId} — forcing re-auth", cameraId);
                _isAuthenticated = false;

                await _authLock.WaitAsync();
                try
                {
                    if (!IsAuthenticated)
                    {
                        var reUrl = await _settingsService.GetUnifiProtectUrlAsync();
                        var reUser = await _settingsService.GetUsernameAsync();
                        var rePass = await _settingsService.GetPasswordAsync();
                        if (!string.IsNullOrEmpty(reUrl) && !string.IsNullOrEmpty(reUser) && !string.IsNullOrEmpty(rePass))
                            await AuthenticateCoreAsync(reUrl, reUser, rePass);
                    }
                }
                finally
                {
                    _authLock.Release();
                }

                if (!IsAuthenticated)
                    return null;

                // Re-apply auth headers after re-authentication
                if (!string.IsNullOrEmpty(_authToken))
                    _authenticatedClient!.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

                if (!IsAuthenticated)
                    return null;

                var retryRequest = new HttpRequestMessage(HttpMethod.Get, snapshotUrl);
                if (!string.IsNullOrEmpty(_csrfToken))
                    retryRequest.Headers.TryAddWithoutValidation("X-CSRF-Token", _csrfToken);
                response = await _authenticatedClient!.SendAsync(retryRequest);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Snapshot failed for camera {CameraId}: {Status}", cameraId, response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var data = await response.Content.ReadAsByteArrayAsync();
            return (data, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading snapshot for camera {CameraId}", cameraId);
            return null;
        }
    }

    public async Task<Dictionary<string, bool>> GetCameraStatusAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving live camera status...");
            var bootstrap = await GetBootstrapDataAsync();

            if (bootstrap == null || !bootstrap.Cameras.Any())
            {
                _logger.LogWarning("No bootstrap data for status check");
                return new Dictionary<string, bool>();
            }

            var statusDict = new Dictionary<string, bool>();

            foreach (var protectCamera in bootstrap.Cameras)
            {
                var isOnline = protectCamera.IsConnected ||
                    (protectCamera.State.Equals("CONNECTED", StringComparison.OrdinalIgnoreCase) &&
                    !protectCamera.IsProbingForWifi);

                statusDict[protectCamera.Id] = isOnline;

                _logger.LogDebug("Camera {Id} status: IsConnected={IsConnected}, State={State}, IsProbingForWifi={Probing} -> IsOnline={IsOnline}",
                    protectCamera.Id, protectCamera.IsConnected, protectCamera.State, protectCamera.IsProbingForWifi, isOnline);
            }

            _logger.LogInformation("Live status retrieved: {Online}/{Total} cameras online",
                statusDict.Count(kvp => kvp.Value), statusDict.Count);

            return statusDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving live status");
            return new Dictionary<string, bool>();
        }
    }
}

