namespace DatabaseProfiler.App.Models;

public sealed class SchemaTableModel
{
    public int ColumnCount { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}.{Name}";

    public string SelectionValue => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}|{Name}";

    public bool HasPrimaryKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public long RowCount { get; set; }

    public string SchemaName { get; set; } = string.Empty;
}
