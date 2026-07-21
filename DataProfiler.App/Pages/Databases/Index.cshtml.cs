using DataProfiler.App.Services.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace DataProfiler.App.Pages.Databases;

public class IndexModel : PageModel
{
    [BindProperty]
    public string? SelectedDatabaseName { get; set; }

    [TempData]
    public string? ConnectedServerName { get; set; }

    [TempData]
    public string? ConnectionStatus { get; set; }

    public SelectList DatabaseNames { get; private set; } = default!;

    public string StatusMessage { get; private set; } = "Ready to load databases.";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDatabasesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadDatabasesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(SelectedDatabaseName))
        {
            StatusMessage = "Select a database to continue.";
            return Page();
        }

        var connection = HttpContext.Session.GetConnection();
        if (connection is not null)
        {
            HttpContext.Session.SetDatabaseSelection(SelectedDatabaseName);
        }

        return RedirectToPage("/ObjectBrowser/Index", new { selectedDatabaseName = SelectedDatabaseName });
    }

    private async Task LoadDatabasesAsync(CancellationToken cancellationToken)
    {
        var connection = HttpContext.Session.GetConnection();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerName))
        {
            DatabaseNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Connect to a SQL Server instance first.";
            return;
        }

        ConnectedServerName = connection.ServerName;

        var databaseNames = new List<string>();

        try
        {
            await using var sqlConnection = new SqlConnection(connection.BuildConnectionString());
            await sqlConnection.OpenAsync(cancellationToken);

            await using var command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' ORDER BY name;";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                databaseNames.Add(reader.GetString(0));
            }

            DatabaseNames = new SelectList(databaseNames);
            StatusMessage = databaseNames.Count == 0
                ? $"No online databases were returned from {connection.ServerName}."
                : $"Found {databaseNames.Count} databases from {connection.ServerName}.";
        }
        catch (SqlException ex)
        {
            DatabaseNames = new SelectList(Array.Empty<string>());
            StatusMessage = $"Could not load databases from {connection.ServerName}: {ex.Message}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DatabaseNames = new SelectList(Array.Empty<string>());
            StatusMessage = "Database loading was canceled.";
        }
    }
}
