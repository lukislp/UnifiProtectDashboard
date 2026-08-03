using Microsoft.AspNetCore.Components;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Components;

/// <summary>
/// Combined base class that provides both setup-check redirection and i18n support.
/// </summary>
public class LocalizedSetupCheckBase : LocalizedComponentBase
{
    [Inject]
    protected ISettingsService SettingsService { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();

        // Only check when not already on the setup page
        if (!Navigation.Uri.Contains("/setup", StringComparison.OrdinalIgnoreCase))
        {
            var isSetupComplete = await SettingsService.IsInitialSetupCompleteAsync();

            if (!isSetupComplete)
            {
                Navigation.NavigateTo("/setup");
            }
        }
    }
}
