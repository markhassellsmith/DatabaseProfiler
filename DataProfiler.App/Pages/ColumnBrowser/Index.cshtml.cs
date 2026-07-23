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

    [BindProperty(SupportsGet = true)]
    public string? SortColumn { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? SortDescending { get; set; }

    [BindProperty]
    public string? SelectedTableSelectionValue { get; set; }

    public SchemaBrowserViewModel ViewModel { get; private set; } = new();

    public SelectList TableNames { get; private set; } = default!;

    public string? StatusMessage { get; private set; }

    private static readonly StringComparer SortComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly string[] SortableColumns = ["Ordinal", "Name", "DataType", "Length"];

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
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        SelectedDatabaseName ??= connection.SelectedDatabaseName;

        if (string.IsNullOrWhiteSpace(SelectedTableSelectionValue)
            && string.IsNullOrWhiteSpace(SelectedTableSchemaName)
            && string.IsNullOrWhiteSpace(SelectedTableName)
            && string.Equals(connection.SelectedObjectKind, "Table", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(connection.SelectedObjectSchemaName)
            && !string.IsNullOrWhiteSpace(connection.SelectedObjectName))
        {
            SelectedTableSchemaName = connection.SelectedObjectSchemaName;
            SelectedTableName = connection.SelectedObjectName;
        }

        ApplySelectedTableSelection();

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            ViewModel = new SchemaBrowserViewModel();
            TableNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Select a database to continue.";
            return;
        }

        SortColumn = NormalizeSortColumn(SortColumn ?? connection.ColumnBrowserSortColumn);
        SortDescending ??= connection.ColumnBrowserSortDescending;

        var discovery = await _schemaDiscoveryService.DiscoverColumnBrowserAsync(connection, SelectedDatabaseName, SelectedTableSchemaName, SelectedTableName, cancellationToken);
        ViewModel = discovery;
        SelectedTableSelectionValue = ViewModel.SelectedTableSelectionValue;
        SelectedTableSchemaName = ViewModel.SelectedTableSchemaName;
        SelectedTableName = ViewModel.SelectedTableName;
        TableNames = new SelectList(ViewModel.Tables, nameof(SchemaTableModel.SelectionValue), nameof(SchemaTableModel.DisplayName), SelectedTableSelectionValue);
        HttpContext.Session.SetTableSelection(SelectedDatabaseName, SelectedTableSchemaName, SelectedTableName);

        ViewModel.Columns = SortColumns(ViewModel.Columns, SortColumn, SortDescending.GetValueOrDefault());
        HttpContext.Session.SetColumnBrowserSort(SortColumn, SortDescending.GetValueOrDefault());
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

    private static string? NormalizeSortColumn(string? sortColumn)
    {
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "Name";
        }

        return SortableColumns.Any(column => SortComparer.Equals(column, sortColumn))
            ? sortColumn
            : "Name";
    }

    private static IReadOnlyList<SchemaColumnModel> SortColumns(IReadOnlyList<SchemaColumnModel> columns, string? sortColumn, bool sortDescending)
    {
        return (sortColumn ?? "Name").ToLowerInvariant() switch
        {
            "ordinal" => sortDescending
                ? columns.OrderByDescending(column => column.Ordinal).ThenBy(column => column.Name, SortComparer).ToList()
                : columns.OrderBy(column => column.Ordinal).ThenBy(column => column.Name, SortComparer).ToList(),
            "datatype" => sortDescending
                ? columns.OrderByDescending(column => column.DataType, SortComparer).ThenBy(column => column.Name, SortComparer).ToList()
                : columns.OrderBy(column => column.DataType, SortComparer).ThenBy(column => column.Name, SortComparer).ToList(),
            "length" => sortDescending
                ? columns.OrderBy(column => column.LengthSortValue is null)
                    .ThenByDescending(column => column.LengthSortValue)
                    .ThenBy(column => column.Name, SortComparer)
                    .ToList()
                : columns.OrderBy(column => column.LengthSortValue is null)
                    .ThenBy(column => column.LengthSortValue)
                    .ThenBy(column => column.Name, SortComparer)
                    .ToList(),
            _ => sortDescending
                ? columns.OrderByDescending(column => column.Name, SortComparer).ThenBy(column => column.Ordinal).ToList()
                : columns.OrderBy(column => column.Name, SortComparer).ThenBy(column => column.Ordinal).ToList()
        };
    }
}