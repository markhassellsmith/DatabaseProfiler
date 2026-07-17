namespace DataProfiler.App.Models.Reporting;

public sealed class TableReportModel
{
    public string? DatabaseName { get; init; }

    public DateTimeOffset GeneratedOnUtc { get; init; }

    public string? ServerName { get; init; }

    public IReadOnlyList<TableReportTableModel> Tables { get; init; } = [];
}

public sealed class TableReportTableModel
{
    public IReadOnlyList<TableReportColumnModel> Columns { get; init; } = [];

    public int ColumnCount { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? TableName : $"{SchemaName}.{TableName}";

    public bool HasPrimaryKey { get; init; }

    public long RowCount { get; init; }

    public string SchemaName { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;
}

public sealed class TableReportColumnModel
{
    public string AverageValue { get; init; } = string.Empty;

    public bool IsForeignKey { get; init; }

    public bool IsIndexed { get; init; }

    public bool IsNullable { get; init; }

    public bool IsPrimaryKey { get; init; }

    public string LengthDisplay { get; init; } = string.Empty;

    public string MaxValue { get; init; } = string.Empty;

    public string MinValue { get; init; } = string.Empty;

    public string MostFrequentCount { get; init; } = string.Empty;

    public string MostFrequentValue { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string NullCount { get; init; } = string.Empty;

    public string NullPercent { get; init; } = string.Empty;

    public int Ordinal { get; init; }

    public string StandardDeviation { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public string CountDistinct { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }
}
