using System.IO.Compression;
using System.Text;
using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Models.Reporting;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;

namespace DatabaseProfiler.App.Services.Scripting;

public sealed class ScriptExportService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly SchemaDiscoveryService _schemaDiscoveryService;

    public ScriptExportService(SchemaDiscoveryService schemaDiscoveryService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
    }

    public async Task<ScriptExportResult> GenerateZipAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IReadOnlyCollection<string> selectedObjectValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(selectedObjectValues);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before script export can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before script export can run.");
        }

        var selections = ParseSelectedObjectValues(selectedObjectValues)
            .GroupBy(item => $"{item.Kind}|{item.SchemaName}|{item.ObjectName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (selections.Length == 0)
        {
            throw new InvalidOperationException("Select at least one object before generating a script export.");
        }

        var browser = await _schemaDiscoveryService.DiscoverObjectBrowserAsync(connection, databaseName, cancellationToken);
        var exportItems = new List<ScriptExportItem>(selections.Length);

        foreach (var selection in selections.OrderBy(item => item.KindOrder).ThenBy(item => item.SchemaName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase))
        {
            exportItems.Add(await CreateExportItemAsync(connection, databaseName, browser, selection, cancellationToken));
        }

        if (exportItems.Count == 0)
        {
            throw new InvalidOperationException("No scriptable objects were found for the selected items.");
        }

        var bytes = CreateZipArchive(connection.ServerName, databaseName, exportItems);
        var fileName = $"DatabaseProfiler_ScriptExport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        return new ScriptExportResult(bytes, "application/zip", fileName);
    }

    private async Task<ScriptExportItem> CreateExportItemAsync(
        ConnectionSessionModel connection,
        string databaseName,
        SchemaBrowserViewModel browser,
        ScriptExportSelection selection,
        CancellationToken cancellationToken)
    {
        return selection.Kind switch
        {
            "Table" => await CreateTableExportItemAsync(connection, databaseName, browser.Tables, selection, cancellationToken),
            "View" => await CreateScriptExportItemAsync(connection, databaseName, browser.Views, selection, "Views", cancellationToken),
            "StoredProcedure" => await CreateScriptExportItemAsync(connection, databaseName, browser.StoredProcedures, selection, "Stored Procedures", cancellationToken),
            "Function" => await CreateScriptExportItemAsync(connection, databaseName, browser.Functions, selection, "Functions", cancellationToken),
            "UserDefinedType" => await CreateUserDefinedTypeExportItemAsync(connection, databaseName, browser.UserDefinedTypes, selection, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported object kind '{selection.Kind}'.")
        };
    }

    private async Task<ScriptExportItem> CreateScriptExportItemAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IReadOnlyList<SchemaObjectEntryModel> items,
        ScriptExportSelection selection,
        string folderName,
        CancellationToken cancellationToken)
    {
        var item = items.FirstOrDefault(candidate =>
            string.Equals(candidate.SchemaName, selection.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Name, selection.ObjectName, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            throw new InvalidOperationException($"The selected object '{selection.DisplayName}' could not be resolved.");
        }

        var script = await _schemaDiscoveryService.DiscoverObjectScriptAsync(
            connection,
            databaseName,
            selection.Kind,
            selection.SchemaName,
            selection.ObjectName,
            cancellationToken);

        return new ScriptExportItem(
            selection.Kind,
            folderName,
            selection.SchemaName,
            selection.ObjectName,
            script,
            CreateScriptFilePath(folderName, selection.SchemaName, selection.ObjectName),
            selection.KindOrder);
    }

    private async Task<ScriptExportItem> CreateUserDefinedTypeExportItemAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IReadOnlyList<UserDefinedTypeModel> items,
        ScriptExportSelection selection,
        CancellationToken cancellationToken)
    {
        var item = items.FirstOrDefault(candidate =>
            string.Equals(candidate.SchemaName, selection.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Name, selection.ObjectName, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            throw new InvalidOperationException($"The selected object '{selection.DisplayName}' could not be resolved.");
        }

        var script = await _schemaDiscoveryService.DiscoverObjectScriptAsync(
            connection,
            databaseName,
            selection.Kind,
            selection.SchemaName,
            selection.ObjectName,
            cancellationToken);

        return new ScriptExportItem(
            selection.Kind,
            "User-Defined Types",
            selection.SchemaName,
            selection.ObjectName,
            script,
            CreateScriptFilePath("User-Defined Types", selection.SchemaName, selection.ObjectName),
            selection.KindOrder);
    }

    private async Task<ScriptExportItem> CreateTableExportItemAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IReadOnlyList<SchemaTableModel> tables,
        ScriptExportSelection selection,
        CancellationToken cancellationToken)
    {
        var table = tables.FirstOrDefault(candidate =>
            string.Equals(candidate.SchemaName, selection.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Name, selection.ObjectName, StringComparison.OrdinalIgnoreCase));

        if (table is null)
        {
            throw new InvalidOperationException($"The selected object '{selection.DisplayName}' could not be resolved.");
        }

        var browser = await _schemaDiscoveryService.DiscoverColumnBrowserAsync(
            connection,
            databaseName,
            tables,
            selection.SchemaName,
            selection.ObjectName,
            cancellationToken);

        var script = BuildCreateTableScript(table, browser.Columns);
        return new ScriptExportItem(
            selection.Kind,
            "Tables",
            selection.SchemaName,
            selection.ObjectName,
            script,
            CreateScriptFilePath("Tables", selection.SchemaName, selection.ObjectName),
            selection.KindOrder);
    }

    private static byte[] CreateZipArchive(string serverName, string databaseName, IReadOnlyList<ScriptExportItem> exportItems)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestText = BuildManifestText(serverName, databaseName, exportItems);
            WriteTextEntry(archive, "MANIFEST.txt", manifestText);

            var combinedText = BuildCombinedScriptText(serverName, databaseName, exportItems);
            WriteTextEntry(archive, "COMBINED.sql", combinedText);

            foreach (var item in exportItems)
            {
                WriteTextEntry(archive, item.FilePath, BuildSingleScriptText(item));
            }
        }

        return stream.ToArray();
    }

    private static string BuildManifestText(string serverName, string databaseName, IReadOnlyList<ScriptExportItem> exportItems)
    {
        var builder = new StringBuilder();
        builder.AppendLine("DatabaseProfiler Script Export Package");
        builder.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"Server: {serverName}");
        builder.AppendLine($"Database: {databaseName}");
        builder.AppendLine($"Object count: {exportItems.Count}");
        builder.AppendLine();

        foreach (var group in exportItems.GroupBy(item => item.KindLabel).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"[{group.Key}] {group.Count()} file(s)");
            foreach (var item in group)
            {
                builder.AppendLine($"- {item.FilePath}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Use COMBINED.sql to run all scripts in sequence.");
        return builder.ToString();
    }

    private static string BuildCombinedScriptText(string serverName, string databaseName, IReadOnlyList<ScriptExportItem> exportItems)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-- DatabaseProfiler Script Export");
        builder.AppendLine($"-- Server: {serverName}");
        builder.AppendLine($"-- Database: {databaseName}");
        builder.AppendLine($"-- Generated: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();

        foreach (var item in exportItems)
        {
            builder.AppendLine(BuildSectionHeader(item));
            builder.AppendLine(NormalizeLineEndings(string.IsNullOrWhiteSpace(item.ScriptText)
                ? "-- No CREATE script was returned for this object."
                : item.ScriptText).TrimEnd());
            builder.AppendLine();
            builder.AppendLine("GO");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildSingleScriptText(ScriptExportItem item)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BuildSectionHeader(item));
        builder.AppendLine(NormalizeLineEndings(string.IsNullOrWhiteSpace(item.ScriptText)
            ? "-- No CREATE script was returned for this object."
            : item.ScriptText).TrimEnd());
        return builder.ToString();
    }

    private static string BuildSectionHeader(ScriptExportItem item)
    {
        return $"-- ================================================================================\r\n-- {item.KindLabel}: {item.DisplayName}\r\n-- File: {item.FilePath}\r\n-- ================================================================================";
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Utf8NoBom, 1024, leaveOpen: false);
        writer.Write(content);
    }

    private static string BuildCreateTableScript(SchemaTableModel table, IReadOnlyList<SchemaColumnModel> columns)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-- DatabaseProfiler Script Export");
        builder.AppendLine($"-- Table: {table.DisplayName}");
        builder.AppendLine($"-- Rows: {table.RowCount:N0} | Columns: {table.ColumnCount}");
        builder.AppendLine();
        builder.AppendLine($"CREATE TABLE {BracketIdentifier(table.SchemaName)}.{BracketIdentifier(table.Name)} (");

        var primaryKeyColumns = columns.Where(column => column.IsPrimaryKey).ToArray();
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLastColumn = i == columns.Count - 1 && primaryKeyColumns.Length == 0;
            builder.Append("    ");
            builder.Append(BracketIdentifier(column.Name));
            builder.Append(' ');

            if (column.IsComputed && !string.IsNullOrWhiteSpace(column.ComputedDefinition))
            {
                builder.Append("AS ");
                builder.Append(column.ComputedDefinition);
            }
            else
            {
                builder.Append(column.DataType);

                if (!string.IsNullOrWhiteSpace(column.ColumnCollation))
                {
                    builder.Append(" COLLATE ");
                    builder.Append(column.ColumnCollation);
                }

                if (column.IsIdentity)
                {
                    builder.Append(" IDENTITY(");
                    builder.Append(column.IdentitySeed);
                    builder.Append(',');
                    builder.Append(column.IdentityIncrement);
                    builder.Append(')');
                }

                builder.Append(column.IsNullable ? " NULL" : " NOT NULL");

                if (!string.IsNullOrWhiteSpace(column.DefaultValue))
                {
                    builder.Append(" DEFAULT ");
                    builder.Append(column.DefaultValue);
                }
            }

            if (!isLastColumn)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        if (primaryKeyColumns.Length > 0)
        {
            var pkColumns = string.Join(", ", primaryKeyColumns.Select(column => BracketIdentifier(column.Name)));
            builder.AppendLine($"    CONSTRAINT {BracketIdentifier($"PK_{table.Name}")} PRIMARY KEY ({pkColumns})");
        }

        builder.AppendLine(");");
        return builder.ToString();
    }

    private static string BracketIdentifier(string value)
    {
        return $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string CreateScriptFilePath(string folderName, string schemaName, string objectName)
    {
        var safeFolderName = SanitizePathSegment(folderName);
        var safeSchemaName = SanitizePathSegment(schemaName);
        var safeObjectName = SanitizePathSegment(objectName);

        return $"{safeFolderName}/{safeSchemaName}.{safeObjectName}.sql";
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private static string NormalizeLineEndings(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    private static IEnumerable<ScriptExportSelection> ParseSelectedObjectValues(IReadOnlyCollection<string> selectedObjectValues)
    {
        foreach (var value in selectedObjectValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var parts = value.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                yield return new ScriptExportSelection("Table", parts[0], parts[1], 10);
                continue;
            }

            if (parts.Length != 3)
            {
                throw new InvalidOperationException($"Invalid script selection '{value}'.");
            }

            var kind = parts[0];
            var kindOrder = kind switch
            {
                "Table" => 10,
                "View" => 20,
                "StoredProcedure" => 30,
                "Function" => 40,
                "UserDefinedType" => 50,
                _ => throw new InvalidOperationException($"Unsupported object kind '{kind}'.")
            };

            yield return new ScriptExportSelection(kind, parts[1], parts[2], kindOrder);
        }
    }

    private sealed record ScriptExportSelection(string Kind, string SchemaName, string ObjectName, int KindOrder)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? ObjectName : $"{SchemaName}.{ObjectName}";
    }

    private sealed record ScriptExportItem(
        string KindLabel,
        string FolderName,
        string SchemaName,
        string ObjectName,
        string? ScriptText,
        string FilePath,
        int KindOrder)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? ObjectName : $"{SchemaName}.{ObjectName}";
    }
}
