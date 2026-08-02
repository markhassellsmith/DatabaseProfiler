using DatabaseProfiler.App.Services.Connections;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseProfiler.App.ViewComponents;

public class ContextBreadcrumbViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var connection = HttpContext.Session.GetConnection();

        var model = new ContextBreadcrumbViewModel
        {
            ServerName = connection?.ServerName,
            DatabaseName = connection?.SelectedDatabaseName,
            ObjectKind = connection?.SelectedObjectKind,
            ObjectSchemaName = connection?.SelectedObjectSchemaName,
            ObjectName = connection?.SelectedObjectName
        };

        return View(model);
    }
}

public class ContextBreadcrumbViewModel
{
    public string? ServerName { get; set; }
    public string? DatabaseName { get; set; }
    public string? ObjectKind { get; set; }
    public string? ObjectSchemaName { get; set; }
    public string? ObjectName { get; set; }

    public bool HasServer => !string.IsNullOrWhiteSpace(ServerName);
    public bool HasDatabase => !string.IsNullOrWhiteSpace(DatabaseName);
    public bool HasObject => !string.IsNullOrWhiteSpace(ObjectName);

    public string ObjectDisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ObjectName))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(ObjectSchemaName))
                return ObjectName;

            return $"{ObjectSchemaName}.{ObjectName}";
        }
    }

    public string ObjectKindDisplay
    {
        get
        {
            return ObjectKind switch
            {
                "Table" => "Table",
                "View" => "View",
                "Function" => "Function",
                "StoredProcedure" => "Stored Procedure",
                _ => "Object"
            };
        }
    }
}
