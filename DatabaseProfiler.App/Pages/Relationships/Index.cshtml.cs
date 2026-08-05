using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;
using DatabaseProfiler.App.Services.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DatabaseProfiler.App.Pages.Relationships;

public class IndexModel : PageModel
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;
    private readonly RelationshipReportService _relationshipReportService;

    public IndexModel(
        SchemaDiscoveryService schemaDiscoveryService,
        RelationshipReportService relationshipReportService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
        _relationshipReportService = relationshipReportService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowHighConfidence { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public bool ShowMediumConfidence { get; set; } = false;

    public RelationshipBrowserViewModel? ViewModel { get; private set; }

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

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            StatusMessage = "Connect to a SQL Server instance first.";
            return Page();
        }

        var databaseName = SelectedDatabaseName ?? connection.SelectedDatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            StatusMessage = "Select a database to continue.";
            return Page();
        }

        // Discover relationships
        var viewModel = await _schemaDiscoveryService.DiscoverRelationshipsAsync(connection, databaseName, cancellationToken);

        // Apply confidence filter to suggested relationships
        var filteredRelationships = viewModel.Relationships.Where(r =>
        {
            if (r.Type == RelationshipType.Explicit)
                return true; // Always show explicit relationships

            // Filter suggested relationships by confidence
            if (r.Confidence == ConfidenceLevel.High && ShowHighConfidence)
                return true;
            if (r.Confidence == ConfidenceLevel.Medium && ShowMediumConfidence)
                return true;

            return false;
        }).ToList();

        // Create filtered view model for export
        var filteredViewModel = new RelationshipBrowserViewModel
        {
            Relationships = filteredRelationships
        };

        // Generate Excel report
        var result = _relationshipReportService.GenerateExcelReport(
            filteredViewModel,
            connection.ServerName,
            databaseName);

        return File(result.Content, result.ContentType, result.FileName);
    }

    private async Task LoadPageModelAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            ViewModel = null;
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
            ViewModel = null;
            StatusMessage = "Select a database to continue.";
            return;
        }

        var discovery = await _schemaDiscoveryService.DiscoverRelationshipsAsync(connection, SelectedDatabaseName, cancellationToken);

        // Apply confidence filter to suggested relationships
        var filteredRelationships = discovery.Relationships.Where(r =>
        {
            if (r.Type == RelationshipType.Explicit)
                return true; // Always show explicit relationships

            // Filter suggested relationships by confidence
            if (r.Confidence == ConfidenceLevel.High && ShowHighConfidence)
                return true;
            if (r.Confidence == ConfidenceLevel.Medium && ShowMediumConfidence)
                return true;

            return false;
        }).ToList();

        // Create a filtered view model
        ViewModel = new RelationshipBrowserViewModel
        {
            Relationships = filteredRelationships
        };

        StatusMessage = null;
    }
}
