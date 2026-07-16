namespace DataProfiler.App.Models;

public sealed class ProfilingViewModel
{
    public string? DatabaseName { get; set; }

    public IReadOnlyList<ColumnProfileModel> Columns { get; set; } = [];

    public int ColumnCount => Columns.Count;

    public int CountDistinctColumnCount => Columns.Count(column => !string.IsNullOrWhiteSpace(column.CountDistinct));

    public int NumericStatColumnCount => Columns.Count(column => !string.IsNullOrWhiteSpace(column.AverageValue) || !string.IsNullOrWhiteSpace(column.StandardDeviation));

    public string? SelectedTableDisplayName => string.IsNullOrWhiteSpace(SelectedTableSchemaName)
        ? SelectedTableName
        : string.IsNullOrWhiteSpace(SelectedTableName)
            ? SelectedTableSchemaName
            : $"{SelectedTableSchemaName}.{SelectedTableName}";

    public string? SelectedTableName { get; set; }

    public string? SelectedTableSchemaName { get; set; }

    public string? SelectedTableSelectionValue => string.IsNullOrWhiteSpace(SelectedTableSchemaName)
        ? SelectedTableName
        : string.IsNullOrWhiteSpace(SelectedTableName)
            ? SelectedTableSchemaName
            : $"{SelectedTableSchemaName}|{SelectedTableName}";

    public string? ServerName { get; set; }

    public long RowCount { get; set; }

    public IReadOnlyList<SchemaTableModel> Tables { get; set; } = [];
}

public sealed class ColumnProfileModel
{
    public int Ordinal { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string CountDistinct { get; set; } = string.Empty;

    public string NullCount { get; set; } = string.Empty;

    public string NullPercent { get; set; } = string.Empty;

    public string MinValue { get; set; } = string.Empty;

    public string AverageValue { get; set; } = string.Empty;

    public string MaxValue { get; set; } = string.Empty;

    public string StandardDeviation { get; set; } = string.Empty;

    public string MostFrequentValue { get; set; } = string.Empty;

    public string MostFrequentCount { get; set; } = string.Empty;
}
