using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using Microsoft.Data.SqlClient;

namespace DataProfiler.App.Services.Discovery;

public sealed class SchemaDiscoveryService
{
    public async Task<IReadOnlyList<SchemaTableModel>> DiscoverTablesAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before schema discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before schema discovery can run.");
        }

        return await LoadTablesAsync(connection, databaseName, cancellationToken);
    }

    public async Task<SchemaBrowserViewModel> DiscoverObjectBrowserAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before schema discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before schema discovery can run.");
        }

        var tables = await LoadTablesAsync(connection, databaseName, cancellationToken);
        var views = await LoadObjectEntriesAsync(connection, databaseName, new[] { "V" }, cancellationToken);
        var storedProcedures = await LoadObjectEntriesAsync(connection, databaseName, new[] { "P" }, cancellationToken);
        var functions = await LoadFunctionsAsync(connection, databaseName, cancellationToken);

        return new SchemaBrowserViewModel
        {
            ServerName = connection.ServerName,
            DatabaseName = databaseName,
            FunctionCount = functions.Count,
            Functions = functions,
            StoredProcedureCount = storedProcedures.Count,
            StoredProcedures = storedProcedures,
            TableCount = tables.Count,
            Tables = tables,
            ViewCount = views.Count,
            Views = views
        };
    }

    public async Task<SchemaBrowserViewModel> DiscoverColumnBrowserAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string? selectedTableSchemaName,
        string? selectedTableName,
        CancellationToken cancellationToken)
    {
        var tables = await DiscoverTablesAsync(connection, databaseName, cancellationToken);
        return await DiscoverColumnBrowserAsync(connection, databaseName, tables, selectedTableSchemaName, selectedTableName, cancellationToken);
    }

    public async Task<SchemaBrowserViewModel> DiscoverColumnBrowserAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IReadOnlyList<SchemaTableModel> tables,
        string? selectedTableSchemaName,
        string? selectedTableName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before schema discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before schema discovery can run.");
        }

        ArgumentNullException.ThrowIfNull(tables);

        var selectedTable = ResolveSelectedTable(tables, selectedTableSchemaName, selectedTableName);

        var columns = selectedTable is null
            ? []
            : await LoadColumnsAsync(connection, databaseName, selectedTable.SchemaName, selectedTable.Name, cancellationToken);

        return new SchemaBrowserViewModel
        {
            ColumnCount = columns.Count,
            Columns = columns,
            DatabaseName = databaseName,
            ServerName = connection.ServerName,
            SelectedTableSchemaName = selectedTable?.SchemaName,
            SelectedTableName = selectedTable?.Name,
            TableCount = tables.Count,
            Tables = tables
        };
    }

    public async Task<ScriptBrowserViewModel> DiscoverScriptBrowserAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string objectKind,
        string objectSchemaName,
        string objectName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before script discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before script discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(objectKind))
        {
            throw new InvalidOperationException("An object kind is required before script discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(objectSchemaName))
        {
            throw new InvalidOperationException("An object schema name is required before script discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new InvalidOperationException("An object name is required before script discovery can run.");
        }

        var script = await LoadObjectScriptAsync(connection, databaseName, objectKind, objectSchemaName, objectName, cancellationToken);
        var scriptLines = SplitScriptLines(script);

        return new ScriptBrowserViewModel
        {
            DatabaseName = databaseName,
            ObjectKindLabel = GetObjectKindLabel(objectKind),
            ObjectName = objectName,
            ObjectSchemaName = objectSchemaName,
            ScriptLines = scriptLines,
            ScriptStatusMessage = scriptLines.Count == 0 ? "No CREATE script was returned for this object." : null,
            ServerName = connection.ServerName
        };
    }

    public async Task<SchemaBrowserViewModel> DiscoverAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string? selectedTableName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before schema discovery can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before schema discovery can run.");
        }

        var objectBrowser = await DiscoverObjectBrowserAsync(connection, databaseName, cancellationToken);
        var columnBrowser = await DiscoverColumnBrowserAsync(connection, databaseName, objectBrowser.Tables, null, selectedTableName, cancellationToken);

        return new SchemaBrowserViewModel
        {
            ColumnCount = columnBrowser.ColumnCount,
            Columns = columnBrowser.Columns,
            DatabaseName = databaseName,
            ServerName = connection.ServerName,
            FunctionCount = objectBrowser.FunctionCount,
            Functions = objectBrowser.Functions,
            SelectedTableName = columnBrowser.SelectedTableName,
            StoredProcedureCount = objectBrowser.StoredProcedureCount,
            StoredProcedures = objectBrowser.StoredProcedures,
            TableCount = objectBrowser.TableCount,
            Tables = objectBrowser.Tables,
            ViewCount = objectBrowser.ViewCount,
            Views = objectBrowser.Views
        };
    }

    private static async Task<List<SchemaColumnModel>> LoadColumnsAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<SchemaColumnModel>();
        var connectionString = connection.BuildConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT
                -- Core Column Identity
                c.column_id AS ColumnId,
                c.name AS ColumnName,
                ty.name AS DataType,

                -- Data Type Attributes
                c.max_length AS MaxLength,
                c.precision AS PrecisionValue,
                c.scale AS ScaleValue,
                c.collation_name AS ColumnCollation,

                -- Common Column Properties
                c.is_nullable AS IsNullable,
                dc.definition AS DefaultDefinition,

                -- Special Column Types
                c.is_identity AS IsIdentity,
                CONVERT(bigint, ic.seed_value) AS IdentitySeed,
                CONVERT(bigint, ic.increment_value) AS IdentityIncrement,
                c.is_computed AS IsComputed,
                cc.definition AS ComputedDefinition,

                -- Keys and Indexes
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ixc ON ixc.object_id = kc.parent_object_id AND ixc.index_id = kc.unique_index_id
                    WHERE kc.[type] = 'PK'
                      AND kc.parent_object_id = c.object_id
                      AND ixc.column_id = c.column_id
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsPrimaryKey,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.index_columns ixc
                    INNER JOIN sys.indexes i ON i.object_id = ixc.object_id AND i.index_id = ixc.index_id
                    WHERE i.object_id = c.object_id
                      AND ixc.column_id = c.column_id
                      AND i.is_hypothetical = 0
                      AND i.name IS NOT NULL
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsIndexed,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.foreign_key_columns fkc
                    WHERE fkc.parent_object_id = c.object_id
                      AND fkc.parent_column_id = c.column_id
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsForeignKey
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.columns c ON c.object_id = t.object_id
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
            WHERE s.name = @SchemaName
              AND t.name = @TableName
            ORDER BY c.column_id;
            """;
        command.Parameters.Add(new SqlParameter("@SchemaName", System.Data.SqlDbType.NVarChar, 128) { Value = schemaName });
        command.Parameters.Add(new SqlParameter("@TableName", System.Data.SqlDbType.NVarChar, 128) { Value = tableName });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columnIdOrdinal = reader.GetOrdinal("ColumnId");
        var columnNameOrdinal = reader.GetOrdinal("ColumnName");
        var dataTypeOrdinal = reader.GetOrdinal("DataType");
        var maxLengthOrdinal = reader.GetOrdinal("MaxLength");
        var precisionValueOrdinal = reader.GetOrdinal("PrecisionValue");
        var scaleValueOrdinal = reader.GetOrdinal("ScaleValue");
        var columnCollationOrdinal = reader.GetOrdinal("ColumnCollation");
        var isNullableOrdinal = reader.GetOrdinal("IsNullable");
        var defaultDefinitionOrdinal = reader.GetOrdinal("DefaultDefinition");
        var isIdentityOrdinal = reader.GetOrdinal("IsIdentity");
        var identitySeedOrdinal = reader.GetOrdinal("IdentitySeed");
        var identityIncrementOrdinal = reader.GetOrdinal("IdentityIncrement");
        var isComputedOrdinal = reader.GetOrdinal("IsComputed");
        var computedDefinitionOrdinal = reader.GetOrdinal("ComputedDefinition");
        var isPrimaryKeyOrdinal = reader.GetOrdinal("IsPrimaryKey");
        var isIndexedOrdinal = reader.GetOrdinal("IsIndexed");
        var isForeignKeyOrdinal = reader.GetOrdinal("IsForeignKey");

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new SchemaColumnModel
            {
                // Core Column Identity
                Ordinal = reader.GetInt32(columnIdOrdinal),
                Name = reader.GetString(columnNameOrdinal),
                DataType = reader.GetString(dataTypeOrdinal),
                SchemaName = schemaName,
                TableName = tableName,

                // Data Type Attributes
                MaxLength = await reader.IsDBNullAsync(maxLengthOrdinal, cancellationToken) ? null : reader.GetInt16(maxLengthOrdinal),
                PrecisionValue = reader.GetByte(precisionValueOrdinal),
                ScaleValue = reader.GetByte(scaleValueOrdinal),
                ColumnCollation = await reader.IsDBNullAsync(columnCollationOrdinal, cancellationToken) ? null : reader.GetString(columnCollationOrdinal),
                LengthDisplay = GetLengthDisplay(reader, maxLengthOrdinal, dataTypeOrdinal),
                LengthSortValue = GetLengthSortValue(reader, maxLengthOrdinal, dataTypeOrdinal),

                // Common Column Properties
                IsNullable = reader.GetBoolean(isNullableOrdinal),
                DefaultValue = await reader.IsDBNullAsync(defaultDefinitionOrdinal, cancellationToken) ? null : reader.GetString(defaultDefinitionOrdinal),

                // Special Column Types
                IsIdentity = reader.GetBoolean(isIdentityOrdinal),
                IdentitySeed = await reader.IsDBNullAsync(identitySeedOrdinal, cancellationToken) ? null : reader.GetInt64(identitySeedOrdinal),
                IdentityIncrement = await reader.IsDBNullAsync(identityIncrementOrdinal, cancellationToken) ? null : reader.GetInt64(identityIncrementOrdinal),
                IsComputed = reader.GetBoolean(isComputedOrdinal),
                ComputedDefinition = await reader.IsDBNullAsync(computedDefinitionOrdinal, cancellationToken) ? null : reader.GetString(computedDefinitionOrdinal),

                // Keys and Indexes
                IsPrimaryKey = reader.GetBoolean(isPrimaryKeyOrdinal),
                IsIndexed = reader.GetBoolean(isIndexedOrdinal),
                IsForeignKey = reader.GetBoolean(isForeignKeyOrdinal),

                // Metadata
                Metadata = GetMetadata(reader, columnIdOrdinal, isPrimaryKeyOrdinal, isIndexedOrdinal)
            });
        }

        return columns.OrderBy(column => column.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static SchemaTableModel? ResolveSelectedTable(
        IReadOnlyList<SchemaTableModel> tables,
        string? selectedTableSchemaName,
        string? selectedTableName)
    {
        if (!string.IsNullOrWhiteSpace(selectedTableSchemaName) && !string.IsNullOrWhiteSpace(selectedTableName))
        {
            var exactMatch = tables.FirstOrDefault(table =>
                string.Equals(table.SchemaName, selectedTableSchemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(table.Name, selectedTableName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedTableName))
        {
            var nameMatch = tables.FirstOrDefault(table => string.Equals(table.Name, selectedTableName, StringComparison.OrdinalIgnoreCase));
            if (nameMatch is not null)
            {
                return nameMatch;
            }
        }

        return tables.FirstOrDefault();
    }

    private static async Task<List<SchemaTableModel>> LoadTablesAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var tables = new List<SchemaTableModel>();
        var connectionString = connection.BuildConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                ISNULL(ps.[RowCount], 0) AS [RowCount],
                ISNULL(colStats.[ColumnCount], 0) AS [ColumnCount],
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.key_constraints kc
                    WHERE kc.parent_object_id = t.object_id
                      AND kc.[type] = 'PK'
                ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasPrimaryKey
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            OUTER APPLY (
                SELECT SUM(CAST(ps.row_count AS bigint)) AS [RowCount]
                FROM sys.dm_db_partition_stats ps
                WHERE ps.object_id = t.object_id
                  AND ps.index_id IN (0, 1)
            ) ps
            OUTER APPLY (
                SELECT COUNT(*) AS ColumnCount
                FROM sys.columns c
                WHERE c.object_id = t.object_id
            ) colStats
            ORDER BY s.name, t.name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new SchemaTableModel
            {
                ColumnCount = reader.GetInt32(reader.GetOrdinal("ColumnCount")),
                HasPrimaryKey = reader.GetBoolean(reader.GetOrdinal("HasPrimaryKey")),
                Name = reader.GetString(reader.GetOrdinal("TableName")),
                RowCount = reader.GetInt64(reader.GetOrdinal("RowCount")),
                SchemaName = reader.GetString(reader.GetOrdinal("SchemaName"))
            });
        }

        return tables.OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<SchemaObjectEntryModel>> LoadFunctionsAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        return await LoadObjectEntriesAsync(connection, databaseName, new[] { "FN", "IF", "TF" }, cancellationToken);
    }

    private static async Task<List<SchemaObjectEntryModel>> LoadObjectEntriesAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IReadOnlyCollection<string> objectTypes,
        CancellationToken cancellationToken)
    {
        var items = new List<SchemaObjectEntryModel>();
        var connectionString = connection.BuildConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT s.name AS SchemaName, o.name AS ObjectName
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.type IN ({0})
            ORDER BY s.name, o.name;
            """;
        var parameters = new List<string>();
        for (var i = 0; i < objectTypes.Count; i++)
        {
            var parameterName = $"@ObjectType{i}";
            parameters.Add(parameterName);
            command.Parameters.Add(new SqlParameter(parameterName, System.Data.SqlDbType.NVarChar, 2) { Value = objectTypes.ElementAt(i) });
        }

        command.CommandText = string.Format(command.CommandText, string.Join(", ", parameters));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var objectNameOrdinal = reader.GetOrdinal("ObjectName");

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SchemaObjectEntryModel
            {
                Name = reader.GetString(objectNameOrdinal),
                SchemaName = reader.GetString(schemaNameOrdinal)
            });
        }

        return items.OrderBy(item => item.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<string?> LoadObjectScriptAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string objectKind,
        string objectSchemaName,
        string objectName,
        CancellationToken cancellationToken)
    {
        var connectionString = connection.BuildConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                COALESCE(sm.definition, OBJECT_DEFINITION(o.object_id)) AS ScriptText
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            LEFT JOIN sys.sql_modules sm ON sm.object_id = o.object_id
            WHERE s.name = @SchemaName
              AND o.name = @ObjectName
              AND o.type IN ({0})
            ORDER BY o.type;
            """;

        foreach (var (parameterName, parameterValue) in GetObjectTypeParameters(objectKind))
        {
            command.Parameters.Add(new SqlParameter(parameterName, System.Data.SqlDbType.NVarChar, 2) { Value = parameterValue });
        }

        command.Parameters.Add(new SqlParameter("@SchemaName", System.Data.SqlDbType.NVarChar, 128) { Value = objectSchemaName });
        command.Parameters.Add(new SqlParameter("@ObjectName", System.Data.SqlDbType.NVarChar, 128) { Value = objectName });
        command.CommandText = string.Format(command.CommandText, string.Join(", ", GetObjectTypeParameters(objectKind).Select(parameter => parameter.ParameterName)));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var scriptOrdinal = reader.GetOrdinal("ScriptText");
        return await reader.IsDBNullAsync(scriptOrdinal, cancellationToken) ? null : reader.GetString(scriptOrdinal);
    }

    private static IReadOnlyList<(string ParameterName, string ParameterValue)> GetObjectTypeParameters(string objectKind)
    {
        return objectKind.Trim().ToLowerInvariant() switch
        {
            "view" => [("@ObjectType0", "V")],
            "function" => [("@ObjectType0", "FN"), ("@ObjectType1", "IF"), ("@ObjectType2", "TF")],
            "storedprocedure" => [("@ObjectType0", "P")],
            _ => throw new InvalidOperationException($"Unsupported object kind '{objectKind}'.")
        };
    }

    private static string GetObjectKindLabel(string objectKind)
    {
        return objectKind.Trim().ToLowerInvariant() switch
        {
            "view" => "View",
            "function" => "Function",
            "storedprocedure" => "Stored Procedure",
            _ => objectKind
        };
    }

    private static IReadOnlyList<ScriptLineModel> SplitScriptLines(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

        var normalizedScript = script.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalizedScript.Split('\n');
        var items = new List<ScriptLineModel>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            items.Add(new ScriptLineModel
            {
                LineNumber = i + 1,
                Text = lines[i]
            });
        }

        return items;
    }

    private static string GetMetadata(SqlDataReader reader, int columnIdOrdinal, int isPrimaryKeyOrdinal, int isIndexedOrdinal)
    {
        var metadata = new List<string>();

        var columnId = reader.GetInt32(columnIdOrdinal);
        metadata.Add($"Ordinal {columnId}");

        if (reader.GetBoolean(isPrimaryKeyOrdinal))
        {
            metadata.Add("Primary key");
        }

        if (reader.GetBoolean(isIndexedOrdinal))
        {
            metadata.Add("Indexed");
        }

        if (reader.GetBoolean(reader.GetOrdinal("IsForeignKey")))
        {
            metadata.Add("Foreign key");
        }

        return string.Join(", ", metadata);
    }

    private static string GetLengthDisplay(SqlDataReader reader, int maxLengthOrdinal, int dataTypeOrdinal)
    {
        var dataType = reader.GetString(dataTypeOrdinal).Trim();
        var maxLength = reader.GetInt16(maxLengthOrdinal);

        if (maxLength < 0)
        {
            return "MAX";
        }

        if (IsUnicodeCharacterType(dataType))
        {
            return (maxLength / 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (IsCharacterType(dataType) || IsBinaryType(dataType))
        {
            return maxLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static int? GetLengthSortValue(SqlDataReader reader, int maxLengthOrdinal, int dataTypeOrdinal)
    {
        var dataType = reader.GetString(dataTypeOrdinal).Trim();
        var maxLength = reader.GetInt16(maxLengthOrdinal);

        if (maxLength < 0)
        {
            return int.MaxValue;
        }

        if (IsUnicodeCharacterType(dataType))
        {
            return maxLength / 2;
        }

        if (IsCharacterType(dataType) || IsBinaryType(dataType))
        {
            return maxLength;
        }

        return null;
    }

    private static bool IsCharacterType(string dataType)
    {
        return dataType.Equals("char", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("varchar", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("nchar", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("binary", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("varbinary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnicodeCharacterType(string dataType)
    {
        return dataType.Equals("nchar", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBinaryType(string dataType)
    {
        return dataType.Equals("binary", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("varbinary", StringComparison.OrdinalIgnoreCase);
    }
}
