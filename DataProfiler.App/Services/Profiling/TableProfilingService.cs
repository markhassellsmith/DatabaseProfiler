using System.Globalization;
using DataProfiler.App.Models;
using DataProfiler.App.Services.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DataProfiler.App.Services.Profiling;

public sealed class TableProfilingService
{
    private readonly TableProfilingPolicyOptions _policyOptions;

    public TableProfilingService(IOptions<TableProfilingPolicyOptions> policyOptions)
    {
        _policyOptions = policyOptions.Value;
    }

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
        return await this.ProfileTableAsync(sqlConnection, connection.ServerName, databaseName, tables, selectedTableSchemaName, selectedTableName, cancellationToken);
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

        return await this.ProfileTableAsync(sqlConnection, connection.ServerName, databaseName, tables, selectedTableSchemaName, selectedTableName, cancellationToken);
    }

    private async Task<ProfilingViewModel> ProfileTableAsync(
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
        ApplyAdaptiveProfilingPolicy(
            selectedTable.RowCount,
            selectedTable.ColumnCount,
            columnMetadata);
        var columnProfiles = await LoadColumnProfilesAsync(
            sqlConnection,
            selectedTable.SchemaName,
            selectedTable.Name,
            columnMetadata,
            selectedTable.RowCount,
            cancellationToken);

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
                IncludeAverage = IsAverageSupported(sqlType),
                IncludeCountDistinct = IsCountDistinctSupported(sqlType),
                IncludeFrequency = IsFrequencySupported(sqlType),
                IncludeMinMax = IsMinMaxSupported(sqlType),
                IncludeStandardDeviation = IsStandardDeviationSupported(sqlType),
                MaxLength = maxLength,
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
        var profiles = await LoadColumnProfilesAsync(sqlConnection, schemaName, tableName, [column], rowCount, cancellationToken);
        return profiles.Count > 0
            ? profiles[0]
            : new ColumnProfileModel
            {
                DataType = column.DataType,
                Name = column.Name,
                Ordinal = column.Ordinal
            };
    }

    private static async Task<List<ColumnProfileModel>> LoadColumnProfilesAsync(
        SqlConnection sqlConnection,
        string schemaName,
        string tableName,
        IReadOnlyList<ColumnMetadataModel> columns,
        long rowCount,
        CancellationToken cancellationToken)
    {
        var profiles = columns
            .Select(column => new ColumnProfileModel
            {
                DataType = column.DataType,
                Name = column.Name,
                Ordinal = column.Ordinal
            })
            .OrderBy(profile => profile.Ordinal)
            .ToList();

        if (profiles.Count == 0)
        {
            return profiles;
        }

        var tableReference = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
        var aggregateSql = BuildAggregateSql(tableReference, columns);
        await using var aggregateCommand = sqlConnection.CreateCommand();
        aggregateCommand.CommandText = aggregateSql;
        aggregateCommand.CommandTimeout = 300; // 5 minutes for complex aggregate queries on large tables

        await using (var reader = await aggregateCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                foreach (var column in columns)
                {
                    var profile = profiles.First(candidate => candidate.Ordinal == column.Ordinal);
                    ReadAggregateProfile(reader, profile, column, rowCount);
                }
            }
        }

        var frequencyProfiles = await LoadColumnFrequencyProfilesAsync(sqlConnection, schemaName, tableName, columns, cancellationToken);
        foreach (var profile in profiles)
        {
            if (frequencyProfiles.TryGetValue(profile.Ordinal, out var frequencyProfile))
            {
                profile.MostFrequentValue = frequencyProfile.MostFrequentValue;
                profile.MostFrequentCount = frequencyProfile.MostFrequentCount;
            }
        }

        return profiles;
    }

    private static async Task<Dictionary<int, (string MostFrequentValue, string MostFrequentCount)>> LoadColumnFrequencyProfilesAsync(
        SqlConnection sqlConnection,
        string schemaName,
        string tableName,
        IReadOnlyList<ColumnMetadataModel> columns,
        CancellationToken cancellationToken)
    {
        var supportedColumns = columns.Where(column => column.IncludeFrequency).ToArray();
        if (supportedColumns.Length == 0)
        {
            return new Dictionary<int, (string MostFrequentValue, string MostFrequentCount)>();
        }

        var tableReference = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
        await using var command = sqlConnection.CreateCommand();
        command.CommandText = BuildFrequencySql(tableReference, supportedColumns);
        command.CommandTimeout = 300; // 5 minutes for frequency analysis with CTEs and window functions

        var results = new Dictionary<int, (string MostFrequentValue, string MostFrequentCount)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ordinalOrdinal = reader.GetOrdinal("Ordinal");
        var valueOrdinal = reader.GetOrdinal("MostFrequentValue");
        var countOrdinal = reader.GetOrdinal("MostFrequentCount");

        while (await reader.ReadAsync(cancellationToken))
        {
            var ordinal = reader.GetInt32(ordinalOrdinal);
            var value = reader.IsDBNull(valueOrdinal) ? string.Empty : reader.GetString(valueOrdinal);
            var count = reader.IsDBNull(countOrdinal) ? string.Empty : reader.GetInt64(countOrdinal).ToString(CultureInfo.InvariantCulture);
            results[ordinal] = (value, count);
        }

        return results;
    }

    private static void ReadAggregateProfile(SqlDataReader reader, ColumnProfileModel profile, ColumnMetadataModel column, long rowCount)
    {
        var nullCount = ReadLong(reader, GetMetricAlias(column.Ordinal, "NullCount"));
        profile.NullCount = nullCount.ToString(CultureInfo.InvariantCulture);
        profile.NullPercent = rowCount == 0 ? string.Empty : $"{(nullCount * 100m / rowCount):0.0}%";

        if (column.IncludeCountDistinct)
        {
            profile.CountDistinct = ReadNullableInt(reader, GetMetricAlias(column.Ordinal, "CountDistinct"))?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (column.IncludeMinMax)
        {
            profile.MinValue = ReadFormattedValue(reader, GetMetricAlias(column.Ordinal, "MinValue"));
            profile.MaxValue = ReadFormattedValue(reader, GetMetricAlias(column.Ordinal, "MaxValue"));
        }

        if (column.IncludeAverage)
        {
            profile.AverageValue = ReadFormattedValue(reader, GetMetricAlias(column.Ordinal, "AverageValue"));
        }

        if (column.IncludeStandardDeviation)
        {
            profile.StandardDeviation = ReadFormattedValue(reader, GetMetricAlias(column.Ordinal, "StandardDeviation"));
        }
    }

    private static string BuildAggregateSql(string tableReference, IReadOnlyList<ColumnMetadataModel> columns)
    {
        var selectExpressions = new List<string>(columns.Count * 6);

        foreach (var column in columns)
        {
            var columnReference = QuoteIdentifier(column.Name);
            var nullCountAlias = GetMetricAlias(column.Ordinal, "NullCount");
            var countDistinctAlias = GetMetricAlias(column.Ordinal, "CountDistinct");
            var minAlias = GetMetricAlias(column.Ordinal, "MinValue");
            var averageAlias = GetMetricAlias(column.Ordinal, "AverageValue");
            var maxAlias = GetMetricAlias(column.Ordinal, "MaxValue");
            var standardDeviationAlias = GetMetricAlias(column.Ordinal, "StandardDeviation");

            selectExpressions.Add($"COALESCE(SUM(CASE WHEN {columnReference} IS NULL THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END), 0) AS {QuoteIdentifier(nullCountAlias)}");
            selectExpressions.Add(column.IncludeCountDistinct
                ? $"COUNT(DISTINCT {columnReference}) AS {QuoteIdentifier(countDistinctAlias)}"
                : $"NULL AS {QuoteIdentifier(countDistinctAlias)}");
            selectExpressions.Add(column.IncludeMinMax
                ? $"MIN({columnReference}) AS {QuoteIdentifier(minAlias)}"
                : $"NULL AS {QuoteIdentifier(minAlias)}");
            selectExpressions.Add(column.IncludeAverage
                ? $"AVG(CAST({columnReference} AS decimal(38, 10))) AS {QuoteIdentifier(averageAlias)}"
                : $"NULL AS {QuoteIdentifier(averageAlias)}");
            selectExpressions.Add(column.IncludeMinMax
                ? $"MAX({columnReference}) AS {QuoteIdentifier(maxAlias)}"
                : $"NULL AS {QuoteIdentifier(maxAlias)}");
            selectExpressions.Add(column.IncludeStandardDeviation
                ? $"STDEV(CAST({columnReference} AS float)) AS {QuoteIdentifier(standardDeviationAlias)}"
                : $"NULL AS {QuoteIdentifier(standardDeviationAlias)}");
        }

        return $"""
            SELECT
                {string.Join(",\n                ", selectExpressions)}
            FROM {tableReference};
            """;
    }

    private static string BuildFrequencySql(string tableReference, IReadOnlyList<ColumnMetadataModel> columns)
    {
        var valuesRows = columns
            .Where(column => column.IncludeFrequency)
            .Select(column => $"({column.Ordinal}, {ToSqlStringLiteral(column.Name)}, CONVERT(nvarchar(4000), {QuoteIdentifier(column.Name)}))")
            .ToArray();

        return $"""
            ;WITH ColumnValues AS (
                SELECT v.Ordinal, v.ColumnName, v.ValueText
                FROM {tableReference}
                CROSS APPLY (VALUES
                    {string.Join(",\n                    ", valuesRows)}
                ) v(Ordinal, ColumnName, ValueText)
                WHERE v.ValueText IS NOT NULL
            ), GroupedValues AS (
                SELECT Ordinal, ColumnName, ValueText, COUNT_BIG(*) AS MostFrequentCount
                FROM ColumnValues
                GROUP BY Ordinal, ColumnName, ValueText
            ), RankedValues AS (
                SELECT Ordinal, ColumnName, ValueText, MostFrequentCount,
                       ROW_NUMBER() OVER (PARTITION BY Ordinal ORDER BY MostFrequentCount DESC, ValueText) AS rn
                FROM GroupedValues
            )
            SELECT Ordinal, ColumnName, ValueText AS MostFrequentValue, MostFrequentCount
            FROM RankedValues
            WHERE rn = 1
            ORDER BY Ordinal;
            """;
    }

    private static long ReadLong(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0L : reader.GetInt64(ordinal);
    }

    private static int? ReadNullableInt(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string ReadFormattedValue(SqlDataReader reader, string columnName)
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

    private static string FormatValue(SqlDataReader reader, string columnName)
    {
        return ReadFormattedValue(reader, columnName);
    }

    private static string GetMetricAlias(int ordinal, string metricName)
    {
        return $"c_{ordinal}_{metricName}";
    }

    private static string ToSqlStringLiteral(string value)
    {
        return $"N'{value.Replace("'", "''")}'";
    }

    private void ApplyAdaptiveProfilingPolicy(long rowCount, int columnCount, IList<ColumnMetadataModel> columns)
    {
        var profileScope = DetermineProfileScope(rowCount, columnCount, _policyOptions);

        foreach (var column in columns)
        {
            column.IncludeAverage = column.IncludeAverage && profileScope != ProfileScope.Massive;
            column.IncludeStandardDeviation = column.IncludeStandardDeviation && profileScope != ProfileScope.Massive;
        }

        if (profileScope is ProfileScope.Lookup or ProfileScope.Detail)
        {
            return;
        }

        var countDistinctCap = profileScope == ProfileScope.Large
            ? _policyOptions.LargeTableMaxCountDistinctColumns
            : _policyOptions.MassiveTableMaxCountDistinctColumns;

        var frequencyCap = profileScope == ProfileScope.Large
            ? _policyOptions.LargeTableMaxFrequencyColumns
            : _policyOptions.MassiveTableMaxFrequencyColumns;

        var countDistinctCandidates = columns
            .Where(column => column.IncludeCountDistinct)
            .OrderByDescending(GetCountDistinctPriority)
            .ThenBy(column => column.Ordinal)
            .Take(countDistinctCap)
            .Select(column => column.Ordinal)
            .ToHashSet();

        var frequencyCandidates = columns
            .Where(column => column.IncludeFrequency)
            .OrderByDescending(GetFrequencyPriority)
            .ThenBy(column => column.Ordinal)
            .Take(frequencyCap)
            .Select(column => column.Ordinal)
            .ToHashSet();

        foreach (var column in columns)
        {
            if (!countDistinctCandidates.Contains(column.Ordinal))
            {
                column.IncludeCountDistinct = false;
            }

            if (!frequencyCandidates.Contains(column.Ordinal))
            {
                column.IncludeFrequency = false;
            }

            if (profileScope == ProfileScope.Massive)
            {
                column.IncludeAverage = column.IncludeAverage && IsNumericType(column.SqlType);
                column.IncludeStandardDeviation = column.IncludeStandardDeviation && IsNumericType(column.SqlType);
            }
        }
    }

    private static ProfileScope DetermineProfileScope(long rowCount, int columnCount, TableProfilingPolicyOptions settings)
    {
        if (rowCount <= settings.LookupTableMaxRowCount && columnCount <= settings.LookupTableMaxColumnCount)
        {
            return ProfileScope.Lookup;
        }

        if (rowCount <= settings.DetailTableMaxRowCount && columnCount <= settings.DetailTableMaxColumnCount)
        {
            return ProfileScope.Detail;
        }

        if (rowCount <= settings.LargeTableMaxRowCount && columnCount <= settings.LargeTableMaxColumnCount)
        {
            return ProfileScope.Large;
        }

        return ProfileScope.Massive;
    }

    private static int GetCountDistinctPriority(ColumnMetadataModel column)
    {
        if (IsNumericType(column.SqlType))
        {
            return 100;
        }

        if (IsTemporalType(column.SqlType))
        {
            return 95;
        }

        if (IsUniqueIdentifierType(column.SqlType))
        {
            return 90;
        }

        if (IsBitType(column.SqlType))
        {
            return 85;
        }

        if (IsShortTextColumn(column))
        {
            return 80 - Math.Min(20, GetEffectiveTextLength(column) / 10);
        }

        if (IsStringType(column.SqlType))
        {
            return 30;
        }

        return 10;
    }

    private static int GetFrequencyPriority(ColumnMetadataModel column)
    {
        if (IsShortTextColumn(column))
        {
            return 100 - Math.Min(20, GetEffectiveTextLength(column) / 10);
        }

        if (IsBitType(column.SqlType))
        {
            return 95;
        }

        if (IsTemporalType(column.SqlType))
        {
            return 80;
        }

        if (IsUniqueIdentifierType(column.SqlType))
        {
            return 75;
        }

        if (IsNumericType(column.SqlType))
        {
            return 60;
        }

        return 10;
    }

    private static bool IsShortTextColumn(ColumnMetadataModel column)
    {
        if (!IsStringType(column.SqlType))
        {
            return false;
        }

        var length = GetEffectiveTextLength(column);
        return length is > 0 and <= 50;
    }

    private static int GetEffectiveTextLength(ColumnMetadataModel column)
    {
        if (column.MaxLength < 0)
        {
            return int.MaxValue;
        }

        return column.SqlType.Trim().ToLowerInvariant() switch
        {
            "nchar" or "nvarchar" => column.MaxLength / 2,
            _ => column.MaxLength
        };
    }

    private static bool IsStringType(string sqlType)
    {
        return sqlType.Equals("char", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("varchar", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("nchar", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("text", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("ntext", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporalType(string sqlType)
    {
        return sqlType.Equals("date", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("datetime", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("datetime2", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("smalldatetime", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("datetimeoffset", StringComparison.OrdinalIgnoreCase)
            || sqlType.Equals("time", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueIdentifierType(string sqlType)
    {
        return sqlType.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBitType(string sqlType)
    {
        return sqlType.Equals("bit", StringComparison.OrdinalIgnoreCase);
    }

    private enum ProfileScope
    {
        Lookup,
        Detail,
        Large,
        Massive
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

        public bool IncludeAverage { get; set; }

        public bool IncludeCountDistinct { get; set; }

        public bool IncludeFrequency { get; set; }

        public bool IncludeMinMax { get; set; }

        public bool IncludeStandardDeviation { get; set; }

        public string Name { get; init; } = string.Empty;

        public short MaxLength { get; init; }

        public int Ordinal { get; init; }

        public string SqlType { get; init; } = string.Empty;
    }

}
