using System.Text.Json;
using DatabaseProfiler.App.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace DatabaseProfiler.App.Services.Connections;

public sealed class ConnectionSessionModel
{
    public AuthenticationMethod AuthenticationMethod { get; set; }

    public string? ColumnBrowserSortColumn { get; set; }

    public bool ColumnBrowserSortDescending { get; set; }

    public string? ProfilingSortColumn { get; set; }

    public bool ProfilingSortDescending { get; set; }

    public string? Password { get; set; }

    public string? SelectedDatabaseName { get; set; }

    public string? SelectedObjectKind { get; set; }

    public string? SelectedObjectSchemaName { get; set; }

    public string? SelectedObjectName { get; set; }

    public string? SelectedColumnName { get; set; }

    public string[] SelectedReportTableValues { get; set; } = Array.Empty<string>();

    public bool IncludeTableProfileInfo { get; set; } = true;

    public string? ServerName { get; set; }

    public string? UserName { get; set; }

    public string BuildConnectionString(string? initialCatalog = "master")
    {
        var builder = new SqlConnectionStringBuilder
        {
            ApplicationName = "DatabaseProfiler.App",
            DataSource = ServerName,
            Encrypt = true,
            InitialCatalog = string.IsNullOrWhiteSpace(initialCatalog) ? "master" : initialCatalog,
            TrustServerCertificate = true
        };

        if (AuthenticationMethod == AuthenticationMethod.WindowsTrustedPassThrough)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = UserName;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }
}

public static class ConnectionSessionState
{
    private const string SessionKey = "DatabaseProfiler.ConnectionSession";
    private const string ActiveReportJobSessionKey = "DatabaseProfiler.ActiveReportJobId";

    public static ConnectionSessionModel? GetConnection(this ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var json = session.GetString(SessionKey);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ConnectionSessionModel>(json);
    }

    public static void SetConnection(this ISession session, ConnectionSessionModel connection)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(connection);

        session.SetString(SessionKey, JsonSerializer.Serialize(connection));
    }

    public static string? GetActiveReportJobId(this ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.GetString(ActiveReportJobSessionKey);
    }

    public static void SetActiveReportJobId(this ISession session, string? jobId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(jobId))
        {
            session.Remove(ActiveReportJobSessionKey);
            return;
        }

        session.SetString(ActiveReportJobSessionKey, jobId);
    }

    public static void ClearActiveReportJobId(this ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.Remove(ActiveReportJobSessionKey);
    }

    public static void SetDatabaseSelection(this ISession session, string? selectedDatabaseName)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = session.GetConnection() ?? new ConnectionSessionModel();
        connection.SelectedDatabaseName = selectedDatabaseName;
        connection.SelectedObjectKind = null;
        connection.SelectedObjectSchemaName = null;
        connection.SelectedObjectName = null;
        connection.SelectedColumnName = null;
        session.SetConnection(connection);
    }

    public static void SetReportTableSelection(this ISession session, IEnumerable<string> selectedTableValues, bool includeTableProfileInfo = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(selectedTableValues);

        var connection = session.GetConnection() ?? new ConnectionSessionModel();
        connection.SelectedReportTableValues = selectedTableValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        connection.IncludeTableProfileInfo = includeTableProfileInfo;
        session.SetConnection(connection);
    }

    public static void SetTableSelection(this ISession session, string? selectedDatabaseName, string? selectedTableSchemaName, string? selectedTableName)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = session.GetConnection() ?? new ConnectionSessionModel();
        connection.SelectedDatabaseName = selectedDatabaseName;
        connection.SelectedObjectKind = "Table";
        connection.SelectedObjectSchemaName = selectedTableSchemaName;
        connection.SelectedObjectName = selectedTableName;
        connection.SelectedColumnName = null;
        session.SetConnection(connection);
    }

    public static void SetObjectSelection(this ISession session, string? selectedDatabaseName, string? selectedObjectKind, string? selectedObjectSchemaName, string? selectedObjectName)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = session.GetConnection() ?? new ConnectionSessionModel();
        connection.SelectedDatabaseName = selectedDatabaseName;
        connection.SelectedObjectKind = selectedObjectKind;
        connection.SelectedObjectSchemaName = selectedObjectSchemaName;
        connection.SelectedObjectName = selectedObjectName;
        connection.SelectedColumnName = null;
        session.SetConnection(connection);
    }

    public static void SetColumnBrowserSort(this ISession session, string? sortColumn, bool sortDescending)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = session.GetConnection() ?? new ConnectionSessionModel();
        connection.ColumnBrowserSortColumn = sortColumn;
        connection.ColumnBrowserSortDescending = sortDescending;
        session.SetConnection(connection);
    }

    public static void SetProfilingSort(this ISession session, string? sortColumn, bool sortDescending)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = session.GetConnection() ?? new ConnectionSessionModel();
        connection.ProfilingSortColumn = sortColumn;
        connection.ProfilingSortDescending = sortDescending;
        session.SetConnection(connection);
    }
}
