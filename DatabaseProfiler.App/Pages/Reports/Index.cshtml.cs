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
                "Script Report",
                "Generate plain text scripts for views, functions, and stored procedures.",
                "Text",
                "Coming soon",
                null)
        ];
    }
}
