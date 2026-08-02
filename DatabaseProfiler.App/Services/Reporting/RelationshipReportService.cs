using System.Globalization;
using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Models.Reporting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DatabaseProfiler.App.Services.Reporting;

public sealed class RelationshipReportService
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Style indices (matching TableReportService pattern)
    private const uint TitleStyleIndex = 0;
    private const uint HeaderStyleIndex = 1;
    private const uint BoldTextStyleIndex = 2;
    private const uint BandedRowStyleIndex = 3;

    public TableReportExportResult GenerateExcelReport(
        RelationshipBrowserViewModel viewModel,
        string serverName,
        string databaseName)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var bytes = CreateWorkbook(viewModel, serverName, databaseName);
        var fileName = CreateFileName(serverName, databaseName);

        return new TableReportExportResult(bytes, ContentType, fileName);
    }

    private static byte[] CreateWorkbook(
        RelationshipBrowserViewModel viewModel,
        string serverName,
        string databaseName)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            CreateStylesheet(workbookPart);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            var explicitRelationships = viewModel.Relationships
                .Where(r => r.Type == RelationshipType.Explicit)
                .ToList();

            var suggestedRelationships = viewModel.Relationships
                .Where(r => r.Type == RelationshipType.Suggested)
                .ToList();

            // Add Explicit FKs sheet
            if (explicitRelationships.Any())
            {
                AddExplicitFKsSheet(workbookPart, sheets, explicitRelationships, serverName, databaseName, 1u);
            }

            // Add Suggested Relationships sheet
            if (suggestedRelationships.Any())
            {
                var sheetId = explicitRelationships.Any() ? 2u : 1u;
                AddSuggestedSheet(workbookPart, sheets, suggestedRelationships, serverName, databaseName, sheetId);
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static void AddExplicitFKsSheet(
        WorkbookPart workbookPart,
        Sheets sheets,
        List<RelationshipModel> relationships,
        string serverName,
        string databaseName,
        uint sheetId)
    {
        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        sheetPart.Worksheet = new Worksheet();

        // Freeze header row
        var sheetViews = new SheetViews();
        var sheetView = new SheetView { WorkbookViewId = 0U };
        sheetView.Append(new Pane
        {
            State = PaneStateValues.Frozen,
            TopLeftCell = "A4",
            VerticalSplit = 3U,
            ActivePane = PaneValues.BottomLeft
        });
        sheetView.Append(new Selection { Pane = PaneValues.BottomLeft });
        sheetViews.Append(sheetView);
        sheetPart.Worksheet.Append(sheetViews);
        sheetPart.Worksheet.Append(sheetData);

        // Add title and metadata
        AppendTitleRow(sheetData, 10U, "Explicit Foreign Key Relationships", TitleStyleIndex);
        AppendTextRow(sheetData, BoldTextStyleIndex, 
            "Server", serverName, 
            "Database", databaseName, 
            "Generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        // Add header row
        AppendHeaderRow(sheetData,
            "Parent Table",
            "Parent Column",
            "Child Table",
            "Child Column",
            "Cardinality",
            "Constraint Name",
            "Delete Action",
            "Update Action",
            "Enabled",
            "Trusted",
            "Indexed");

        // Add data rows
        var rowIndex = 0;
        foreach (var rel in relationships)
        {
            var styleIndex = rowIndex++ % 2 == 0 ? BandedRowStyleIndex : (uint?)null;
            var row = new Row();

            row.Append(CreateTextCell(rel.ParentTableDisplay, styleIndex));
            row.Append(CreateTextCell(rel.ParentColumn, styleIndex));
            row.Append(CreateTextCell(rel.ChildTableDisplay, styleIndex));
            row.Append(CreateTextCell(rel.ChildColumn, styleIndex));
            row.Append(CreateTextCell(rel.Cardinality ?? "", styleIndex));
            row.Append(CreateTextCell(rel.ConstraintName ?? "", styleIndex));
            row.Append(CreateTextCell(rel.DeleteAction ?? "", styleIndex));
            row.Append(CreateTextCell(rel.UpdateAction ?? "", styleIndex));
            row.Append(CreateTextCell(rel.IsEnabled ? "Yes" : "No", styleIndex));
            row.Append(CreateTextCell(rel.IsTrusted ? "Yes" : "No", styleIndex));
            row.Append(CreateTextCell(rel.IsIndexed ? "Yes" : "No", styleIndex));

            sheetData.Append(row);
        }

        // Set column widths
        var columns = new Columns();
        columns.Append(new Column { Min = 1, Max = 1, Width = 25, CustomWidth = true }); // Parent Table
        columns.Append(new Column { Min = 2, Max = 2, Width = 20, CustomWidth = true }); // Parent Column
        columns.Append(new Column { Min = 3, Max = 3, Width = 25, CustomWidth = true }); // Child Table
        columns.Append(new Column { Min = 4, Max = 4, Width = 20, CustomWidth = true }); // Child Column
        columns.Append(new Column { Min = 5, Max = 5, Width = 15, CustomWidth = true }); // Cardinality
        columns.Append(new Column { Min = 6, Max = 6, Width = 30, CustomWidth = true }); // Constraint
        columns.Append(new Column { Min = 7, Max = 7, Width = 15, CustomWidth = true }); // Delete
        columns.Append(new Column { Min = 8, Max = 8, Width = 15, CustomWidth = true }); // Update
        columns.Append(new Column { Min = 9, Max = 9, Width = 10, CustomWidth = true }); // Enabled
        columns.Append(new Column { Min = 10, Max = 10, Width = 10, CustomWidth = true }); // Trusted
        columns.Append(new Column { Min = 11, Max = 11, Width = 10, CustomWidth = true }); // Indexed
        sheetPart.Worksheet.InsertBefore(columns, sheetData);

        var sheet = new Sheet
        {
            Id = workbookPart.GetIdOfPart(sheetPart),
            SheetId = sheetId,
            Name = "Explicit FKs"
        };
        sheets.Append(sheet);
    }

    private static void AddSuggestedSheet(
        WorkbookPart workbookPart,
        Sheets sheets,
        List<RelationshipModel> relationships,
        string serverName,
        string databaseName,
        uint sheetId)
    {
        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        sheetPart.Worksheet = new Worksheet();

        // Freeze header row
        var sheetViews = new SheetViews();
        var sheetView = new SheetView { WorkbookViewId = 0U };
        sheetView.Append(new Pane
        {
            State = PaneStateValues.Frozen,
            TopLeftCell = "A8",
            VerticalSplit = 7U,
            ActivePane = PaneValues.BottomLeft
        });
        sheetView.Append(new Selection { Pane = PaneValues.BottomLeft });
        sheetViews.Append(sheetView);
        sheetPart.Worksheet.Append(sheetViews);
        sheetPart.Worksheet.Append(sheetData);

        // Add title and metadata
        AppendTitleRow(sheetData, 6U, "Suggested Relationships", TitleStyleIndex);
        AppendTextRow(sheetData, BoldTextStyleIndex,
            "Server", serverName,
            "Database", databaseName,
            "Generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendEmptyRow(sheetData);

        // Add confidence level explanation
        AppendTextRow(sheetData, BoldTextStyleIndex, "Confidence Levels:");
        AppendTextRow(sheetData, null, 
            "• High", "Exact table name match (e.g., CustomerID → Customer.ID or Customer.CustomerID)");
        AppendTextRow(sheetData, null,
            "• Medium", "Partial name match - column contains table name and ends with ID");
        AppendEmptyRow(sheetData);

        // Add header row
        AppendHeaderRow(sheetData,
            "Parent Table",
            "Parent Column",
            "Child Table",
            "Child Column",
            "Confidence");

        // Add data rows
        var rowIndex = 0;
        foreach (var rel in relationships)
        {
            var styleIndex = rowIndex++ % 2 == 0 ? BandedRowStyleIndex : (uint?)null;
            var row = new Row();

            row.Append(CreateTextCell(rel.ParentTableDisplay, styleIndex));
            row.Append(CreateTextCell(rel.ParentColumn, styleIndex));
            row.Append(CreateTextCell(rel.ChildTableDisplay, styleIndex));
            row.Append(CreateTextCell(rel.ChildColumn, styleIndex));
            row.Append(CreateTextCell(rel.ConfidenceDisplay, styleIndex));

            sheetData.Append(row);
        }

        // Set column widths
        var columns = new Columns();
        columns.Append(new Column { Min = 1, Max = 1, Width = 25, CustomWidth = true }); // Parent Table
        columns.Append(new Column { Min = 2, Max = 2, Width = 20, CustomWidth = true }); // Parent Column
        columns.Append(new Column { Min = 3, Max = 3, Width = 25, CustomWidth = true }); // Child Table
        columns.Append(new Column { Min = 4, Max = 4, Width = 20, CustomWidth = true }); // Child Column
        columns.Append(new Column { Min = 5, Max = 5, Width = 15, CustomWidth = true }); // Confidence
        sheetPart.Worksheet.InsertBefore(columns, sheetData);

        var sheet = new Sheet
        {
            Id = workbookPart.GetIdOfPart(sheetPart),
            SheetId = sheetId,
            Name = "Suggested"
        };
        sheets.Append(sheet);
    }

    private static string CreateFileName(string serverName, string databaseName)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safeServerName = string.Join("_", serverName.Split(Path.GetInvalidFileNameChars()));
        var safeDatabaseName = string.Join("_", databaseName.Split(Path.GetInvalidFileNameChars()));
        return $"Relationships_{safeServerName}_{safeDatabaseName}_{timestamp}.xlsx";
    }

    // Helper methods (matching TableReportService pattern)
    private static void CreateStylesheet(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet();

        // Fonts
        var fonts = new Fonts { Count = 3 };
        fonts.Append(new Font()); // 0 - Default
        fonts.Append(new Font(new Bold(), new FontSize { Val = 14 })); // 1 - Title
        fonts.Append(new Font(new Bold())); // 2 - Bold

        // Fills
        var fills = new Fills { Count = 3 };
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None })); // 0 - Default
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 })); // 1 - Gray
        fills.Append(new Fill(new PatternFill // 2 - Light blue for banding
        {
            PatternType = PatternValues.Solid,
            ForegroundColor = new ForegroundColor { Rgb = "FFE7F3FF" }
        }));

        // Borders
        var borders = new Borders { Count = 1 };
        borders.Append(new Border());

        // Cell formats
        var cellFormats = new CellFormats { Count = 4 };
        cellFormats.Append(new CellFormat()); // 0 - Title (bold, large)
        cellFormats.Append(new CellFormat { FontId = 1, ApplyFont = true }); // 1 - Header
        cellFormats.Append(new CellFormat { FontId = 2, ApplyFont = true }); // 2 - Bold text
        cellFormats.Append(new CellFormat { FillId = 2, ApplyFill = true }); // 3 - Banded row

        stylesPart.Stylesheet.Fonts = fonts;
        stylesPart.Stylesheet.Fills = fills;
        stylesPart.Stylesheet.Borders = borders;
        stylesPart.Stylesheet.CellFormats = cellFormats;
    }

    private static void AppendTitleRow(SheetData sheetData, uint colSpan, string text, uint? styleIndex)
    {
        var row = new Row();
        var cell = CreateTextCell(text, styleIndex);
        row.Append(cell);
        sheetData.Append(row);
    }

    private static void AppendTextRow(SheetData sheetData, uint? styleIndex, params string[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(CreateTextCell(value, styleIndex));
        }
        sheetData.Append(row);
    }

    private static void AppendHeaderRow(SheetData sheetData, params string[] headers)
    {
        var row = new Row();
        foreach (var header in headers)
        {
            row.Append(CreateTextCell(header, HeaderStyleIndex));
        }
        sheetData.Append(row);
    }

    private static void AppendEmptyRow(SheetData sheetData)
    {
        sheetData.Append(new Row());
    }

    private static Cell CreateTextCell(string text, uint? styleIndex)
    {
        var cell = new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(text))
        };

        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }

        return cell;
    }
}
