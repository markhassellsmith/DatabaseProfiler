using DataProfiler.App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DataProfiler.App.Pages.Schema;

public class IndexModel : PageModel
{
    [BindProperty]
    public string? SelectedTableName { get; set; }

    public SchemaBrowserViewModel ViewModel { get; private set; } = new();

    public SelectList TableNames { get; private set; } = default!;

    public string StatusMessage { get; private set; } = "Schema inventory ready.";

    public void OnGet()
    {
        LoadPageModel();
    }

    public void OnPost()
    {
        LoadPageModel();
    }

    private void LoadPageModel()
    {
        var tables = new[]
        {
            "Customers",
            "Invoices",
            "Orders"
        };

        var functions = new[]
        {
            "fnCustomerStatus",
            "fnInvoiceBalance"
        };

        var storedProcedures = new[]
        {
            "uspBuildCustomerSummary",
            "uspRefreshProfilingData"
        };

        var views = new[]
        {
            "CustomerOrderSummary",
            "InvoiceStatusSummary"
        };

        var selectedTableName = string.IsNullOrWhiteSpace(SelectedTableName) ? tables[0] : SelectedTableName;
        var columns = selectedTableName switch
        {
            "Customers" => new[]
            {
                new SchemaColumnModel { Name = "CreatedDate", DataType = "datetime2", DefaultValue = "sysdatetime()", Metadata = "Created timestamp", IsNullable = false },
                new SchemaColumnModel { Name = "CustomerId", DataType = "int", DefaultValue = null, Metadata = "Primary key", IsNullable = false },
                new SchemaColumnModel { Name = "CustomerName", DataType = "nvarchar(200)", DefaultValue = null, Metadata = "Display name", IsNullable = false },
                new SchemaColumnModel { Name = "CustomerType", DataType = "nvarchar(50)", DefaultValue = null, Metadata = "Lookup value", IsNullable = false },
                new SchemaColumnModel { Name = "IsActive", DataType = "bit", DefaultValue = "1", Metadata = "Status flag", IsNullable = false }
            },
            "Invoices" => new[]
            {
                new SchemaColumnModel { Name = "Amount", DataType = "decimal(18,2)", DefaultValue = null, Metadata = "Invoice total", IsNullable = false },
                new SchemaColumnModel { Name = "CustomerId", DataType = "int", DefaultValue = null, Metadata = "Foreign key candidate", IsNullable = false },
                new SchemaColumnModel { Name = "InvoiceDate", DataType = "date", DefaultValue = null, Metadata = "Invoice date", IsNullable = false },
                new SchemaColumnModel { Name = "InvoiceId", DataType = "int", DefaultValue = null, Metadata = "Primary key", IsNullable = false },
                new SchemaColumnModel { Name = "InvoiceNumber", DataType = "nvarchar(50)", DefaultValue = null, Metadata = "Business identifier", IsNullable = false }
            },
            "Orders" => new[]
            {
                new SchemaColumnModel { Name = "CustomerId", DataType = "int", DefaultValue = null, Metadata = "Foreign key candidate", IsNullable = false },
                new SchemaColumnModel { Name = "OrderDate", DataType = "datetime2", DefaultValue = "sysdatetime()", Metadata = "Order timestamp", IsNullable = false },
                new SchemaColumnModel { Name = "OrderId", DataType = "int", DefaultValue = null, Metadata = "Primary key", IsNullable = false },
                new SchemaColumnModel { Name = "OrderStatus", DataType = "nvarchar(50)", DefaultValue = null, Metadata = "Lookup value", IsNullable = false },
                new SchemaColumnModel { Name = "TotalAmount", DataType = "decimal(18,2)", DefaultValue = null, Metadata = "Order total", IsNullable = false }
            },
            _ => Array.Empty<SchemaColumnModel>()
        };

        ViewModel = new SchemaBrowserViewModel
        {
            DatabaseName = "Selected database",
            SelectedTableName = selectedTableName,
            Columns = columns,
            Functions = functions,
            StoredProcedures = storedProcedures,
            Tables = tables,
            Views = views
        };

        TableNames = new SelectList(ViewModel.Tables, ViewModel.SelectedTableName);
        StatusMessage = $"Showing schema inventory for {ViewModel.SelectedTableName}.";
    }
}
