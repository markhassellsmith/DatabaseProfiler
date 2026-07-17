using System.Globalization;
using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using Microsoft.Data.SqlClient;

namespace DataProfiler.App.Services.Profiling;

public sealed class TableProfilingService
{
    public async Task<ProfilingViewModel> ProfileTableAsync(
        ConnectionSessionModel connection,
        string databaseName,
        string? selectedTableSchemaName,
        string? selectedTableName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before profiling can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before profiling can run.");
        }

        var connectionString = connection.BuildConnectionString(databaseName);
        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        var tables = await LoadTablesAsync(sqlConnection, cancellationToken);
        return await ProfileTableAsync(sqlConnection, connection.ServerName, databaseName, tables, selectedTableSchemaName, selectedTableName, cancellationToken);
    }

    public async Task<ProfilingViewModel> ProfileTableAsync(
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
            throw new InvalidOperationException("A server name is required before profiling can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before profiling can run.");
        }

        ArgumentNullException.ThrowIfNull(tables);

        var connectionString = connection.BuildConnectionString(databaseName);
        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        return await ProfileTableAsync(sqlConnection, connection.ServerName, databaseName, tables, selectedTableSchemaName, selectedTableName, cancellationToken);
    }

    private static async Task<ProfilingViewModel> ProfileTableAsync(
        SqlConnection sqlConnection,
        string serverName,
        string databaseName,
        IReadOnlyList<SchemaTableModel> tables,
        string? selectedTableSchemaName,
        string? selectedTableName,
        CancellationToken cancellationToken)
    {
        var selectedTable = ResolveSelectedTable(tables, selectedTableSchemaName, selectedTableName);

        if (selectedTable is null)
        {
            return new ProfilingViewModel
            {
                DatabaseName = databaseName,
                ServerName = serverName,
                Tables = tables
            };
        }

        var columnMetadata = await LoadColumnMetadataAsync(sqlConnection, selectedTable.SchemaName, selectedTable.Name, cancellationToken);
        var columnProfiles = new List<ColumnProfileModel>(columnMetadata.Count);
        foreach (var column in columnMetadata)
        {
            columnProfiles.Add(await LoadColumnProfileAsync(sqlConnection, selectedTable.SchemaName, selectedTable.Name, column, selectedTable.RowCount, cancellationToken));
        }

        return new ProfilingViewModel
        {
            DatabaseName = databaseName,
            Columns = columnProfiles,
            SelectedTableName = selectedTable.Name,
            SelectedTableSchemaName = selectedTable.SchemaName,
            ServerName = serverName,
            RowCount = selectedTable.RowCount,
            Tables = tables
        };
    }

    private static async Task<List<SchemaTableModel>> LoadTablesAsync(
        SqlConnection sqlConnection,
        CancellationToken cancellationToken)
    {
        var tables = new List<SchemaTableModel>();

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
        var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
        var tableNameOrdinal = reader.GetOrdinal("TableName");
        var rowCountOrdinal = reader.GetOrdinal("RowCount");
        var columnCountOrdinal = reader.GetOrdinal("ColumnCount");
        var hasPrimaryKeyOrdinal = reader.GetOrdinal("HasPrimaryKey");

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new SchemaTableModel
            {
                ColumnCount = reader.GetInt32(columnCountOrdinal),
                HasPrimaryKey = reader.GetBoolean(hasPrimaryKeyOrdinal),
                Name = reader.GetString(tableNameOrdinal),
                RowCount = reader.GetInt64(rowCountOrdinal),
                SchemaName = reader.GetString(schemaNameOrdinal)
            });
        }

        return tables.OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static async Task<List<ColumnMetadataModel>> LoadColumnMetadataAsync(
        SqlConnection sqlConnection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnMetadataModel>();

        await using var command = sqlConnection.CreateCommand();
        command.CommandText = """
            SELECT
                c.column_id AS Ordinal,
                c.name AS ColumnName,
                ty.name AS SqlType,
                c.max_length AS MaxLength,
                c.precision AS PrecisionValue,
                c.scale AS ScaleValue
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.columns c ON c.object_id = t.object_id
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE s.name = @SchemaName
              AND t.name = @TableName
            ORDER BY c.column_id;
            """;
        command.Parameters.Add(new SqlParameter("@SchemaName", System.Data.SqlDbType.NVarChar, 128) { Value = schemaName });
        command.Parameters.Add(new SqlParameter("@TableName", System.Data.SqlDbType.NVarChar, 128) { Value = tableName });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ordinalOrdinal = reader.GetOrdinal("Ordinal");
        var columnNameOrdinal = reader.GetOrdinal("ColumnName");
        var sqlTypeOrdinal = reader.GetOrdinal("SqlType");
        var maxLengthOrdinal = reader.GetOrdinal("MaxLength");
        var precisionOrdinal = reader.GetOrdinal("PrecisionValue");
        var scaleOrdinal = reader.GetOrdinal("ScaleValue");

        while (await reader.ReadAsync(cancellationToken))
        {
            var sqlType = reader.GetString(sqlTypeOrdinal);
            var maxLength = reader.GetInt16(maxLengthOrdinal);
            var precision = reader.GetByte(precisionOrdinal);
            var scale = reader.GetByte(scaleOrdinal);

            columns.Add(new ColumnMetadataModel
            {
                DataType = GetDataTypeDisplay(sqlType, maxLength, precision, scale),
                IsAverageSupported = IsAverageSupported(sqlType),
                IsCountDistinctSupported = IsCountDistinctSupported(sqlType),
                IsFrequencySupported = IsFrequencySupported(sqlType),
                IsMinMaxSupported = IsMinMaxSupported(sqlType),
                IsStandardDeviationSupported = IsStandardDeviationSupported(sqlType),
                Name = reader.GetString(columnNameOrdinal),
                Ordinal = reader.GetInt32(ordinalOrdinal),
                SqlType = sqlType
            });
        }

        return columns;
    }

    private static async Task<ColumnProfileModel> LoadColumnProfileAsync(
        SqlConnection sqlConnection,
        string schemaName,
        string tableName,
        ColumnMetadataModel column,
        long rowCount,
        CancellationToken cancellationToken)
    {
        var profile = new ColumnProfileModel
        {
            DataType = column.DataType,
            Name = column.Name,
            Ordinal = column.Ordinal
        };

        var tableReference = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
        var columnReference = QuoteIdentifier(column.Name);

        await using (var command = sqlConnection.CreateCommand())
        {
            command.CommandText = BuildAggregateSql(tableReference, columnReference, column);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var nullCount = reader.IsDBNull(reader.GetOrdinal("NullCount")) ? 0L : reader.GetInt64(reader.GetOrdinal("NullCount"));
                profile.NullCount = nullCount.ToString(CultureInfo.InvariantCulture);
                profile.NullPercent = rowCount == 0 ? string.Empty : $"{(nullCount * 100m / rowCount):0.0}%";

                if (column.IsCountDistinctSupported)
                {
                    profile.CountDistinct = reader.IsDBNull(reader.GetOrdinal("CountDistinct"))
                        ? string.Empty
                        : reader.GetInt32(reader.GetOrdinal("CountDistinct")).ToString(CultureInfo.InvariantCulture);
                }

                if (column.IsMinMaxSupported)
                {
                    profile.MinValue = FormatValue(reader, "MinValue");
                    profile.MaxValue = FormatValue(reader, "MaxValue");
                }

                if (column.IsAverageSupported)
                {
                    profile.AverageValue = FormatValue(reader, "AverageValue");
                }

                if (column.IsStandardDeviationSupported)
                {
                    profile.StandardDeviation = FormatValue(reader, "StandardDeviation");
                }
            }
        }

        if (column.IsFrequencySupported && rowCount > 0)
        {
            await using var command = sqlConnection.CreateCommand();
            command.CommandText = $"""
                SELECT TOP (1)
                    CONVERT(nvarchar(4000), {columnReference}) AS MostFrequentValue,
                    COUNT_BIG(*) AS MostFrequentCount
                FROM {tableReference}
                WHERE {columnReference} IS NOT NULL
                GROUP BY {columnReference}
                ORDER BY COUNT_BIG(*) DESC, CONVERT(nvarchar(4000), {columnReference});
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                profile.MostFrequentValue = reader.IsDBNull(reader.GetOrdinal("MostFrequentValue"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("MostFrequentValue"));
                profile.MostFrequentCount = reader.IsDBNull(reader.GetOrdinal("MostFrequentCount"))
                    ? string.Empty
                    : reader.GetInt64(reader.GetOrdinal("MostFrequentCount")).ToString(CultureInfo.InvariantCulture);
            }
        }

        return profile;
    }

    private static string BuildAggregateSql(string tableReference, string columnReference, ColumnMetadataModel column)
    {
        var countDistinctSql = column.IsCountDistinctSupported
            ? $"COALESCE(COUNT(DISTINCT {columnReference}), 0)"
            : "NULL";

        var minSql = column.IsMinMaxSupported ? $"MIN({columnReference})" : "NULL";
        var maxSql = column.IsMinMaxSupported ? $"MAX({columnReference})" : "NULL";
        var averageSql = column.IsAverageSupported ? $"AVG(CAST({columnReference} AS decimal(38, 10)))" : "NULL";
        var standardDeviationSql = column.IsStandardDeviationSupported ? $"STDEV(CAST({columnReference} AS float))" : "NULL";

        return $"""
            SELECT
                COALESCE(SUM(CASE WHEN {columnReference} IS NULL THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END), 0) AS NullCount,
                {countDistinctSql} AS CountDistinct,
                {minSql} AS MinValue,
                {averageSql} AS AverageValue,
                {maxSql} AS MaxValue,
                {standardDeviationSql} AS StandardDeviation
            FROM {tableReference};
            """;
    }

    private static string FormatValue(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString(),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string GetDataTypeDisplay(string sqlType, short maxLength, byte precision, byte scale)
    {
        return sqlType.Trim().ToLowerInvariant() switch
        {
            "char" or "varchar" or "binary" or "varbinary" => $"{sqlType}({FormatLength(maxLength)})",
            "nchar" or "nvarchar" => $"{sqlType}({FormatLength((short)(maxLength / 2))})",
            "decimal" or "numeric" => $"{sqlType}({precision}, {scale})",
            "datetime2" or "datetimeoffset" or "time" => $"{sqlType}({scale})",
            _ => sqlType
        };
    }

    private static string FormatLength(short length)
    {
        return length < 0 ? "MAX" : length.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsAverageSupported(string sqlType)
    {
        return IsNumericType(sqlType);
    }

    private static bool IsCountDistinctSupported(string sqlType)
    {
        return IsProfileFriendlyType(sqlType);
    }

    private static bool IsFrequencySupported(string sqlType)
    {
        return IsProfileFriendlyType(sqlType);
    }

    private static bool IsMinMaxSupported(string sqlType)
    {
        return IsProfileFriendlyType(sqlType)
            && !sqlType.Equals("bit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandardDeviationSupported(string sqlType)
    {
        return IsNumericType(sqlType);
    }

    private static bool IsNumericType(string sqlType)
    {
        return sqlType.Equals("tinyint", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("smallint", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("int", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("bigint", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("decimal", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("numeric", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("money", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("smallmoney", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("float", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("real", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProfileFriendlyType(string sqlType)
    {
        return IsNumericType(sqlType)
            || sqlType.Equals("bit", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("date", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("datetime", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("datetime2", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("smalldatetime", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("datetimeoffset", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("time", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("char", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("varchar", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("nchar", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("binary", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("varbinary", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"[{identifier.Replace("]", "]]")}]";
    }

    private sealed record ColumnMetadataModel
    {
        public string DataType { get; init; } = string.Empty;

        public bool IsAverageSupported { get; init; }

        public bool IsCountDistinctSupported { get; init; }

        public bool IsFrequencySupported { get; init; }

        public bool IsMinMaxSupported { get; init; }

        public bool IsStandardDeviationSupported { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Ordinal { get; init; }

        public string SqlType { get; init; } = string.Empty;
    }
}
