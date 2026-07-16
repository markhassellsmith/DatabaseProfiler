using DataProfiler.App.Services.Theme;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Theme;

public class IndexModel : PageModel
{
    [BindProperty]
    public string? Theme { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public IActionResult OnPost()
    {
        if (Enum.TryParse<AppTheme>(Theme, ignoreCase: true, out var theme))
        {
            HttpContext.SetTheme(theme);
        }

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }
}
