using System.Globalization;
using DataProfiler.App.Models;
using DataProfiler.App.Models.Reporting;
using DataProfiler.App.Services.Connections;
using DataProfiler.App.Services.Discovery;
using DataProfiler.App.Services.Profiling;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DataProfiler.App.Services.Reporting;

public sealed class TableReportService
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SchemaDiscoveryService _schemaDiscoveryService;
    private readonly TableProfilingService _tableProfilingService;

    public TableReportService(SchemaDiscoveryService schemaDiscoveryService, TableProfilingService tableProfilingService)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
        _tableProfilingService = tableProfilingService;
    }

    public async Task<TableReportExportResult> GenerateExcelReportAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IEnumerable<string> selectedTableValues,
        string jobId,
        IProgress<TableReportProgressModel>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(selectedTableValues);
        ArgumentNullException.ThrowIfNull(jobId);

        ReportProgress(progress, jobId, "Starting", 5, "Preparing report generation.");

        var report = await BuildReportAsync(connection, databaseName, selectedTableValues, jobId, progress, cancellationToken);
        ReportProgress(progress, jobId, "Rendering workbook", 95, "Building the Excel workbook.");
        var bytes = CreateWorkbook(report);
        var fileName = CreateFileName(report);

        return new TableReportExportResult(bytes, ContentType, fileName);
    }

    private async Task<TableReportModel> BuildReportAsync(
        ConnectionSessionModel connection,
        string databaseName,
        IEnumerable<string> selectedTableValues,
        string jobId,
        IProgress<TableReportProgressModel>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.ServerName))
        {
            throw new InvalidOperationException("A server name is required before report generation can run.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("A database name is required before report generation can run.");
        }

        var selectedKeys = ParseSelectedTableValues(selectedTableValues).ToArray();
        if (selectedKeys.Length == 0)
        {
            throw new InvalidOperationException("Select at least one table before generating a report.");
        }

        ReportProgress(progress, jobId, "Discovering tables", 10, "Loading selected tables.");
        var tables = await _schemaDiscoveryService.DiscoverTablesAsync(connection, databaseName, cancellationToken);
        var tableLookup = tables.ToDictionary(table => table.SelectionValue, StringComparer.OrdinalIgnoreCase);

        var reportTables = new List<TableReportTableModel>(selectedKeys.Length);
        for (var index = 0; index < selectedKeys.Length; index++)
        {
            var key = selectedKeys[index];
            if (!tableLookup.TryGetValue(key.SelectionValue, out var table))
            {
                table = tables.FirstOrDefault(candidate =>
                    string.Equals(candidate.SchemaName, key.SchemaName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.Name, key.TableName, StringComparison.OrdinalIgnoreCase))
                    ?? tables.FirstOrDefault(candidate => string.Equals(candidate.Name, key.TableName, StringComparison.OrdinalIgnoreCase));
            }

            if (table is null)
            {
                continue;
            }

            ReportProgress(progress, jobId, "Reading schema", 15 + (index * 30 / Math.Max(selectedKeys.Length, 1)), $"Loading schema for {table.DisplayName}.");
            var schemaBrowser = await _schemaDiscoveryService.DiscoverColumnBrowserAsync(
                connection,
                databaseName,
                tables,
                table.SchemaName,
                table.Name,
                cancellationToken);

            ReportProgress(progress, jobId, "Profiling data", 25 + (index * 40 / Math.Max(selectedKeys.Length, 1)), $"Profiling {table.DisplayName}.");
            var profiling = await _tableProfilingService.ProfileTableAsync(
                connection,
                databaseName,
                tables,
                table.SchemaName,
                table.Name,
                cancellationToken);

            reportTables.Add(new TableReportTableModel
            {
                ColumnCount = table.ColumnCount,
                Columns = MergeColumns(schemaBrowser.Columns, profiling.Columns),
                HasPrimaryKey = table.HasPrimaryKey,
                RowCount = table.RowCount,
                SchemaName = table.SchemaName,
                TableName = table.Name
            });

            ReportProgress(progress, jobId, "Table complete", 35 + (index * 45 / Math.Max(selectedKeys.Length, 1)), $"Finished {table.DisplayName}.");
        }

        if (reportTables.Count == 0)
        {
            throw new InvalidOperationException("No selected tables could be resolved for the report.");
        }

        reportTables = reportTables
            .OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TableReportModel
        {
            DatabaseName = databaseName,
            GeneratedOnUtc = DateTimeOffset.UtcNow,
            ServerName = connection.ServerName,
            Tables = reportTables
        };
    }

    private static void ReportProgress(IProgress<TableReportProgressModel>? progress, string jobId, string stage, int percentComplete, string message)
    {
        progress?.Report(new TableReportProgressModel
        {
            JobId = jobId,
            CurrentStageStartedOnUtc = DateTimeOffset.UtcNow,
            Message = message,
            PercentComplete = Math.Clamp(percentComplete, 0, 99),
            Stage = stage,
            UpdatedOnUtc = DateTimeOffset.UtcNow
        });
    }

    private static IReadOnlyList<TableReportColumnModel> MergeColumns(
        IReadOnlyList<SchemaColumnModel> schemaColumns,
        IReadOnlyList<ColumnProfileModel> profileColumns)
    {
        var profileLookup = profileColumns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);

        return schemaColumns
            .OrderBy(column => column.Ordinal)
            .Select(column =>
            {
                profileLookup.TryGetValue(column.Name, out var profile);

                return new TableReportColumnModel
                {
                    AverageValue = profile?.AverageValue ?? string.Empty,
                    CountDistinct = profile?.CountDistinct ?? string.Empty,
                    DataType = column.DataType,
                    DefaultValue = column.DefaultValue,
                    IsForeignKey = column.IsForeignKey,
                    IsIndexed = column.IsIndexed,
                    IsNullable = column.IsNullable,
                    IsPrimaryKey = column.IsPrimaryKey,
                    LengthDisplay = column.LengthDisplay,
                    MaxValue = profile?.MaxValue ?? string.Empty,
                    MinValue = profile?.MinValue ?? string.Empty,
                    MostFrequentCount = profile?.MostFrequentCount ?? string.Empty,
                    MostFrequentValue = profile?.MostFrequentValue ?? string.Empty,
                    Name = column.Name,
                    NullCount = profile?.NullCount ?? string.Empty,
                    NullPercent = profile?.NullPercent ?? string.Empty,
                    Ordinal = column.Ordinal,
                    StandardDeviation = profile?.StandardDeviation ?? string.Empty
                };
            })
            .ToArray();
    }

    private static IEnumerable<TableSelectionKey> ParseSelectedTableValues(IEnumerable<string> selectedTableValues)
    {
        foreach (var value in selectedTableValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var parts = value.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                yield return new TableSelectionKey(parts[0], parts[1], value.Trim());
            }
            else
            {
                yield return new TableSelectionKey(string.Empty, value.Trim(), value.Trim());
            }
        }
    }

    private static byte[] CreateWorkbook(TableReportModel report)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            CreateStylesheet(workbookPart);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            AddSummarySheet(workbookPart, sheets, report);

            var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            usedSheetNames.Add("Summary");

            var sheetId = 2u;
            foreach (var table in report.Tables)
            {
                var sheetName = CreateSheetName(table.DisplayName, usedSheetNames);
                usedSheetNames.Add(sheetName);
                AddTableSheet(workbookPart, sheets, table, sheetName, sheetId++);
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static void AddSummarySheet(WorkbookPart workbookPart, Sheets sheets, TableReportModel report)
    {
        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        sheetPart.Worksheet = new Worksheet();
        var summaryViews = new SheetViews();
        var summaryView = new SheetView { WorkbookViewId = 0U };
        summaryView.Append(new Pane
        {
            State = PaneStateValues.Frozen,
            TopLeftCell = "A6",
            VerticalSplit = 5U,
            ActivePane = PaneValues.BottomLeft
        });
        summaryView.Append(new Selection { Pane = PaneValues.BottomLeft });
        summaryViews.Append(summaryView);
        sheetPart.Worksheet.Append(summaryViews);
        sheetPart.Worksheet.Append(sheetData);

        AppendTitleRow(sheetData, 4U, "Table report summary", TitleStyleIndex);
        AppendTextRow(sheetData, BoldTextStyleIndex, "Report generated", report.GeneratedOnUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendTextRow(sheetData, BoldTextStyleIndex, "Server", report.ServerName ?? string.Empty, "Database", report.DatabaseName ?? string.Empty);
        AppendEmptyRow(sheetData);
        AppendHeaderRow(sheetData, "Table", "Rows", "Columns", "Primary key");

        var rowIndex = 0;
        foreach (var table in report.Tables)
        {
            AppendDataRow(sheetData, rowIndex++ % 2 == 0 ? BandedRowStyleIndex : null, table.DisplayName, table.RowCount, table.ColumnCount, table.HasPrimaryKey ? "Yes" : "No");
        }

        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(sheetPart),
            Name = "Summary",
            SheetId = 1u
        });
    }

    private static void AddTableSheet(WorkbookPart workbookPart, Sheets sheets, TableReportTableModel table, string sheetName, uint sheetId)
    {
        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        sheetPart.Worksheet = new Worksheet();
        var sheetViews = new SheetViews();
        var sheetView = new SheetView { WorkbookViewId = 0U };
        sheetView.Append(new Pane
        {
            State = PaneStateValues.Frozen,
            TopLeftCell = "A6",
            VerticalSplit = 5U,
            ActivePane = PaneValues.BottomLeft
        });
        sheetView.Append(new Selection { Pane = PaneValues.BottomLeft });
        sheetViews.Append(sheetView);
        sheetPart.Worksheet.Append(sheetViews);
        sheetPart.Worksheet.Append(sheetData);

        AppendTitleRow(sheetData, 18U, table.DisplayName, TitleStyleIndex);
        AppendTextRow(sheetData, BoldTextStyleIndex, "Rows", table.RowCount.ToString(CultureInfo.InvariantCulture), "Columns", table.ColumnCount.ToString(CultureInfo.InvariantCulture));
        AppendTextRow(sheetData, BoldTextStyleIndex, "Primary key", table.HasPrimaryKey ? "Yes" : "No", "Schema", table.SchemaName);
        AppendEmptyRow(sheetData);
        AppendHeaderRow(
            sheetData,
            "Ordinal",
            "Column",
            "Data type",
            "Length",
            "Nullable",
            "Default",
            "PK",
            "FK",
            "Indexed",
            "Null count",
            "Null %",
            "Distinct",
            "Min",
            "Average",
            "Max",
            "Std dev",
            "Most frequent",
            "Frequency");

        var rowIndex = 0;
        foreach (var column in table.Columns)
        {
            AppendDataRow(
                sheetData,
                rowIndex++ % 2 == 0 ? BandedRowStyleIndex : null,
                column.Ordinal,
                column.Name,
                column.DataType,
                column.LengthDisplay,
                column.IsNullable ? "Yes" : "No",
                column.DefaultValue ?? string.Empty,
                column.IsPrimaryKey ? "Yes" : "No",
                column.IsForeignKey ? "Yes" : "No",
                column.IsIndexed ? "Yes" : "No",
                column.NullCount,
                column.NullPercent,
                column.CountDistinct,
                column.MinValue,
                column.AverageValue,
                column.MaxValue,
                column.StandardDeviation,
                column.MostFrequentValue,
                column.MostFrequentCount);
        }

        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(sheetPart),
            Name = sheetName,
            SheetId = sheetId
        });
    }

    private static void AppendEmptyRow(SheetData sheetData)
    {
        sheetData.AppendChild(new Row());
    }

    private static void AppendHeaderRow(SheetData sheetData, params string[] headers)
    {
        var row = new Row();
        foreach (var header in headers)
        {
            row.Append(CreateTextCell(header, HeaderStyleIndex));
        }

        sheetData.AppendChild(row);
    }

    private static void AppendTitleRow(SheetData sheetData, uint mergeAcrossColumns, string title, uint styleIndex)
    {
        var row = new Row();
        row.Append(CreateTextCell(title, styleIndex));
        sheetData.AppendChild(row);

        if (mergeAcrossColumns > 1)
        {
            var mergeCells = sheetData.Parent?.Parent?.ChildElements.OfType<MergeCells>().FirstOrDefault();
            if (mergeCells is null)
            {
                mergeCells = new MergeCells();
                sheetData.Parent?.Parent?.Append(mergeCells);
            }

            var endColumn = GetColumnName(mergeAcrossColumns);
            mergeCells.Append(new MergeCell { Reference = new StringValue($"A1:{endColumn}1") });
        }
    }

    private static void AppendDataRow(SheetData sheetData, uint? styleIndex, params object?[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(value switch
            {
                null => CreateTextCell(string.Empty, styleIndex),
                bool boolean => CreateTextCell(boolean ? "Yes" : "No", styleIndex),
                byte or sbyte or short or ushort or int or uint or long or ulong => CreateNumberCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, styleIndex),
                float or double or decimal => CreateNumberCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, styleIndex),
                _ => CreateTextCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, styleIndex)
            });
        }

        sheetData.AppendChild(row);
    }

    private static void AppendTextRow(SheetData sheetData, params string[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(CreateTextCell(value));
        }

        sheetData.AppendChild(row);
    }

    private static void AppendTextRow(SheetData sheetData, uint styleIndex, params string[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(CreateTextCell(value, styleIndex));
        }

        sheetData.AppendChild(row);
    }

    private static void AppendDataRow(SheetData sheetData, params object?[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(value switch
            {
                null => CreateTextCell(string.Empty),
                bool boolean => CreateTextCell(boolean ? "Yes" : "No"),
                byte or sbyte or short or ushort or int or uint or long or ulong => CreateNumberCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
                float or double or decimal => CreateNumberCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
                _ => CreateTextCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            });
        }

        sheetData.AppendChild(row);
    }

    private static Cell CreateTextCell(string value, uint? styleIndex = null)
    {
        var cell = new Cell
        {
            DataType = CellValues.String,
            CellValue = new CellValue(value ?? string.Empty)
        };

        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }

        return cell;
    }

    private static Cell CreateNumberCell(string value, uint? styleIndex = null)
    {
        var cell = new Cell
        {
            DataType = CellValues.Number,
            CellValue = new CellValue(value)
        };

        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }

        return cell;
    }

    private static void CreateStylesheet(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        var fonts = new Fonts();
        fonts.Append(new Font());
        fonts.Append(new Font(new Bold()));
        fonts.Append(new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }));
        fonts.Count = 3U;

        var fills = new Fills();
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        fills.Append(new Fill(new PatternFill(
            new ForegroundColor { Rgb = "FFD9D9D9" },
            new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid }));
        fills.Append(new Fill(new PatternFill(
            new ForegroundColor { Rgb = "FFF9FBFD" },
            new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid }));
        fills.Append(new Fill(new PatternFill(
            new ForegroundColor { Rgb = "FFD9E2F3" },
            new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid }));
        fills.Count = 5U;

        var borders = new Borders();
        borders.Append(new Border());
        borders.Count = 1U;

        var cellStyleFormats = new CellStyleFormats();
        cellStyleFormats.Append(new CellFormat());
        cellStyleFormats.Count = 1U;

        var cellFormats = new CellFormats();
        cellFormats.Append(new CellFormat());
        cellFormats.Append(new CellFormat { FontId = 1U, ApplyFont = true });
        cellFormats.Append(new CellFormat { FontId = 1U, FillId = 2U, BorderId = 0U, ApplyFont = true, ApplyFill = true });
        cellFormats.Append(new CellFormat { FillId = 3U, BorderId = 0U, ApplyFill = true });
        cellFormats.Append(new CellFormat { FontId = 2U, FillId = 4U, BorderId = 0U, ApplyFont = true, ApplyFill = true });
        cellFormats.Count = 5U;

        var cellStyles = new CellStyles();
        cellStyles.Append(new CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U });
        cellStyles.Count = 1U;

        var differentialFormats = new DifferentialFormats();
        differentialFormats.Count = 0U;

        var tableStyles = new TableStyles();
        tableStyles.Count = 0U;
        tableStyles.DefaultTableStyle = "TableStyleMedium2";
        tableStyles.DefaultPivotStyle = "PivotStyleLight16";

        var stylesheet = new Stylesheet();
        stylesheet.Append(fonts);
        stylesheet.Append(fills);
        stylesheet.Append(borders);
        stylesheet.Append(cellStyleFormats);
        stylesheet.Append(cellFormats);
        stylesheet.Append(cellStyles);
        stylesheet.Append(differentialFormats);
        stylesheet.Append(tableStyles);
        stylesPart.Stylesheet = stylesheet;

        stylesPart.Stylesheet.Save();
    }

    private const uint BoldTextStyleIndex = 1U;

    private const uint HeaderStyleIndex = 2U;

    private const uint BandedRowStyleIndex = 3U;

    private const uint TitleStyleIndex = 4U;

    private static string CreateFileName(TableReportModel report)
    {
        var server = SanitizeFileName(report.ServerName ?? "Server");
        var database = SanitizeFileName(report.DatabaseName ?? "Database");
        var timestamp = report.GeneratedOnUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{server}_{database}_TableReport_{timestamp}.xlsx";
    }

    private static string CreateSheetName(string displayName, ISet<string> usedSheetNames)
    {
        var sanitized = new string(displayName.Select(character => InvalidSheetNameCharacters.Contains(character) ? '_' : character).ToArray());
        sanitized = string.IsNullOrWhiteSpace(sanitized) ? "Table" : sanitized.Trim();
        sanitized = sanitized.Length <= 31 ? sanitized : sanitized[..31];

        var candidate = sanitized;
        var index = 2;
        while (usedSheetNames.Contains(candidate))
        {
            var suffix = $"-{index++}";
            var length = Math.Min(31 - suffix.Length, sanitized.Length);
            candidate = $"{sanitized[..length]}{suffix}";
        }

        return candidate;
    }

    private static string GetColumnName(uint columnNumber)
    {
        var dividend = (int)columnNumber;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "Report" : value;
    }

    private static readonly HashSet<char> InvalidSheetNameCharacters = new([':', '\\', '/', '?', '*', '[', ']']);

    private sealed record TableSelectionKey(string SchemaName, string TableName, string SelectionValue);
}