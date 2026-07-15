using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Databases;

public class IndexModel : PageModel
{
    [BindProperty]
    public string? SelectedDatabaseName { get; set; }

    public SelectList DatabaseNames { get; private set; } = default!;

    public void OnGet()
    {
        DatabaseNames = CreateDatabaseNames();
    }

    public void OnPost()
    {
        DatabaseNames = CreateDatabaseNames();
    }

    private static SelectList CreateDatabaseNames()
    {
        var databaseNames = new[]
        {
            "master",
            "msdb",
            "model",
            "tempdb"
        };

        return new SelectList(databaseNames);
    }
}
