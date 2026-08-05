namespace DatabaseProfiler.App.Models.Reporting;

public sealed record ScriptExportResult(byte[] Content, string ContentType, string FileName);