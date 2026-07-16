using DataProfiler.App.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Reports;

public class IndexModel : PageModel
{
    public ExportViewModel ViewModel { get; private set; } = new();

    public void OnGet()
    {
        ViewModel = new ExportViewModel
        {
            ExportFormats = new[]
            {
                "CSV",
                "Excel",
                "JSON",
                "Markdown",
                "PDF"
            },
            ScriptObjectTypes = new[]
            {
                "Functions",
                "Stored Procedures",
                "Tables",
                "Views"
            }
        };
    }
}
