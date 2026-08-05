namespace DatabaseProfiler.App.Models;

public sealed class SchemaObjectEntryModel
{
    public string DisplayName => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}.{Name}";

    public string SelectionValue => string.IsNullOrWhiteSpace(SchemaName) ? Name : $"{SchemaName}|{Name}";

    public string Name { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;
}
