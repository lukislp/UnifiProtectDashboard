using Microsoft.AspNetCore.Components;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Components;

/// <summary>
/// Base class for all components that display localized text.
/// Automatically subscribes to I18nService.OnLanguageChanged and triggers
/// a re-render so translations update when the browser language is detected.
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected I18nService I18n { get; set; } = default!;

    protected override void OnInitialized()
    {
        I18n.OnLanguageChanged += HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        I18n.OnLanguageChanged -= HandleLanguageChanged;
    }
}
