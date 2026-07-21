using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using DataProfiler.App.Services.Discovery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Reports;

public class TablesModel : PageModel
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;

    public TablesModel(SchemaDiscoveryService schemaDiscoveryService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    [BindProperty]
    public string[] SelectedTableValues { get; set; } = Array.Empty<string>();

    public IReadOnlyList<SchemaTableModel> AvailableTables { get; private set; } = Array.Empty<SchemaTableModel>();

    public IReadOnlyList<string> SelectedTableDisplayNames { get; private set; } = Array.Empty<string>();

    public string? ReviewUrl { get; private set; }

    public string? DatabaseName { get; private set; }

    public string? ServerName { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(StatusMessage))
        {
            return Page();
        }

        HttpContext.Session.SetReportTableSelection(SelectedTableValues, HttpContext.Session.GetConnection()?.IncludeTableProfileInfo ?? true);
        return RedirectToPage("/Reports/Confirm");
    }

    public bool HasSelection => SelectedTableDisplayNames.Count > 0;

    private async Task LoadPageModelAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            StatusMessage = "Connect to a SQL Server instance first.";
            SelectedTableValues = Array.Empty<string>();
            AvailableTables = Array.Empty<SchemaTableModel>();
            SelectedTableDisplayNames = Array.Empty<string>();
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;
        if (connection.SelectedDatabaseName != SelectedDatabaseName)
        {
            HttpContext.Session.SetDatabaseSelection(SelectedDatabaseName);
        }

        ServerName = connection?.ServerName;
        DatabaseName = SelectedDatabaseName;

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            StatusMessage = "Select a database to continue.";
            SelectedTableValues = Array.Empty<string>();
            AvailableTables = Array.Empty<SchemaTableModel>();
            SelectedTableDisplayNames = Array.Empty<string>();
            return;
        }

        var discovery = await _schemaDiscoveryService.DiscoverObjectBrowserAsync(connection, SelectedDatabaseName, cancellationToken);
        AvailableTables = discovery.Tables;

        SelectedTableValues = SelectedTableValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selectedTables = AvailableTables
            .Where(table => SelectedTableValues.Contains(table.SelectionValue, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        SelectedTableDisplayNames = selectedTables.Length == 0
            ? Array.Empty<string>()
            : selectedTables.Select(table => table.DisplayName).ToArray();

        StatusMessage = SelectedTableDisplayNames.Count == 0
            ? "Choose one or more tables to review before generating the Excel report."
            : null;

        ReviewUrl = "/Reports/Confirm";
    }
}