using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using Microsoft.Data.SqlClient;

namespace DatabaseProfiler.App.Services.Discovery;

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
        var userDefinedTypes = await LoadUserDefinedTypesAsync(connection, databaseName, cancellationToken);

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
            Views = views,
            UserDefinedTypeCount = userDefinedTypes.Count,
            UserDefinedTypes = userDefinedTypes
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

    public async Task<string?> DiscoverObjectScriptAsync(
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

        return await LoadObjectScriptAsync(connection, databaseName, objectKind, objectSchemaName, objectName, cancellationToken);
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
        // User-defined types require different handling than regular objects
        if (objectKind.Equals("UserDefinedType", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadUserDefinedTypeScriptAsync(connection, databaseName, objectSchemaName, objectName, cancellationToken);
        }

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

    private static async Task<string?> LoadUserDefinedTypeScriptAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string objectSchemaName,
        string objectName,
        CancellationToken cancellationToken)
    {
        var connectionString = connection.BuildConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT 
                s.name AS SchemaName,
                t.name AS TypeName,
                bt.name AS BaseTypeName,
                t.max_length AS MaxLength,
                t.precision AS PrecisionValue,
                t.scale AS ScaleValue,
                t.is_nullable AS IsNullable,
                t.is_table_type AS IsTableType
            FROM sys.types t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            LEFT JOIN sys.types bt ON bt.user_type_id = t.system_type_id AND bt.user_type_id = bt.system_type_id
            WHERE t.is_user_defined = 1
              AND s.name = @SchemaName
              AND t.name = @TypeName;
            """;

        command.Parameters.Add(new SqlParameter("@SchemaName", System.Data.SqlDbType.NVarChar, 128) { Value = objectSchemaName });
        command.Parameters.Add(new SqlParameter("@TypeName", System.Data.SqlDbType.NVarChar, 128) { Value = objectName });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var schemaName = reader.GetString(reader.GetOrdinal("SchemaName"));
        var typeName = reader.GetString(reader.GetOrdinal("TypeName"));
        var isTableType = reader.GetBoolean(reader.GetOrdinal("IsTableType"));

        // Table types require querying the table structure
        if (isTableType)
        {
            return await LoadTableTypeScriptAsync(sqlConnection, schemaName, typeName, cancellationToken);
        }

        // Alias types are simpler
        var baseTypeName = reader.IsDBNull(reader.GetOrdinal("BaseTypeName")) ? "sql_variant" : reader.GetString(reader.GetOrdinal("BaseTypeName"));
        var maxLength = reader.IsDBNull(reader.GetOrdinal("MaxLength")) ? (int?)null : reader.GetInt16(reader.GetOrdinal("MaxLength"));
        var precision = reader.IsDBNull(reader.GetOrdinal("PrecisionValue")) ? (int?)null : reader.GetByte(reader.GetOrdinal("PrecisionValue"));
        var scale = reader.IsDBNull(reader.GetOrdinal("ScaleValue")) ? (int?)null : reader.GetByte(reader.GetOrdinal("ScaleValue"));
        var isNullable = reader.GetBoolean(reader.GetOrdinal("IsNullable"));

        var typeSpec = FormatTypeSpecification(baseTypeName, maxLength, precision, scale);
        var nullSpec = isNullable ? "NULL" : "NOT NULL";

        return $"CREATE TYPE [{schemaName}].[{typeName}] FROM {typeSpec} {nullSpec};";
    }

    private static async Task<string> LoadTableTypeScriptAsync(
        SqlConnection sqlConnection,
        string schemaName,
        string typeName,
        CancellationToken cancellationToken)
    {
        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT 
                c.column_id AS ColumnId,
                c.name AS ColumnName,
                t.name AS DataType,
                c.max_length AS MaxLength,
                c.precision AS PrecisionValue,
                c.scale AS ScaleValue,
                c.is_nullable AS IsNullable,
                c.is_identity AS IsIdentity,
                CASE 
                    WHEN pk.column_id IS NOT NULL THEN 1 
                    ELSE 0 
                END AS IsPrimaryKey
            FROM sys.table_types tt
            INNER JOIN sys.schemas s ON s.schema_id = tt.schema_id
            INNER JOIN sys.columns c ON c.object_id = tt.type_table_object_id
            INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
            LEFT JOIN sys.indexes i ON i.object_id = tt.type_table_object_id AND i.is_primary_key = 1
            LEFT JOIN sys.index_columns pk ON pk.object_id = i.object_id AND pk.index_id = i.index_id AND pk.column_id = c.column_id
            WHERE s.name = @SchemaName
              AND tt.name = @TypeName
            ORDER BY c.column_id;
            """;

        command.Parameters.Add(new SqlParameter("@SchemaName", System.Data.SqlDbType.NVarChar, 128) { Value = schemaName });
        command.Parameters.Add(new SqlParameter("@TypeName", System.Data.SqlDbType.NVarChar, 128) { Value = typeName });

        var columns = new List<string>();
        var primaryKeyColumns = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(reader.GetOrdinal("ColumnName"));
            var dataType = reader.GetString(reader.GetOrdinal("DataType"));
            var maxLength = reader.IsDBNull(reader.GetOrdinal("MaxLength")) ? (int?)null : reader.GetInt16(reader.GetOrdinal("MaxLength"));
            var precision = reader.IsDBNull(reader.GetOrdinal("PrecisionValue")) ? (int?)null : reader.GetByte(reader.GetOrdinal("PrecisionValue"));
            var scale = reader.IsDBNull(reader.GetOrdinal("ScaleValue")) ? (int?)null : reader.GetByte(reader.GetOrdinal("ScaleValue"));
            var isNullable = reader.GetBoolean(reader.GetOrdinal("IsNullable"));
            var isIdentity = reader.GetBoolean(reader.GetOrdinal("IsIdentity"));
            var isPrimaryKey = reader.GetInt32(reader.GetOrdinal("IsPrimaryKey")) == 1;

            var typeSpec = FormatTypeSpecification(dataType, maxLength, precision, scale);
            var identitySpec = isIdentity ? " IDENTITY(1,1)" : "";
            var nullSpec = isNullable ? " NULL" : " NOT NULL";

            columns.Add($"    [{columnName}] {typeSpec}{identitySpec}{nullSpec}");

            if (isPrimaryKey)
            {
                primaryKeyColumns.Add($"[{columnName}]");
            }
        }

        var script = $"CREATE TYPE [{schemaName}].[{typeName}] AS TABLE\r\n(\r\n";
        script += string.Join(",\r\n", columns);

        if (primaryKeyColumns.Count > 0)
        {
            script += ",\r\n    PRIMARY KEY (" + string.Join(", ", primaryKeyColumns) + ")";
        }

        script += "\r\n);";

        return script;
    }

    private static string FormatTypeSpecification(string dataTypeName, int? maxLength, int? precision, int? scale)
    {
        // Handle types with max length (varchar, nvarchar, char, nchar, binary, varbinary)
        if (maxLength.HasValue && maxLength.Value > 0)
        {
            // nvarchar/nchar use half the byte length
            if (dataTypeName.StartsWith("nvar", StringComparison.OrdinalIgnoreCase) ||
                dataTypeName.StartsWith("ncha", StringComparison.OrdinalIgnoreCase))
            {
                var displayLength = maxLength.Value == -1 ? "max" : (maxLength.Value / 2).ToString();
                return $"{dataTypeName}({displayLength})";
            }
            // varchar, char, binary, varbinary
            else if (dataTypeName.Contains("var", StringComparison.OrdinalIgnoreCase) ||
                     dataTypeName.Contains("char", StringComparison.OrdinalIgnoreCase) ||
                     dataTypeName.Contains("binary", StringComparison.OrdinalIgnoreCase))
            {
                var displayLength = maxLength.Value == -1 ? "max" : maxLength.Value.ToString();
                return $"{dataTypeName}({displayLength})";
            }
        }

        // Handle decimal/numeric with precision and scale
        if (precision.HasValue && scale.HasValue &&
            (dataTypeName.Equals("decimal", StringComparison.OrdinalIgnoreCase) ||
             dataTypeName.Equals("numeric", StringComparison.OrdinalIgnoreCase)))
        {
            return $"{dataTypeName}({precision},{scale})";
        }

        // Simple types (int, bit, datetime, etc.)
        return dataTypeName;
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
            "userdefinedtype" => "User-Defined Type",
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

    private static async Task<List<UserDefinedTypeModel>> LoadUserDefinedTypesAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var userDefinedTypes = new List<UserDefinedTypeModel>();
        var connectionString = connection.BuildConnectionString(databaseName);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT 
                s.name AS SchemaName,
                t.name AS TypeName,
                bt.name AS BaseTypeName,
                t.max_length AS MaxLength,
                t.precision AS PrecisionValue,
                t.scale AS ScaleValue
            FROM sys.types t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            LEFT JOIN sys.types bt ON bt.user_type_id = t.system_type_id AND bt.user_type_id = bt.system_type_id
            WHERE t.is_user_defined = 1
            ORDER BY s.name, t.name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var typeNameOrdinal = reader.GetOrdinal("TypeName");
        var baseTypeNameOrdinal = reader.GetOrdinal("BaseTypeName");
        var maxLengthOrdinal = reader.GetOrdinal("MaxLength");
        var precisionOrdinal = reader.GetOrdinal("PrecisionValue");
        var scaleOrdinal = reader.GetOrdinal("ScaleValue");

        while (await reader.ReadAsync(cancellationToken))
        {
            userDefinedTypes.Add(new UserDefinedTypeModel
            {
                SchemaName = reader.GetString(schemaNameOrdinal),
                Name = reader.GetString(typeNameOrdinal),
                BaseTypeName = reader.IsDBNull(baseTypeNameOrdinal) ? string.Empty : reader.GetString(baseTypeNameOrdinal),
                MaxLength = reader.IsDBNull(maxLengthOrdinal) ? null : reader.GetInt16(maxLengthOrdinal),
                Precision = reader.IsDBNull(precisionOrdinal) ? null : reader.GetByte(precisionOrdinal),
                Scale = reader.IsDBNull(scaleOrdinal) ? null : reader.GetByte(scaleOrdinal)
            });
        }

        return userDefinedTypes;
    }

    public async Task<RelationshipBrowserViewModel> DiscoverRelationshipsAsync(
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

        var relationships = await LoadRelationshipsAsync(connection, databaseName, cancellationToken);
        var suggestedRelationships = await LoadSuggestedRelationshipsAsync(connection, databaseName, cancellationToken);

        // Combine explicit and suggested relationships
        var allRelationships = new List<RelationshipModel>(relationships);
        allRelationships.AddRange(suggestedRelationships);

        return new RelationshipBrowserViewModel
        {
            Relationships = allRelationships
        };
    }

    private static async Task<List<RelationshipModel>> LoadRelationshipsAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var relationships = new List<RelationshipModel>();

        var connectionString = connection.BuildConnectionString(databaseName);
        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = @"
            SELECT 
                fk.name AS ConstraintName,
                SCHEMA_NAME(parent_obj.schema_id) AS ChildSchema,
                parent_obj.name AS ChildTable,
                parent_col.name AS ChildColumn,
                SCHEMA_NAME(referenced_obj.schema_id) AS ParentSchema,
                referenced_obj.name AS ParentTable,
                referenced_col.name AS ParentColumn,
                fk.delete_referential_action_desc AS DeleteAction,
                fk.update_referential_action_desc AS UpdateAction,
                fk.is_disabled AS IsDisabled,
                fk.is_not_trusted AS IsNotTrusted,
                CASE 
                    WHEN EXISTS (
                        SELECT 1 
                        FROM sys.index_columns ic
                        INNER JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                        WHERE ic.object_id = fkc.parent_object_id
                          AND ic.column_id = fkc.parent_column_id
                          AND i.is_hypothetical = 0
                    ) THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS IsIndexed,
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM sys.index_columns ic
                        INNER JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                        WHERE ic.object_id = fkc.parent_object_id
                          AND ic.column_id = fkc.parent_column_id
                          AND i.is_unique = 1
                          AND i.is_hypothetical = 0
                    ) THEN 'One-to-One'
                    ELSE 'Many-to-One'
                END AS Cardinality
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.objects parent_obj ON fkc.parent_object_id = parent_obj.object_id
            INNER JOIN sys.columns parent_col ON fkc.parent_object_id = parent_col.object_id 
                AND fkc.parent_column_id = parent_col.column_id
            INNER JOIN sys.objects referenced_obj ON fkc.referenced_object_id = referenced_obj.object_id
            INNER JOIN sys.columns referenced_col ON fkc.referenced_object_id = referenced_col.object_id 
                AND fkc.referenced_column_id = referenced_col.column_id
            ORDER BY 
                ParentSchema,
                ParentTable,
                ParentColumn,
                ChildSchema,
                ChildTable,
                ChildColumn";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var constraintNameOrdinal = reader.GetOrdinal("ConstraintName");
        var childSchemaOrdinal = reader.GetOrdinal("ChildSchema");
        var childTableOrdinal = reader.GetOrdinal("ChildTable");
        var childColumnOrdinal = reader.GetOrdinal("ChildColumn");
        var parentSchemaOrdinal = reader.GetOrdinal("ParentSchema");
        var parentTableOrdinal = reader.GetOrdinal("ParentTable");
        var parentColumnOrdinal = reader.GetOrdinal("ParentColumn");
        var deleteActionOrdinal = reader.GetOrdinal("DeleteAction");
        var updateActionOrdinal = reader.GetOrdinal("UpdateAction");
        var isDisabledOrdinal = reader.GetOrdinal("IsDisabled");
        var isNotTrustedOrdinal = reader.GetOrdinal("IsNotTrusted");
        var isIndexedOrdinal = reader.GetOrdinal("IsIndexed");
        var cardinalityOrdinal = reader.GetOrdinal("Cardinality");

        while (await reader.ReadAsync(cancellationToken))
        {
            relationships.Add(new RelationshipModel
            {
                Type = RelationshipType.Explicit,
                ConstraintName = reader.GetString(constraintNameOrdinal),
                ChildSchema = reader.GetString(childSchemaOrdinal),
                ChildTable = reader.GetString(childTableOrdinal),
                ChildColumn = reader.GetString(childColumnOrdinal),
                ParentSchema = reader.GetString(parentSchemaOrdinal),
                ParentTable = reader.GetString(parentTableOrdinal),
                ParentColumn = reader.GetString(parentColumnOrdinal),
                DeleteAction = reader.GetString(deleteActionOrdinal),
                UpdateAction = reader.GetString(updateActionOrdinal),
                IsEnabled = !reader.GetBoolean(isDisabledOrdinal),
                IsTrusted = !reader.GetBoolean(isNotTrustedOrdinal),
                IsIndexed = reader.GetBoolean(isIndexedOrdinal),
                Cardinality = reader.GetString(cardinalityOrdinal)
            });
        }

        return relationships;
    }

    private static async Task<List<RelationshipModel>> LoadSuggestedRelationshipsAsync(
        ConnectionSessionModel connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var suggestedRelationships = new List<RelationshipModel>();

        var connectionString = connection.BuildConnectionString(databaseName);
        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using var command = sqlConnection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = @"
            -- Find potential implicit relationships based on naming patterns
            WITH PotentialFKs AS (
                SELECT 
                    child_schema = SCHEMA_NAME(child_t.schema_id),
                    child_table = child_t.name,
                    child_column = child_c.name,
                    child_type = TYPE_NAME(child_c.user_type_id),
                    parent_schema = SCHEMA_NAME(parent_t.schema_id),
                    parent_table = parent_t.name,
                    parent_column = parent_c.name,
                    parent_type = TYPE_NAME(parent_c.user_type_id),
                    -- Calculate confidence based on naming patterns
                    confidence = CASE
                        -- High confidence: exact table name match (e.g., CustomerID -> Customer.CustomerID or Customer.ID)
                        WHEN child_c.name = parent_t.name + 'ID' AND parent_c.name IN ('ID', parent_t.name + 'ID') THEN 'High'
                        WHEN child_c.name = parent_t.name + '_ID' AND parent_c.name IN ('ID', parent_t.name + '_ID') THEN 'High'
                        -- Medium confidence: partial match or ID suffix
                        WHEN child_c.name LIKE '%' + parent_t.name + '%' AND child_c.name LIKE '%ID' THEN 'Medium'
                        WHEN child_c.name LIKE parent_t.name + '%' AND parent_c.name = 'ID' THEN 'Medium'
                        -- Low confidence: just ID suffix match
                        WHEN child_c.name LIKE '%ID' AND parent_c.name = 'ID' AND child_c.name <> 'ID' THEN 'Low'
                        ELSE 'Low'
                    END
                FROM sys.tables child_t
                INNER JOIN sys.columns child_c ON child_t.object_id = child_c.object_id
                INNER JOIN sys.tables parent_t ON parent_t.name <> child_t.name
                INNER JOIN sys.columns parent_c ON parent_t.object_id = parent_c.object_id
                WHERE 
                    -- Child column suggests FK (ends with ID or _ID)
                    (child_c.name LIKE '%ID' OR child_c.name LIKE '%_ID')
                    -- Parent column is likely PK (named ID or TableNameID)
                    AND (parent_c.name = 'ID' OR parent_c.name LIKE parent_t.name + '%ID')
                    -- Data types must match
                    AND child_c.user_type_id = parent_c.user_type_id
                    -- Exclude if explicit FK already exists
                    AND NOT EXISTS (
                        SELECT 1 
                        FROM sys.foreign_keys fk
                        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        WHERE fkc.parent_object_id = child_t.object_id
                          AND fkc.parent_column_id = child_c.column_id
                          AND fkc.referenced_object_id = parent_t.object_id
                          AND fkc.referenced_column_id = parent_c.column_id
                    )
            )
            SELECT DISTINCT
                child_schema,
                child_table,
                child_column,
                parent_schema,
                parent_table,
                parent_column,
                confidence
            FROM PotentialFKs
            WHERE confidence IN ('High', 'Medium')  -- Only include Medium and High confidence
            ORDER BY 
                confidence DESC,
                parent_schema,
                parent_table,
                parent_column,
                child_schema,
                child_table,
                child_column";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var childSchemaOrdinal = reader.GetOrdinal("child_schema");
        var childTableOrdinal = reader.GetOrdinal("child_table");
        var childColumnOrdinal = reader.GetOrdinal("child_column");
        var parentSchemaOrdinal = reader.GetOrdinal("parent_schema");
        var parentTableOrdinal = reader.GetOrdinal("parent_table");
        var parentColumnOrdinal = reader.GetOrdinal("parent_column");
        var confidenceOrdinal = reader.GetOrdinal("confidence");

        while (await reader.ReadAsync(cancellationToken))
        {
            var confidenceStr = reader.GetString(confidenceOrdinal);
            var confidence = confidenceStr switch
            {
                "High" => ConfidenceLevel.High,
                "Medium" => ConfidenceLevel.Medium,
                "Low" => ConfidenceLevel.Low,
                _ => ConfidenceLevel.Low
            };

            suggestedRelationships.Add(new RelationshipModel
            {
                Type = RelationshipType.Suggested,
                Confidence = confidence,
                ChildSchema = reader.GetString(childSchemaOrdinal),
                ChildTable = reader.GetString(childTableOrdinal),
                ChildColumn = reader.GetString(childColumnOrdinal),
                ParentSchema = reader.GetString(parentSchemaOrdinal),
                ParentTable = reader.GetString(parentTableOrdinal),
                ParentColumn = reader.GetString(parentColumnOrdinal),
                Cardinality = "Many-to-One (Suggested)",
                IsEnabled = true,
                IsTrusted = true,
                IsIndexed = false
            });
        }

        return suggestedRelationships;
    }
}
