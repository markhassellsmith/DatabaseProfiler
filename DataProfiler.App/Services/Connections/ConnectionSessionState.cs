using System.Text.Json;
using DataProfiler.App.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace DataProfiler.App.Services.Connections;

public sealed class ConnectionSessionModel
{
    public AuthenticationMethod AuthenticationMethod { get; set; }

    public string? Password { get; set; }

    public string? SelectedDatabaseName { get; set; }

    public string? SelectedObjectKind { get; set; }

    public string? SelectedObjectSchemaName { get; set; }

    public string? SelectedObjectName { get; set; }

    public string? SelectedColumnName { get; set; }

    public string? ServerName { get; set; }

    public string? UserName { get; set; }

    public string BuildConnectionString(string? initialCatalog = "master")
    {
        var builder = new SqlConnectionStringBuilder
        {
            ApplicationName = "DataProfiler.App",
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
    private const string SessionKey = "DataProfiler.ConnectionSession";

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
}
