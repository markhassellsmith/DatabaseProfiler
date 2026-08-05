using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Models.Reporting;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;
using DatabaseProfiler.App.Services.Scripting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DatabaseProfiler.App.Pages.Reports;

public class ScriptExportModel : PageModel
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;
    private readonly ScriptExportService _scriptExportService;

    public ScriptExportModel(SchemaDiscoveryService schemaDiscoveryService, ScriptExportService scriptExportService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
        _scriptExportService = scriptExportService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    [BindProperty]
    public string[] SelectedObjectValues { get; set; } = [];

    public SchemaBrowserViewModel Browser { get; private set; } = new();

    public string? DatabaseName { get; private set; }

    public string? ServerName { get; private set; }

    public string? StatusMessage { get; private set; }

    public int ViewCount { get; private set; }

    public int StoredProcedureCount { get; private set; }

    public int FunctionCount { get; private set; }

    public int UserDefinedTypeCount { get; private set; }

    public bool HasSelection => SelectedObjectValues.Length > 0;

    public bool IsSelected(string selectionValue) => SelectedObjectValues.Contains(selectionValue, StringComparer.OrdinalIgnoreCase);

    public string CreateTableSelectionValue(SchemaTableModel table) => $"Table|{table.SelectionValue}";

    public string CreateObjectSelectionValue(string objectKind, string schemaName, string objectName) => $"{objectKind}|{schemaName}|{objectName}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(StatusMessage))
        {
            return Page();
        }

        if (!HasSelection)
        {
            StatusMessage = "Select at least one object kind before generating the ZIP package.";
            return Page();
        }

        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(DatabaseName))
        {
            StatusMessage = "Connect to a SQL Server instance and select a database first.";
            return Page();
        }

        var export = await _scriptExportService.GenerateZipAsync(
            connection,
            DatabaseName,
            SelectedObjectValues,
            cancellationToken);

        return File(export.Content, export.ContentType, export.FileName);
    }

    private async Task LoadPageModelAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            Browser = new SchemaBrowserViewModel();
            ServerName = null;
            DatabaseName = null;
            ViewCount = 0;
            StoredProcedureCount = 0;
            FunctionCount = 0;
            UserDefinedTypeCount = 0;
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;
        if (connection.SelectedDatabaseName != SelectedDatabaseName)
        {
            HttpContext.Session.SetDatabaseSelection(SelectedDatabaseName);
        }

        ServerName = connection.ServerName;
        DatabaseName = SelectedDatabaseName;

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            ViewCount = 0;
            StoredProcedureCount = 0;
            FunctionCount = 0;
            UserDefinedTypeCount = 0;
            StatusMessage = "Select a database to continue.";
            return;
        }

        var browser = await _schemaDiscoveryService.DiscoverObjectBrowserAsync(connection, SelectedDatabaseName, cancellationToken);
        Browser = browser;
        ViewCount = browser.ViewCount;
        StoredProcedureCount = browser.StoredProcedureCount;
        FunctionCount = browser.FunctionCount;
        UserDefinedTypeCount = browser.UserDefinedTypeCount;

        SelectedObjectValues = SelectedObjectValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        StatusMessage = null;
    }
}