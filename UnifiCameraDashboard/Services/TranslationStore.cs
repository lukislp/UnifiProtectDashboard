using System.Text.Json;

namespace UnifiCameraDashboard.Services;

/// <summary>
/// Singleton that loads all i18n/*.json files once at startup.
/// Adding a new language only requires dropping a new JSON file — no C# changes needed.
/// </summary>
public sealed class TranslationStore
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations;

    public TranslationStore(IWebHostEnvironment env)
    {
        _translations = [];

        var i18nDir = Path.Combine(env.ContentRootPath, "i18n");
        if (!Directory.Exists(i18nDir))
            return;

        foreach (var file in Directory.EnumerateFiles(i18nDir, "*.json"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            var json = File.ReadAllText(file);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is not null)
                _translations[lang] = dict;
        }
    }

    public bool SupportsLanguage(string lang) => _translations.ContainsKey(lang);

    public string Get(string lang, string key)
    {
        if (_translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        if (_translations.TryGetValue("en", out dict) && dict.TryGetValue(key, out value))
            return value;
        return key;
    }
}
