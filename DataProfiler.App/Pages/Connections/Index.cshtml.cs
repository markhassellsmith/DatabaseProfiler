using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Connections;

public class IndexModel : PageModel
{
    [BindProperty]
    public ConnectionInputModel Input { get; set; } = new();

    [TempData]
    public string? ConnectedServerName { get; set; }

    [TempData]
    public string? ConnectionStatus { get; set; }

    public SelectList AuthenticationMethods { get; private set; } = default!;

    public void OnGet()
    {
        AuthenticationMethods = CreateAuthenticationMethods();
    }

    public IActionResult OnPost()
    {
        AuthenticationMethods = CreateAuthenticationMethods();
        ConnectedServerName = Input.ServerName;

        HttpContext.Session.SetConnection(new ConnectionSessionModel
        {
            AuthenticationMethod = Input.AuthenticationMethod,
            Password = Input.Password,
            ServerName = Input.ServerName,
            UserName = Input.UserName
        });

        if (Input.AuthenticationMethod == AuthenticationMethod.WindowsTrustedPassThrough)
        {
            ConnectionStatus = "Using Windows trusted pass-through. No SQL credentials are required if your Windows account already has access.";
            return RedirectToPage("/Databases/Index");
        }

        if (string.IsNullOrWhiteSpace(Input.UserName) || string.IsNullOrWhiteSpace(Input.Password))
        {
            return Page();
        }

        ConnectionStatus = "Connection details captured for the current session.";
        return RedirectToPage("/Databases/Index");
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
