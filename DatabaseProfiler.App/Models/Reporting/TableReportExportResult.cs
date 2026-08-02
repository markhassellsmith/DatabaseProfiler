namespace DatabaseProfiler.App.Models.Reporting;

public sealed record TableReportExportResult(byte[] Content, string ContentType, string FileName);
