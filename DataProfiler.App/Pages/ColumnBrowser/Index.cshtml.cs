using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using DataProfiler.App.Services.Discovery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.ColumnBrowser;

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
    public string? SelectedTableSchemaName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedTableName { get; set; }

    [BindProperty]
    public string? SelectedTableSelectionValue { get; set; }

    public SchemaBrowserViewModel ViewModel { get; private set; } = new();

    public SelectList TableNames { get; private set; } = default!;

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
        ApplySelectedTableSelection();

        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            ViewModel = new SchemaBrowserViewModel();
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            ViewModel = new SchemaBrowserViewModel();
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Select a database to continue.";
            return;
        }

        var discovery = await _schemaDiscoveryService.DiscoverColumnBrowserAsync(connection, SelectedDatabaseName, SelectedTableSchemaName, SelectedTableName, cancellationToken);
        ViewModel = discovery;
        SelectedTableSelectionValue = ViewModel.SelectedTableSelectionValue;
        SelectedTableSchemaName = ViewModel.SelectedTableSchemaName;
        SelectedTableName = ViewModel.SelectedTableName;
        TableNames = new SelectList(ViewModel.Tables, nameof(SchemaTableModel.SelectionValue), nameof(SchemaTableModel.DisplayName), SelectedTableSelectionValue);
        HttpContext.Session.SetTableSelection(SelectedDatabaseName, SelectedTableSchemaName, SelectedTableName);
        StatusMessage = null;
    }

    private void ApplySelectedTableSelection()
    {
        if (!string.IsNullOrWhiteSpace(SelectedTableSelectionValue))
        {
            var parts = SelectedTableSelectionValue.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                SelectedTableSchemaName = parts[0];
                SelectedTableName = parts[1];
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(SelectedTableSchemaName) && !string.IsNullOrWhiteSpace(SelectedTableName))
        {
            SelectedTableSelectionValue = $"{SelectedTableSchemaName}|{SelectedTableName}";
        }
    }
}