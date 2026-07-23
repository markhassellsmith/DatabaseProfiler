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
        bool includeTableProfileInfo,
        bool includeTableDetailSheets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(selectedTableValues);
        ArgumentNullException.ThrowIfNull(jobId);

        ReportProgress(progress, jobId, "Starting", 5, "Preparing report generation.");

        var report = await BuildReportAsync(connection, databaseName, selectedTableValues, jobId, progress, includeTableProfileInfo, includeTableDetailSheets, cancellationToken);
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
        bool includeTableProfileInfo,
        bool includeTableDetailSheets,
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

            ColumnProfileModel[]? profileColumns = null;
            string profileScope = string.Empty;
            if (includeTableProfileInfo)
            {
                ReportProgress(progress, jobId, "Profiling data", 25 + (index * 40 / Math.Max(selectedKeys.Length, 1)), $"Profiling {table.DisplayName}.");
                var profiling = await _tableProfilingService.ProfileTableAsync(
                    connection,
                    databaseName,
                    tables,
                    table.SchemaName,
                    table.Name,
                    cancellationToken);

                profileColumns = profiling.Columns.ToArray();
                profileScope = profiling.ProfileScope;
            }

            reportTables.Add(new TableReportTableModel
            {
                ColumnCount = table.ColumnCount,
                Columns = MergeColumns(schemaBrowser.Columns, profileColumns),
                HasPrimaryKey = table.HasPrimaryKey,
                IncludeProfileInfo = includeTableProfileInfo,
                RowCount = table.RowCount,
                ProfileScope = profileScope,
                SchemaName = table.SchemaName,
                TableName = table.Name
            });

            ReportProgress(progress, jobId, "Table complete", includeTableProfileInfo ? 35 + (index * 45 / Math.Max(selectedKeys.Length, 1)) : 85, $"Finished {table.DisplayName}.");
        }

        if (reportTables.Count == 0)
        {
            throw new InvalidOperationException("No selected tables could be resolved for the report.");
        }

        reportTables = reportTables
            .OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var emptyTableNames = reportTables
            .Where(table => table.RowCount <= 0)
            .Select(table => table.DisplayName)
            .ToArray();

        var largestRowTable = reportTables
            .OrderByDescending(table => table.RowCount)
            .ThenBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .First();

        var smallestColumnTable = reportTables
            .OrderBy(table => table.ColumnCount)
            .ThenBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .First();

        var largestColumnTable = reportTables
            .OrderByDescending(table => table.ColumnCount)
            .ThenBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
            .First();

        return new TableReportModel
        {
            DatabaseName = databaseName,
            EmptyTablesText = emptyTableNames.Length == 0 ? "None" : string.Join(", ", emptyTableNames),
            IncludeProfileInfo = includeTableProfileInfo,
            IncludeTableDetailSheets = includeTableDetailSheets,
            GeneratedOnUtc = DateTimeOffset.UtcNow,
            LargestColumnTableColumnCount = largestColumnTable.ColumnCount,
            LargestColumnTableName = largestColumnTable.DisplayName,
            LargestRowTableName = largestRowTable.DisplayName,
            LargestRowTableRowCount = largestRowTable.RowCount,
            SmallestColumnTableColumnCount = smallestColumnTable.ColumnCount,
            SmallestColumnTableName = smallestColumnTable.DisplayName,
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
        IReadOnlyList<ColumnProfileModel>? profileColumns)
    {
        var profileLookup = (profileColumns ?? Array.Empty<ColumnProfileModel>())
            .Where(column => !string.IsNullOrWhiteSpace(column.Name))
            .GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return schemaColumns
            .OrderBy(column => column.Ordinal)
            .Select(column =>
            {
                profileLookup.TryGetValue(column.Name, out var profile);

                return new TableReportColumnModel
                {
                    // Core Column Identity
                    Ordinal = column.Ordinal,
                    Name = column.Name,
                    DataType = column.DataType,

                    // Data Type Attributes
                    LengthDisplay = column.LengthDisplay,
                    PrecisionValue = column.PrecisionValue,
                    ScaleValue = column.ScaleValue,
                    ColumnCollation = column.ColumnCollation,

                    // Common Column Properties
                    IsNullable = column.IsNullable,
                    DefaultValue = column.DefaultValue,

                    // Special Column Types
                    IsIdentity = column.IsIdentity,
                    IdentitySeed = column.IdentitySeed,
                    IdentityIncrement = column.IdentityIncrement,
                    IsComputed = column.IsComputed,
                    ComputedDefinition = column.ComputedDefinition,

                    // Keys and Indexes
                    IsPrimaryKey = column.IsPrimaryKey,
                    IsIndexed = column.IsIndexed,
                    IsForeignKey = column.IsForeignKey,

                    // Common Profile Statistics
                    RowsProfiled = profile?.RowsProfiled,
                    NullCount = profile?.NullCount ?? string.Empty,
                    NullPercent = profile?.NullPercent ?? string.Empty,
                    CountDistinct = profile?.CountDistinct ?? string.Empty,
                    DistinctPercent = profile?.DistinctPercent ?? string.Empty,

                    // Frequency Analysis
                    MostFrequentValue = profile?.MostFrequentValue ?? string.Empty,
                    MostFrequentCount = profile?.MostFrequentCount ?? string.Empty,
                    MostFrequentPercent = profile?.MostFrequentPercent ?? string.Empty,

                    // Numeric Profile Statistics
                    MinValue = profile?.MinValue ?? string.Empty,
                    MaxValue = profile?.MaxValue ?? string.Empty,
                    AverageValue = profile?.AverageValue ?? string.Empty,
                    StandardDeviation = profile?.StandardDeviation ?? string.Empty,

                    // Character Profile Statistics
                    MinLength = profile?.MinLength,
                    MaxLengthObserved = profile?.MaxLengthObserved,
                    AverageLength = profile?.AverageLength,
                    EmptyStringCount = profile?.EmptyStringCount,
                    WhitespaceOnlyCount = profile?.WhitespaceOnlyCount,

                    // Date/Time Profile Statistics
                    MinDateValue = profile?.MinDateValue,
                    MaxDateValue = profile?.MaxDateValue,
                    DateRangeDays = profile?.DateRangeDays,

                    // Profile Metadata
                    ProfileNote = profile?.ProfileNote
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

            var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            usedSheetNames.Add("Summary");

            // Pre-calculate sheet names for all tables so we can create hyperlinks in the summary
            var tableSheetNames = new Dictionary<string, string>();
            if (report.IncludeTableDetailSheets)
            {
                foreach (var table in report.Tables)
                {
                    var sheetName = CreateSheetName(table.DisplayName, usedSheetNames);
                    usedSheetNames.Add(sheetName);
                    tableSheetNames[table.DisplayName] = sheetName;
                }
            }

            AddSummarySheet(workbookPart, sheets, report, tableSheetNames);

            var sheetId = 2u;
            if (report.IncludeTableDetailSheets)
            {
                foreach (var table in report.Tables)
                {
                    var sheetName = tableSheetNames[table.DisplayName];
                    AddTableSheet(workbookPart, sheets, table, sheetName, sheetId++);
                }
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static void AddSummarySheet(WorkbookPart workbookPart, Sheets sheets, TableReportModel report, Dictionary<string, string> tableSheetNames)
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
        AppendTitleRow(sheetData, 4U, "Database info", TitleStyleIndex);
        AppendLabelValueRow(sheetData, "Empty tables", report.EmptyTablesText, WrappedBoldTextStyleIndex, WrappedBoldTextStyleIndex);
        AppendTextRow(sheetData, BoldTextStyleIndex, "Most rows", report.LargestRowTableName, report.LargestRowTableRowCount.ToString(CultureInfo.InvariantCulture));
        AppendTextRow(sheetData, BoldTextStyleIndex, "Fewest columns", report.SmallestColumnTableName, report.SmallestColumnTableColumnCount.ToString(CultureInfo.InvariantCulture));
        AppendTextRow(sheetData, BoldTextStyleIndex, "Most columns", report.LargestColumnTableName, report.LargestColumnTableColumnCount.ToString(CultureInfo.InvariantCulture));
        AppendEmptyRow(sheetData);
        AppendTitleRow(sheetData, 4U, "Tables", TitleStyleIndex);
        AppendHeaderRow(sheetData, "Table", "Rows", "Columns", "Primary key", "Profile info", "Link to Table");

        var rowIndex = 0;
        foreach (var table in report.Tables)
        {
            var styleIndex = rowIndex++ % 2 == 0 ? BandedRowStyleIndex : (uint?)null;
            var row = new Row();

            // Add regular data cells
            row.Append(CreateTextCell(table.DisplayName, styleIndex));
            row.Append(CreateNumberCell(table.RowCount.ToString(CultureInfo.InvariantCulture), styleIndex));
            row.Append(CreateNumberCell(table.ColumnCount.ToString(CultureInfo.InvariantCulture), styleIndex));
            row.Append(CreateTextCell(table.HasPrimaryKey ? "Yes" : "No", styleIndex));
            row.Append(CreateTextCell(table.IncludeProfileInfo ? "Yes" : "No", styleIndex));

            // Add hyperlink cell if table has a detail sheet
            if (tableSheetNames.TryGetValue(table.DisplayName, out var sheetName))
            {
                var hyperlinkFormula = $"HYPERLINK(\"#{sheetName}!A1\",\"View details for {table.DisplayName}\")";
                var hyperlinkCell = CreateFormulaCell(hyperlinkFormula, HyperlinkStyleIndex);
                row.Append(hyperlinkCell);
            }
            else
            {
                row.Append(CreateTextCell(string.Empty, styleIndex));
            }

            sheetData.AppendChild(row);
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
            TopLeftCell = "C7",
            VerticalSplit = 6U,   // Freeze top 6 rows (header row is row 6)
            HorizontalSplit = 2U, // Freeze left 2 columns (Ordinal and Column name)
            ActivePane = PaneValues.BottomRight
        });
        sheetView.Append(new Selection { Pane = PaneValues.BottomRight });
        sheetViews.Append(sheetView);
        sheetPart.Worksheet.Append(sheetViews);
        sheetPart.Worksheet.Append(sheetData);

        AppendTitleRow(sheetData, 40U, table.DisplayName, TitleStyleIndex);

        // Add "Back to Summary" hyperlink in cell H1
        var firstRow = sheetData.Elements<Row>().FirstOrDefault();
        if (firstRow is not null)
        {
            var hyperlinkCell = CreateFormulaCell("HYPERLINK(\"#Summary!A1\",\"Back to Summary\")", HyperlinkStyleIndex);
            hyperlinkCell.CellReference = "H1";
            firstRow.Append(hyperlinkCell);
        }

        AppendTextRow(sheetData, BoldTextStyleIndex, "Rows", table.RowCount.ToString(CultureInfo.InvariantCulture), "Columns", table.ColumnCount.ToString(CultureInfo.InvariantCulture));
        AppendTextRow(sheetData, BoldTextStyleIndex, "Primary key", table.HasPrimaryKey ? "Yes" : "No", "Schema", table.SchemaName);
        AppendTextRow(sheetData, BoldTextStyleIndex, "Profile scope", string.IsNullOrWhiteSpace(table.ProfileScope) ? "Unknown" : table.ProfileScope);
        AppendEmptyRow(sheetData);
        if (table.IncludeProfileInfo)
        {
            AppendHeaderRow(
                sheetData,
                // Core Identity
                "Ordinal",
                "Column",
                "Data type",
                // Data Type Attributes
                "Length",
                "Precision",
                "Scale",
                "Collation",
                // Common Properties
                "Nullable",
                "Default",
                // Special Types
                "Identity",
                "Id Seed",
                "Id Increment",
                "Computed",
                "Computed Def",
                // Keys/Indexes
                "PK",
                "FK",
                "Indexed",
                // Common Profile Stats
                "Rows Profiled",
                "Null count",
                "Null %",
                "Distinct",
                "Distinct %",
                // Frequency
                "Most frequent",
                "Freq Count",
                "Freq %",
                // Numeric Stats
                "Min",
                "Max",
                "Average",
                "Std dev",
                // Character Stats
                "Min Len",
                "Max Len",
                "Avg Len",
                "Empty",
                "Whitespace",
                // Date Stats
                "Min Date",
                "Max Date",
                "Date Range",
                // Metadata
                "Note");
        }
        else
        {
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
                "Indexed");
        }

        var rowIndex = 0;
        foreach (var column in table.Columns)
        {
            if (table.IncludeProfileInfo)
            {
                AppendDataRow(
                    sheetData,
                    rowIndex++ % 2 == 0 ? BandedRowStyleIndex : null,
                    // Core Identity
                    column.Ordinal,
                    column.Name,
                    column.DataType,
                    // Data Type Attributes
                    column.LengthDisplay,
                    column.PrecisionValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.ScaleValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.ColumnCollation ?? string.Empty,
                    // Common Properties
                    column.IsNullable ? "Yes" : "No",
                    column.DefaultValue ?? string.Empty,
                    // Special Types
                    column.IsIdentity ? "Yes" : "No",
                    column.IdentitySeed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.IdentityIncrement?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.IsComputed ? "Yes" : "No",
                    column.ComputedDefinition ?? string.Empty,
                    // Keys/Indexes
                    column.IsPrimaryKey ? "Yes" : "No",
                    column.IsForeignKey ? "Yes" : "No",
                    column.IsIndexed ? "Yes" : "No",
                    // Common Profile Stats
                    column.RowsProfiled?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.NullCount,
                    column.NullPercent,
                    column.CountDistinct,
                    column.DistinctPercent,
                    // Frequency
                    column.MostFrequentValue,
                    column.MostFrequentCount,
                    column.MostFrequentPercent,
                    // Numeric Stats
                    column.MinValue,
                    column.MaxValue,
                    column.AverageValue,
                    column.StandardDeviation,
                    // Character Stats
                    column.MinLength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.MaxLengthObserved?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.AverageLength?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                    column.EmptyStringCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    column.WhitespaceOnlyCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    // Date Stats
                    column.MinDateValue?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                    column.MaxDateValue?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                    column.DateRangeDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    // Metadata
                    column.ProfileNote ?? string.Empty);
            }
            else
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
                    column.IsIndexed ? "Yes" : "No");
            }
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

    private static void AppendLabelValueRow(SheetData sheetData, string label, string value, uint labelStyleIndex, uint valueStyleIndex)
    {
        var row = new Row();
        row.Append(CreateTextCell(label, labelStyleIndex));
        row.Append(CreateTextCell(value, valueStyleIndex));
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

    private static Cell CreateFormulaCell(string formula, uint? styleIndex = null)
    {
        var cell = new Cell
        {
            CellFormula = new CellFormula(formula),
            DataType = CellValues.String
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
        fonts.Append(new Font(new Bold(), new Underline(), new Color { Rgb = "FF0563C1" })); // Blue hyperlink style
        fonts.Count = 4U;

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
            new ForegroundColor { Rgb = "FF1F1F1F" },
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
        cellFormats.Append(new CellFormat
        {
            FontId = 1U,
            ApplyFont = true,
            ApplyAlignment = true,
            Alignment = new Alignment
            {
                Vertical = VerticalAlignmentValues.Top,
                WrapText = true
            }
        });
        cellFormats.Append(new CellFormat { FontId = 3U, ApplyFont = true }); // Hyperlink style
        cellFormats.Count = 7U;

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

    private const uint WrappedBoldTextStyleIndex = 5U;

    private const uint HyperlinkStyleIndex = 6U;

    private static string CreateFileName(TableReportModel report)
    {
        var server = SanitizeFileName(report.ServerName ?? "Server");
        var database = SanitizeFileName(report.DatabaseName ?? "Database");
        var timestamp = report.GeneratedOnUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var reportKind = report.IncludeTableDetailSheets
            ? "TableReport"
            : "DatabaseReport";
        return $"{server}_{database}_{reportKind}_{timestamp}.xlsx";
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