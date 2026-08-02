namespace DatabaseProfiler.App.Models;

public sealed class ExportViewModel
{
    public IReadOnlyList<string> ExportFormats { get; set; } = [];

    public IReadOnlyList<string> ScriptObjectTypes { get; set; } = [];
}
