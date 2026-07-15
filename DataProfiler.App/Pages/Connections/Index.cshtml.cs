using DataProfiler.App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Connections;

public class IndexModel : PageModel
{
    [BindProperty]
    public ConnectionInputModel Input { get; set; } = new();

    public SelectList AuthenticationMethods { get; private set; } = default!;

    public string StatusMessage { get; private set; } = "Ready to connect.";

    public void OnGet()
    {
        AuthenticationMethods = CreateAuthenticationMethods();
    }

    public void OnPost()
    {
        AuthenticationMethods = CreateAuthenticationMethods();
        if (Input.AuthenticationMethod == AuthenticationMethod.WindowsTrustedPassThrough)
        {
            StatusMessage = "Using Windows trusted pass-through. No SQL credentials are required if your Windows account already has access.";
            return;
        }

        StatusMessage = "Connection details captured for the current session.";
    }

    private static SelectList CreateAuthenticationMethods()
    {
        var methods = new[]
        {
            new SelectListItem("SQL Server authentication", AuthenticationMethod.SqlServer.ToString()),
            new SelectListItem("Windows trusted authentication pass-through", AuthenticationMethod.WindowsTrustedPassThrough.ToString())
        };

        return new SelectList(methods, "Value", "Text", AuthenticationMethod.WindowsTrustedPassThrough.ToString());
    }
}
