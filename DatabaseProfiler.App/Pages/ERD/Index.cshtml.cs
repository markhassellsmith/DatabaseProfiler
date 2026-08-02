using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;
using DatabaseProfiler.App.Services.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace DatabaseProfiler.App.Pages.ERD;

public class IndexModel : PageModel
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;
    private readonly ErdGenerationService _erdGenerationService;

    public IndexModel(
        SchemaDiscoveryService schemaDiscoveryService,
        ErdGenerationService erdGenerationService)
    {
        _schemaDiscoveryService = schemaDiscoveryService ?? throw new ArgumentNullException(nameof(schemaDiscoveryService));
        _erdGenerationService = erdGenerationService ?? throw new ArgumentNullException(nameof(erdGenerationService));
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedDatabaseName { get; set; }

    [BindProperty]
    public List<string> SelectedTableIds { get; set; } = [];

    [BindProperty]
    public bool IncludeExplicitFKs { get; set; } = true;

    [BindProperty]
    public bool IncludeSuggestedRelationships { get; set; }

    [BindProperty]
    public string ExportFormat { get; set; } = "Both";

    public IReadOnlyList<SchemaTableModel> AvailableTables { get; private set; } = [];

    public string? DatabaseName { get; private set; }

    public string? ServerName { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        await LoadPageModelAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(StatusMessage))
        {
            return Page();
        }

        if (SelectedTableIds.Count == 0)
        {
            StatusMessage = "Please select at least one table to generate the ERD.";
            return Page();
        }

        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            StatusMessage = "Unable to retrieve connection information.";
            return Page();
        }

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var baseFileName = $"{SelectedDatabaseName}_ERD_{timestamp}";

            switch (ExportFormat)
            {
                case "SqlDdl":
                    return await GenerateSqlDdlFile(connection, baseFileName, cancellationToken);

                case "Mermaid":
                    return await GenerateMermaidFile(connection, baseFileName, cancellationToken);

                case "Both":
                default:
                    // For "Both", we'll return SQL first and provide a message
                    // In a real implementation, you might use a ZIP file or return multiple files via JavaScript
                    // For now, we'll generate SQL and show a message
                    var sqlContent = await _erdGenerationService.GenerateSqlDdl(
                        connection,
                        SelectedDatabaseName,
                        SelectedTableIds,
                        IncludeExplicitFKs,
                        IncludeSuggestedRelationships,
                        cancellationToken);

                    var mermaidContent = await _erdGenerationService.GenerateMermaidDiagram(
                        connection,
                        SelectedDatabaseName,
                        SelectedTableIds,
                        IncludeExplicitFKs,
                        IncludeSuggestedRelationships,
                        cancellationToken);

                    // Store both in session temporarily for sequential download
                    HttpContext.Session.SetString("ERD_SQL_Content", sqlContent);
                    HttpContext.Session.SetString("ERD_SQL_FileName", $"{baseFileName}.sql");
                    HttpContext.Session.SetString("ERD_Mermaid_Content", mermaidContent);
                    HttpContext.Session.SetString("ERD_Mermaid_FileName", $"{baseFileName}.md");

                    StatusMessage = $"✅ ERD files generated successfully! Tables: {SelectedTableIds.Count}. Use the download buttons below.";
                    return Page();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating ERD: {ex.Message}";
            return Page();
        }
    }

    public IActionResult OnGetDownloadSql()
    {
        var content = HttpContext.Session.GetString("ERD_SQL_Content");
        var fileName = HttpContext.Session.GetString("ERD_SQL_FileName") ?? "ERD.sql";

        if (string.IsNullOrWhiteSpace(content))
        {
            return NotFound();
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/plain", fileName);
    }

    public IActionResult OnGetDownloadMermaid()
    {
        var content = HttpContext.Session.GetString("ERD_Mermaid_Content");
        var fileName = HttpContext.Session.GetString("ERD_Mermaid_FileName") ?? "ERD.md";

        if (string.IsNullOrWhiteSpace(content))
        {
            return NotFound();
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/markdown", fileName);
    }

    private async Task<IActionResult> GenerateSqlDdlFile(
        ConnectionSessionModel connection,
        string baseFileName,
        CancellationToken cancellationToken)
    {
        var content = await _erdGenerationService.GenerateSqlDdl(
            connection,
            SelectedDatabaseName!,
            SelectedTableIds,
            IncludeExplicitFKs,
            IncludeSuggestedRelationships,
            cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/plain", $"{baseFileName}.sql");
    }

    private async Task<IActionResult> GenerateMermaidFile(
        ConnectionSessionModel connection,
        string baseFileName,
        CancellationToken cancellationToken)
    {
        var content = await _erdGenerationService.GenerateMermaidDiagram(
            connection,
            SelectedDatabaseName!,
            SelectedTableIds,
            IncludeExplicitFKs,
            IncludeSuggestedRelationships,
            cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/markdown", $"{baseFileName}.md");
    }

    private async Task LoadPageModelAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            StatusMessage = "Connect to a SQL Server instance first.";
            AvailableTables = [];
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
            StatusMessage = "Select a database to continue.";
            AvailableTables = [];
            return;
        }

        try
        {
            AvailableTables = await _schemaDiscoveryService.DiscoverTablesAsync(
                connection,
                SelectedDatabaseName,
                cancellationToken);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading tables: {ex.Message}";
            AvailableTables = [];
        }
    }
}
