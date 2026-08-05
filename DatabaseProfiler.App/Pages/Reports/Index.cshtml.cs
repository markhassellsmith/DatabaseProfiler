using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DatabaseProfiler.App.Pages.Reports;

public class IndexModel : PageModel
{
    public sealed record ReportChoiceViewModel(string Title, string Description, string Format, string ActionText, string? PagePath);

    public IReadOnlyList<ReportChoiceViewModel> ReportChoices { get; private set; } = [];

    public string? DatabaseName { get; private set; }

    public string? ServerName { get; private set; }

    public void OnGet()
    {
        var connection = HttpContext.Session.GetConnection();

        ServerName = connection?.ServerName;
        DatabaseName = connection?.SelectedDatabaseName;

        ReportChoices =
        [
            new ReportChoiceViewModel(
                "Excel Report on the Selected Database",
                "Generate a Summary-only Excel report for the selected database as a whole.",
                "Excel",
                "Open database report",
                "/Reports/Database"),
            new ReportChoiceViewModel(
                "Excel Report on Tables",
                "Combine table schema and profiling data into a detailed Excel report with one sheet per table.",
                "Excel",
                "Open table report",
                "/Reports/Tables"),
            new ReportChoiceViewModel(
                "Excel Report on Relationships",
                "Generate a two-tab Excel report showing explicit foreign keys and suggested relationships based on naming patterns.",
                "Excel",
                "Open relationships report",
                "/Relationships/Index"),
            new ReportChoiceViewModel(
                "Entity-Relationship Diagram",
                "Generate entity-relationship diagrams for Visio import or Mermaid markdown viewers. Export selected tables with relationships in SQL DDL or Mermaid format.",
                "SQL DDL | Mermaid",
                "Open diagram export",
                "/ERD/Index"),
            new ReportChoiceViewModel(
                "Script Export (ZIP)",
                "Generate ZIP packages of SQL scripts for selected objects, including COMBINED.sql with GO separators.",
                "ZIP | SQL",
                "Open script export",
                "/Reports/ScriptExport")
        ];
    }
}
