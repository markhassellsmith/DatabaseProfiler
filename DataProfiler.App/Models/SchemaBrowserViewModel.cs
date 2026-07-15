namespace DataProfiler.App.Models;

public sealed class SchemaBrowserViewModel
{
    public string? DatabaseName { get; set; }

    public string? SelectedTableName { get; set; }

    public IReadOnlyList<SchemaColumnModel> Columns { get; set; } = [];

    public IReadOnlyList<string> Functions { get; set; } = [];

    public IReadOnlyList<string> StoredProcedures { get; set; } = [];

    public IReadOnlyList<string> Tables { get; set; } = [];

    public IReadOnlyList<string> Views { get; set; } = [];
}

public sealed class SchemaColumnModel
{
    public string Name { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string? DefaultValue { get; set; }

    public string Metadata { get; set; } = string.Empty;

    public bool IsNullable { get; set; }
}
