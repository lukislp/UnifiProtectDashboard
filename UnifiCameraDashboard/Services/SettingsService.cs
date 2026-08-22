using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UnifiCameraDashboard.Data;

namespace UnifiCameraDashboard.Services;

public interface ISettingsService
{
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value, bool encrypt = false);
    Task<bool> IsInitialSetupCompleteAsync();
    Task<string?> GetUnifiProtectUrlAsync();
    Task<string?> GetUsernameAsync();
    Task<string?> GetPasswordAsync();
    Task<int> GetRefreshIntervalAsync();
    Task<bool> GetAutoDiscoveryEnabledAsync();
    Task SaveUnifiCredentialsAsync(string url, string username, string password);

    Task<bool> GetDailyDigestEnabledAsync();

    /// <summary>"HH:mm" in local server time.</summary>
    Task<string> GetDailyDigestTimeOfDayAsync();
    Task SaveDailyDigestSettingsAsync(bool enabled, string timeOfDay);
}

public class SettingsService : ISettingsService
{
    private readonly DashboardDbContext _context;
    private readonly ILogger<SettingsService> _logger;
    private readonly byte[] _encryptionKey;

    // Setting Keys
    private const string KEY_SETUP_COMPLETE = "SetupComplete";
    private const string KEY_UNIFI_URL = "UnifiProtectUrl";
    private const string KEY_USERNAME = "UnifiUsername";
    private const string KEY_PASSWORD = "UnifiPassword";
    private const string KEY_REFRESH_INTERVAL = "RefreshIntervalSeconds";
    private const string KEY_AUTO_DISCOVERY = "AutoDiscoveryEnabled";
    private const string KEY_DAILY_DIGEST_ENABLED = "DailyDigestEnabled";
    private const string KEY_DAILY_DIGEST_TIME = "DailyDigestTimeOfDay";

    public SettingsService(DashboardDbContext context, ILogger<SettingsService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;

        _encryptionKey = LoadOrCreateEncryptionKey(configuration);
    }

    /// <summary>
    /// Loads a stable encryption key from a file in the persistent data directory.
    /// If no file exists, a new key is generated and saved.
    /// This ensures the key survives container restarts.
    /// </summary>
    private static byte[] LoadOrCreateEncryptionKey(IConfiguration configuration)
    {
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnifiCameraDashboard");

        Directory.CreateDirectory(dataDir);
        var keyFile = Path.Combine(dataDir, "enc.key");

        if (File.Exists(keyFile))
        {
            try
            {
                var keyBytes = Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
                if (keyBytes.Length == 32)
                    return keyBytes;
            }
            catch { /* fall through to regenerate */ }
        }

        // Generate a new stable 256-bit key and persist it
        var newKey = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(keyFile, Convert.ToBase64String(newKey));
        return newKey;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        try
        {
            var setting = await _context.Settings
      .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
                return null;

            if (setting.IsEncrypted)
            {
                return DecryptString(setting.Value);
            }

            return setting.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving setting: {Key}", key);
            return null;
        }
    }

    public async Task SetSettingAsync(string key, string value, bool encrypt = false)
    {
        try
        {
            var setting = await _context.Settings
        .FirstOrDefaultAsync(s => s.Key == key);

            var valueToStore = encrypt ? EncryptString(value) : value;

            if (setting == null)
            {
                setting = new AppSettings
                {
                    Key = key,
                    Value = valueToStore,
                    IsEncrypted = encrypt,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Settings.Add(setting);
            }
            else
            {
                setting.Value = valueToStore;
                setting.IsEncrypted = encrypt;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving setting: {Key}", key);
            throw;
        }
    }

    public async Task<bool> IsInitialSetupCompleteAsync()
    {
        var value = await GetSettingAsync(KEY_SETUP_COMPLETE);
        return value == "true";
    }

    public async Task<string?> GetUnifiProtectUrlAsync()
    {
        return await GetSettingAsync(KEY_UNIFI_URL);
    }

    public async Task<string?> GetUsernameAsync()
    {
        return await GetSettingAsync(KEY_USERNAME);
    }

    public async Task<string?> GetPasswordAsync()
    {
        return await GetSettingAsync(KEY_PASSWORD);
    }

    public async Task<int> GetRefreshIntervalAsync()
    {
        var value = await GetSettingAsync(KEY_REFRESH_INTERVAL);
        return int.TryParse(value, out var interval) ? interval : 5;
    }

    public async Task<bool> GetAutoDiscoveryEnabledAsync()
    {
        var value = await GetSettingAsync(KEY_AUTO_DISCOVERY);
        return value == "true";
    }

    public async Task SaveUnifiCredentialsAsync(string url, string username, string password)
    {
        await SetSettingAsync(KEY_UNIFI_URL, url, encrypt: false);
        await SetSettingAsync(KEY_USERNAME, username, encrypt: false);
        await SetSettingAsync(KEY_PASSWORD, password, encrypt: true); // encrypt password
        await SetSettingAsync(KEY_SETUP_COMPLETE, "true", encrypt: false);
        await SetSettingAsync(KEY_AUTO_DISCOVERY, "true", encrypt: false);

        _logger.LogInformation("Unifi Protect credentials saved");
    }

    public async Task<bool> GetDailyDigestEnabledAsync()
    {
        var value = await GetSettingAsync(KEY_DAILY_DIGEST_ENABLED);
        return value == "true";
    }

    public async Task<string> GetDailyDigestTimeOfDayAsync()
    {
        return await GetSettingAsync(KEY_DAILY_DIGEST_TIME) ?? "20:00";
    }

    public async Task SaveDailyDigestSettingsAsync(bool enabled, string timeOfDay)
    {
        await SetSettingAsync(KEY_DAILY_DIGEST_ENABLED, enabled ? "true" : "false", encrypt: false);
        await SetSettingAsync(KEY_DAILY_DIGEST_TIME, timeOfDay, encrypt: false);
    }

    // Encryption with AES
    private string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();

        // Prepend IV
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    private string? DecryptString(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;

            // Extract IV
            var iv = new byte[aes.IV.Length];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Array.Copy(fullCipher, iv, iv.Length);
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed - the setting may have been encrypted with a different key. Please re-enter credentials.");
            return null;
        }
    }
}
