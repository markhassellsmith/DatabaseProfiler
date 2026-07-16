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
    public bool IsForeignKey { get; set; }

    public bool IsIndexed { get; set; }

    public bool IsPrimaryKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public string QualifiedName => string.IsNullOrWhiteSpace(TableName) ? Name : $"{TableName}.{Name}";

    public string? SchemaName { get; set; }

    public string? TableName { get; set; }

    public string DataType { get; set; } = string.Empty;

    public string LengthDisplay { get; set; } = string.Empty;

    public int? LengthSortValue { get; set; }

    public string? DefaultValue { get; set; }

    public string Metadata { get; set; } = string.Empty;

    public bool IsNullable { get; set; }
}
