using Microsoft.AspNetCore.Components;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Components;

public class SetupCheckBase : ComponentBase
{
    [Inject]
    protected ISettingsService SettingsService { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Only check when not already on the setup page
        if (!Navigation.Uri.Contains("/setup", StringComparison.OrdinalIgnoreCase))
        {
            var isSetupComplete = await SettingsService.IsInitialSetupCompleteAsync();

            if (!isSetupComplete)
            {
                // NavigateTo without forceLoad avoids NavigationException in Blazor Server
                Navigation.NavigateTo("/setup");
            }
        }
    }
}
