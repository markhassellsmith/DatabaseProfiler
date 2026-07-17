using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using DataProfiler.App.Models.Reporting;
using DataProfiler.App.Services.Reporting;
using Microsoft.VSDiagnostics;

namespace DataProfiler.App.Benchmarks;
[CPUUsageDiagnoser]
public class TableReportServiceBenchmarks
{
    private Func<TableReportModel, byte[]>? _createWorkbook;
    private TableReportModel? _report;
    [GlobalSetup]
    public void Setup()
    {
        var method = typeof(TableReportService).GetMethod("CreateWorkbook", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException("Unable to locate CreateWorkbook.");
        _createWorkbook = (Func<TableReportModel, byte[]>)method.CreateDelegate(typeof(Func<TableReportModel, byte[]>));
        _report = CreateReportModel();
    }

    [Benchmark]
    public byte[] CreateWorkbook()
    {
        return _createWorkbook!(_report!);
    }

    private static TableReportModel CreateReportModel()
    {
        var tables = new List<TableReportTableModel>();
        for (var tableIndex = 1; tableIndex <= 8; tableIndex++)
        {
            var columns = new List<TableReportColumnModel>();
            for (var columnIndex = 1; columnIndex <= 24; columnIndex++)
            {
                columns.Add(new TableReportColumnModel { AverageValue = (columnIndex * 1.25m).ToString(System.Globalization.CultureInfo.InvariantCulture), CountDistinct = (columnIndex * 3).ToString(System.Globalization.CultureInfo.InvariantCulture), DataType = columnIndex % 3 == 0 ? "nvarchar(100)" : "int", DefaultValue = columnIndex % 5 == 0 ? "0" : null, IsForeignKey = columnIndex % 7 == 0, IsIndexed = columnIndex % 4 == 0, IsNullable = columnIndex % 2 == 0, IsPrimaryKey = columnIndex == 1, LengthDisplay = columnIndex % 3 == 0 ? "100" : "4", MaxValue = (columnIndex * 100).ToString(System.Globalization.CultureInfo.InvariantCulture), MinValue = columnIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), MostFrequentCount = (columnIndex * 2).ToString(System.Globalization.CultureInfo.InvariantCulture), MostFrequentValue = $"Value {columnIndex}", Name = $"Column{columnIndex}", NullCount = (columnIndex % 6).ToString(System.Globalization.CultureInfo.InvariantCulture), NullPercent = $"{columnIndex % 10}%", Ordinal = columnIndex, StandardDeviation = (columnIndex * 0.75m).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }

            tables.Add(new TableReportTableModel { ColumnCount = columns.Count, Columns = columns, HasPrimaryKey = true, RowCount = 100000 + tableIndex * 1000, SchemaName = $"schema{tableIndex}", TableName = $"table{tableIndex}" });
        }

        return new TableReportModel
        {
            DatabaseName = "BenchmarkDb",
            GeneratedOnUtc = DateTimeOffset.UtcNow,
            ServerName = "BenchmarkServer",
            Tables = tables
        };
    }
}