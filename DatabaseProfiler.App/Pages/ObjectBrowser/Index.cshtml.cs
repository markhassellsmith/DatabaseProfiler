using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DatabaseProfiler.App.Pages.ObjectBrowser;

public class IndexModel : PageModel
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;

    public IndexModel(SchemaDiscoveryService schemaDiscoveryService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    public SchemaBrowserViewModel ViewModel { get; private set; } = new();

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    private async Task LoadPageModelAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            ViewModel = new SchemaBrowserViewModel();
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;
        if (connection.SelectedDatabaseName != SelectedDatabaseName)
        {
            HttpContext.Session.SetDatabaseSelection(SelectedDatabaseName);
        }

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            ViewModel = new SchemaBrowserViewModel();
            StatusMessage = "Select a database to continue.";
            return;
        }

        var discovery = await _schemaDiscoveryService.DiscoverObjectBrowserAsync(connection, SelectedDatabaseName, cancellationToken);
        ViewModel = discovery;
        StatusMessage = null;
    }
}
