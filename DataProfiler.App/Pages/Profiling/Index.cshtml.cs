using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using DataProfiler.App.Services.Profiling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Profiling;

public class IndexModel : PageModel
{
    private readonly TableProfilingService _tableProfilingService;

    public IndexModel(TableProfilingService tableProfilingService)
    {
        _tableProfilingService = tableProfilingService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedTableSchemaName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedTableName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SortColumn { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? SortDescending { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedTableSelectionValue { get; set; }

    public ProfilingViewModel ViewModel { get; private set; } = new();

    public SelectList TableNames { get; private set; } = default!;

    public string? StatusMessage { get; private set; }

    private static readonly StringComparer SortComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly string[] SortableColumns = ["Ordinal", "Name"];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var redirect = GetPrerequisiteRedirect();
        if (redirect is not null)
        {
            return redirect;
        }

        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var redirect = GetPrerequisiteRedirect();
        if (redirect is not null)
        {
            return redirect;
        }

        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    private IActionResult? GetPrerequisiteRedirect()
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            return RedirectToPage("/Connections/Index");
        }

        var databaseName = SelectedDatabaseName ?? connection.SelectedDatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return RedirectToPage("/Databases/Index");
        }

        if (!HasSelectedTable(connection))
        {
            return RedirectToPage("/ObjectBrowser/Index", new { selectedDatabaseName = databaseName });
        }

        return null;
    }

    private bool HasSelectedTable(ConnectionSessionModel? connection)
    {
        if (!string.IsNullOrWhiteSpace(SelectedTableSelectionValue)
            || !string.IsNullOrWhiteSpace(SelectedTableSchemaName)
            || !string.IsNullOrWhiteSpace(SelectedTableName))
        {
            return true;
        }

        return connection is not null
            && string.Equals(connection.SelectedObjectKind, "Table", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(connection.SelectedObjectSchemaName)
            && !string.IsNullOrWhiteSpace(connection.SelectedObjectName);
    }

    private async Task LoadPageModelAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            ViewModel = new ProfilingViewModel();
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;

        SortColumn = NormalizeSortColumn(SortColumn ?? connection.ProfilingSortColumn);
        SortDescending ??= connection.ProfilingSortDescending;

        if (string.IsNullOrWhiteSpace(SelectedTableSelectionValue)
            && string.IsNullOrWhiteSpace(SelectedTableSchemaName)
            && string.IsNullOrWhiteSpace(SelectedTableName))
        {
            ViewModel = new ProfilingViewModel
            {
                DatabaseName = SelectedDatabaseName,
                ServerName = connection.ServerName
            };
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Select a table from Object Browser to load the profile.";
            return;
        }

        ApplySelectedTableSelection();

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            ViewModel = new ProfilingViewModel();
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Select a database to continue.";
            return;
        }

        var discovery = await _tableProfilingService.ProfileTableAsync(
            connection,
            SelectedDatabaseName,
            SelectedTableSchemaName,
            SelectedTableName,
            cancellationToken);

        ViewModel = discovery;
        SelectedTableSelectionValue = ViewModel.SelectedTableSelectionValue;
        SelectedTableSchemaName = ViewModel.SelectedTableSchemaName;
        SelectedTableName = ViewModel.SelectedTableName;
        ViewModel.ProfileScope = discovery.ProfileScope;
        TableNames = new SelectList(ViewModel.Tables, nameof(SchemaTableModel.SelectionValue), nameof(SchemaTableModel.DisplayName), SelectedTableSelectionValue);
        HttpContext.Session.SetTableSelection(SelectedDatabaseName, SelectedTableSchemaName, SelectedTableName);

        ViewModel.Columns = SortColumns(ViewModel.Columns, SortColumn, SortDescending.GetValueOrDefault());
        HttpContext.Session.SetProfilingSort(SortColumn, SortDescending.GetValueOrDefault());
        StatusMessage = null;
    }

    public bool IsActiveSortColumn(string columnName)
    {
        return string.Equals(SortColumn, columnName, StringComparison.OrdinalIgnoreCase);
    }

    public bool GetNextSortDescending(string columnName)
    {
        return IsActiveSortColumn(columnName)
            ? !SortDescending.GetValueOrDefault()
            : false;
    }

    public string GetSortIndicator(string columnName)
    {
        if (!IsActiveSortColumn(columnName))
        {
            return string.Empty;
        }

        return SortDescending.GetValueOrDefault() ? "▼" : "▲";
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

    private static string NormalizeSortColumn(string? sortColumn)
    {
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "Name";
        }

        return SortableColumns.Any(column => SortComparer.Equals(column, sortColumn))
            ? sortColumn
            : "Name";
    }

    private static IReadOnlyList<ColumnProfileModel> SortColumns(IReadOnlyList<ColumnProfileModel> columns, string? sortColumn, bool sortDescending)
    {
        return (sortColumn ?? "Name").ToLowerInvariant() switch
        {
            "ordinal" => sortDescending
                ? columns.OrderByDescending(column => column.Ordinal).ThenBy(column => column.Name, SortComparer).ToList()
                : columns.OrderBy(column => column.Ordinal).ThenBy(column => column.Name, SortComparer).ToList(),
            _ => sortDescending
                ? columns.OrderByDescending(column => column.Name, SortComparer).ThenBy(column => column.Ordinal).ToList()
                : columns.OrderBy(column => column.Name, SortComparer).ThenBy(column => column.Ordinal).ToList()
        };
    }
}