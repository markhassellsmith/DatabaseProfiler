namespace DataProfiler.App.Models;

public sealed class DatabaseSelectionModel
{
    public string? ServerName { get; set; }

    public string? SelectedDatabaseName { get; set; }

    public IReadOnlyList<string> DatabaseNames { get; set; } = [];
}
