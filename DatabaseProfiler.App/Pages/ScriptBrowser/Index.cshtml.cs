using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DatabaseProfiler.App.Pages.ScriptBrowser;

public class IndexModel : PageModel
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;

    public IndexModel(SchemaDiscoveryService schemaDiscoveryService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedObjectKind { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedObjectSchemaName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedObjectName { get; set; }

    public ScriptBrowserViewModel ViewModel { get; private set; } = new();

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
            ViewModel = new ScriptBrowserViewModel();
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            ViewModel = new ScriptBrowserViewModel();
            StatusMessage = "Select a database to continue.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedObjectKind) || string.IsNullOrWhiteSpace(SelectedObjectSchemaName) || string.IsNullOrWhiteSpace(SelectedObjectName))
        {
            ViewModel = new ScriptBrowserViewModel();
            StatusMessage = "Select an object to view its script.";
            return;
        }

        var discovery = await _schemaDiscoveryService.DiscoverScriptBrowserAsync(
            connection,
            SelectedDatabaseName,
            SelectedObjectKind,
            SelectedObjectSchemaName,
            SelectedObjectName,
            cancellationToken);

        ViewModel = discovery;
        StatusMessage = discovery.ScriptStatusMessage;

        HttpContext.Session.SetObjectSelection(SelectedDatabaseName, SelectedObjectKind, SelectedObjectSchemaName, SelectedObjectName);
    }
}
