namespace DataProfiler.App.Models;

public sealed class ScriptBrowserViewModel
{
    public string? DatabaseName { get; set; }

    public string? ObjectKindLabel { get; set; }

    public string? ObjectName { get; set; }

    public string? ObjectSchemaName { get; set; }

    public string? ObjectDisplayName => string.IsNullOrWhiteSpace(ObjectSchemaName)
        ? ObjectName
        : string.IsNullOrWhiteSpace(ObjectName)
            ? ObjectSchemaName
            : $"{ObjectSchemaName}.{ObjectName}";

    public string? ScriptStatusMessage { get; set; }

    public string? ServerName { get; set; }

    public IReadOnlyList<ScriptLineModel> ScriptLines { get; set; } = [];
}

public sealed class ScriptLineModel
{
    public int LineNumber { get; set; }

    public string Text { get; set; } = string.Empty;
}
