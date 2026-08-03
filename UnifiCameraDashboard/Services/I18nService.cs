namespace UnifiCameraDashboard.Services;

/// <summary>
/// Scoped i18n service. Language detection via navigator.language (JS interop) or
/// Accept-Language header (SSR). Translations are loaded from i18n/*.json at startup
/// by the singleton <see cref="TranslationStore"/> — add a new language by dropping a
/// new JSON file, no C# changes required.
/// </summary>
public sealed class I18nService(TranslationStore store)
{
    /// <summary>Fired after the active language changes so components can call StateHasChanged().</summary>
    public event Action? OnLanguageChanged;

    public string Language { get; private set; } = "en";

    /// <summary>
    /// Called from LanguageProvider after JS interop delivers navigator.language.
    /// Resolves to the best supported language and fires OnLanguageChanged.
    /// </summary>
    public void SetLanguageFromBrowser(string? browserLanguage)
    {
        if (string.IsNullOrWhiteSpace(browserLanguage))
            return;

        var primary = browserLanguage.Split('-')[0].ToLowerInvariant();
        var resolved = store.SupportsLanguage(primary) ? primary : "en";

        if (resolved == Language)
            return;

        Language = resolved;
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Called from App.razor during SSR to pre-set language from Accept-Language header.
    /// Does NOT fire OnLanguageChanged (no circuit yet).
    /// </summary>
    public void SetLanguageFromHeader(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return;

        foreach (var part in acceptLanguage.Split(','))
        {
            var tag = part.Split(';')[0].Trim();
            var primary = tag.Split('-')[0].ToLowerInvariant();
            if (store.SupportsLanguage(primary))
            {
                Language = primary;
                return;
            }
        }

        Language = "en";
    }

    /// <summary>Returns the translated string for the given key, with English fallback.</summary>
    public string Get(string key) => store.Get(Language, key);

    /// <summary>Formats a translated string with string.Format arguments.</summary>
    public string Format(string key, params object[] args)
        => string.Format(Get(key), args);
}
