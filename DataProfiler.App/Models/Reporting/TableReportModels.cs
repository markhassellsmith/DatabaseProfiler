namespace DataProfiler.App.Models.Reporting;

public sealed class TableReportModel
{
    public string? DatabaseName { get; init; }

    public string EmptyTablesText { get; init; } = string.Empty;

    public bool IncludeProfileInfo { get; init; }

    public bool IncludeTableDetailSheets { get; init; } = true;

    public DateTimeOffset GeneratedOnUtc { get; init; }

    public string LargestColumnTableName { get; init; } = string.Empty;

    public int LargestColumnTableColumnCount { get; init; }

    public string LargestRowTableName { get; init; } = string.Empty;

    public long LargestRowTableRowCount { get; init; }

    public string SmallestColumnTableName { get; init; } = string.Empty;

    public int SmallestColumnTableColumnCount { get; init; }

    public string? ServerName { get; init; }

    public IReadOnlyList<TableReportTableModel> Tables { get; init; } = [];
}

public sealed class TableReportTableModel
{
    public IReadOnlyList<TableReportColumnModel> Columns { get; init; } = [];

    public int ColumnCount { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? TableName : $"{SchemaName}.{TableName}";

    public bool HasPrimaryKey { get; init; }

    public bool IncludeProfileInfo { get; init; }

    public long RowCount { get; init; }

    public string ProfileScope { get; init; } = string.Empty;

    public string SchemaName { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;
}

public sealed class TableReportColumnModel
{
    // Core Column Identity
    public int Ordinal { get; init; }

    public string Name { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    // Data Type Attributes
    public string LengthDisplay { get; init; } = string.Empty;

    public int? PrecisionValue { get; init; }

    public int? ScaleValue { get; init; }

    public string? ColumnCollation { get; init; }

    // Common Column Properties
    public bool IsNullable { get; init; }

    public string? DefaultValue { get; init; }

    // Special Column Types
    public bool IsIdentity { get; init; }

    public long? IdentitySeed { get; init; }

    public long? IdentityIncrement { get; init; }

    public bool IsComputed { get; init; }

    public string? ComputedDefinition { get; init; }

    // Keys and Indexes
    public bool IsPrimaryKey { get; init; }

    public bool IsIndexed { get; init; }

    public bool IsForeignKey { get; init; }

    // Common Profile Statistics
    public long? RowsProfiled { get; init; }

    public string NullCount { get; init; } = string.Empty;

    public string NullPercent { get; init; } = string.Empty;

    public string CountDistinct { get; init; } = string.Empty;

    public string DistinctPercent { get; init; } = string.Empty;

    // Frequency Analysis
    public string MostFrequentValue { get; init; } = string.Empty;

    public string MostFrequentCount { get; init; } = string.Empty;

    public string MostFrequentPercent { get; init; } = string.Empty;

    // Numeric Profile Statistics
    public string MinValue { get; init; } = string.Empty;

    public string MaxValue { get; init; } = string.Empty;

    public string AverageValue { get; init; } = string.Empty;

    public string StandardDeviation { get; init; } = string.Empty;

    // Character Profile Statistics
    public int? MinLength { get; init; }

    public int? MaxLengthObserved { get; init; }

    public decimal? AverageLength { get; init; }

    public long? EmptyStringCount { get; init; }

    public long? WhitespaceOnlyCount { get; init; }

    // Date/Time Profile Statistics
    public DateTime? MinDateValue { get; init; }

    public DateTime? MaxDateValue { get; init; }

    public int? DateRangeDays { get; init; }

    // Profile Metadata
    public string? ProfileNote { get; init; }
}
