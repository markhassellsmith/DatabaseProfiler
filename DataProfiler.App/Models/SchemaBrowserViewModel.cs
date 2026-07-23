namespace DataProfiler.App.Models;

public sealed class SchemaBrowserViewModel
{
    public string? ServerName { get; set; }

    public string? DatabaseName { get; set; }

    public int ColumnCount { get; set; }

    public string? SelectedTableSchemaName { get; set; }

    public string? SelectedTableName { get; set; }

    public string? SelectedTableDisplayName => string.IsNullOrWhiteSpace(SelectedTableSchemaName)
        ? SelectedTableName
        : string.IsNullOrWhiteSpace(SelectedTableName)
            ? SelectedTableSchemaName
            : $"{SelectedTableSchemaName}.{SelectedTableName}";

    public string? SelectedTableSelectionValue => string.IsNullOrWhiteSpace(SelectedTableSchemaName)
        ? SelectedTableName
        : string.IsNullOrWhiteSpace(SelectedTableName)
            ? SelectedTableSchemaName
            : $"{SelectedTableSchemaName}|{SelectedTableName}";

    public int FunctionCount { get; set; }

    public int StoredProcedureCount { get; set; }

    public int TableCount { get; set; }

    public IReadOnlyList<SchemaColumnModel> Columns { get; set; } = [];

    public IReadOnlyList<SchemaObjectEntryModel> Functions { get; set; } = [];

    public IReadOnlyList<SchemaObjectEntryModel> StoredProcedures { get; set; } = [];

    public IReadOnlyList<SchemaTableModel> Tables { get; set; } = [];

    public IReadOnlyList<SchemaObjectEntryModel> Views { get; set; } = [];

    public int ViewCount { get; set; }
}

public sealed class SchemaColumnModel
{
    // Core Column Identity
    public int Ordinal { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string QualifiedName => string.IsNullOrWhiteSpace(TableName) ? Name : $"{TableName}.{Name}";

    public string? SchemaName { get; set; }

    public string? TableName { get; set; }

    // Data Type Attributes
    public int? MaxLength { get; set; }

    public int? PrecisionValue { get; set; }

    public int? ScaleValue { get; set; }

    public string? ColumnCollation { get; set; }

    public string LengthDisplay { get; set; } = string.Empty;

    public int? LengthSortValue { get; set; }

    // Common Column Properties
    public bool IsNullable { get; set; }

    public string? DefaultValue { get; set; }

    // Special Column Types
    public bool IsIdentity { get; set; }

    public long? IdentitySeed { get; set; }

    public long? IdentityIncrement { get; set; }

    public bool IsComputed { get; set; }

    public string? ComputedDefinition { get; set; }

    // Keys and Indexes
    public bool IsPrimaryKey { get; set; }

    public bool IsIndexed { get; set; }

    public bool IsForeignKey { get; set; }

    // Metadata
    public string Metadata { get; set; } = string.Empty;
}
