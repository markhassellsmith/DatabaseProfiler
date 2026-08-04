# Database Profiler

A web-based SQL Server database exploration and profiling tool that helps database administrators, developers, and analysts quickly understand database structure, generate schema scripts, and analyze data quality.

## Overview

Database Profiler provides a streamlined workflow for exploring SQL Server databases without writing queries. The application offers four core capabilities:

- **Schema Discovery** - Browse database objects, tables, columns, and relationships
- **Relationship Mapping** - Visualize foreign key relationships and generate ERD exports
- **Script Generation** - Export `CREATE TABLE` scripts and schema definitions
- **Data Profiling** - Analyze column statistics, data quality, and value distributions

## Key Features

- 🔍 **Interactive Schema Browser** - Navigate database objects with a clean, intuitive interface
- 🔗 **Relationship Browser** - Explore explicit and suggested foreign key relationships between tables
- 📊 **Automated Data Profiling** - Statistical analysis including nulls, distinct counts, min/max values, and frequency distributions
- 📝 **Script Export** - Generate production-ready `CREATE TABLE` scripts
- 🗺️ **ERD Generation** - Export Entity-Relationship Diagrams in SQL DDL and Mermaid formats
- 📈 **Excel Reporting** - Comprehensive table and relationship reports with hyperlinked navigation
- 🎨 **Theme Support** - Light, dark, and ocean color themes
- ⚡ **V3 Optimized Stored Procedure** - 30-40% faster profiling with the included `usp_ProfileTable_v3_OPTIMIZED` stored procedure

## Workflow

Database Profiler follows a simple, guided workflow:

### 1. Connect to a Server and Database

Start by connecting to your SQL Server instance and selecting a database to explore.

### 2. Object Browser Sections
Click the down-arrow to the right of the Tables, Views, Functions or Stored Procedures section to view lists of objects.
![Object Browser Screenshot](Docs/screenshots/1-object-browser-sections.png)
*Screenshot placeholder: Object Browser showing choice of Tables, Views, Functions or Stored Procedures*

### 3. Clicking on the section for "Tables" displays a list of tables in the database

![Object Browser Screenshot](Docs/screenshots/2-object-browser-tables.png)
*Screenshot placeholder: Object Browser showing list of tables*


### 4. Clicking on a table name opens the Column Browser for that table

Column Browser

Select a table to view detailed column information:
- Column names and data types
- Nullability
- Default values
- Identity columns
- Computed columns
- Extended column descriptions

From here you can:
- Generate `CREATE TABLE` scripts
- Navigate to data profiling

![Column Browser Screenshot](Docs/screenshots/3-column-browser.png)
*Screenshot placeholder: Column Browser showing table columns*


### 5. Clicking on the "Profile Info" link beside a table name opens the data profile for the table 

Data Profiling

Analyze actual data in the selected table:
- **Count Statistics** - Total rows, null counts, distinct values
- **Numeric Analysis** - Min, average, max, standard deviation
- **Text Analysis** - Min/max/average length
- **Date Analysis** - Earliest and latest dates
- **Value Frequency** - Most common values with counts and percentages
- **ID Column Detection** - Automatic handling of identifier columns

![Data Profiling Screenshot](Docs/screenshots/4-data-profiling.png)
*Screenshot placeholder: Data Profiling page showing column statistics*

### 6. Relationship Browser

Explore table relationships and database structure:
- **Explicit Relationships** - Foreign keys defined in the database schema
- **Suggested Relationships** - Automatically detected potential relationships based on column naming patterns
- **Relationship Details** - View parent/child tables, columns, cardinality, and cascade rules
- **Interactive Navigation** - Click table names to jump to their column definitions or profiles

From the Relationships page you can:
- Filter by specific tables or schemas
- Navigate to related tables
- Generate relationship reports

![Relationships Browser Screenshot](Docs/screenshots/5-relationships.png)
*Screenshot placeholder: Relationships Browser showing foreign key relationships*

### 7. Excel Report Generation

Generate comprehensive Excel workbooks for multiple report types:

#### Table Reports
- **Summary Sheet** - Database overview with hyperlinked table navigation
- **Detail Sheets** - Per-table analysis with full profiling results
- **Hyperlinked Navigation** - Jump between summary and detail sheets
- **Professional Formatting** - Styled headers, banded rows, and freeze panes

#### Relationship Reports
- **Relationship Overview** - All explicit and suggested relationships in a single workbook
- **Detailed Metadata** - Parent/child tables, columns, cardinality, cascade rules, and constraint names
- **Excel Export** - Professional formatting with frozen header rows and auto-sized columns

**Report Confirmation Page:**

![Reports Confirm Screenshot](Docs/screenshots/6-reports-confirm.png)
*Screenshot placeholder: Reports confirmation page showing selected tables*

**Sample Excel Report:**

![Excel Report Screenshot](Docs/screenshots/7-excel-report.png)
*Screenshot placeholder: Excel report showing summary and detail sheets*

### 7. Entity-Relationship Diagram (ERD) Export

Generate ERD diagrams for documentation and analysis:
- **SQL DDL Export** - Production-ready `CREATE TABLE` statements with foreign key constraints
- **Mermaid Diagram Export** - Markdown-based ERD diagrams compatible with GitHub, documentation tools, and Mermaid renderers
- **Table Selection** - Choose specific tables to include in the diagram
- **Relationship Options** - Include explicit foreign keys and/or suggested relationships
- **Multi-Format Download** - Export both formats simultaneously or individually

![ERD Export Screenshot](Docs/screenshots/8-ERD.png)
*Screenshot placeholder: ERD export page with table selection and format options*

## Technology Stack

- **Framework**: ASP.NET Core Razor Pages (.NET 10)
- **Database**: SQL Server 2016 or later
- **UI**: Bootstrap 5 with custom theming
- **Excel Generation**: Open XML SDK
- **Background Processing**: Hosted service with in-memory queue

## Getting Started

### Prerequisites

- .NET 10 SDK
- SQL Server 2016 or later
- Windows, Linux, or macOS

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/DatabaseProfiler-VS.git
   cd DatabaseProfiler-VS
   ```

2. **(Optional but Recommended)** Deploy the V3 stored procedure for better performance:

   > 💡 **Performance Optimization for DBAs**  
   > Deploy the `usp_ProfileTable` stored procedure to get **30-40% faster profiling**.  
   > **This is optional** - the application automatically falls back to dynamic SQL if the procedure isn't found.  
   > No errors, no downtime!
   >
   > **Quick Deploy:**
   > - Open the file: `DatabaseProfiler.App/Docs/usp_ProfileTable_v3_OPTIMIZED.sql`
   > - Run it in SSMS on your target database(s)
   > - That's it! The app will automatically detect and use it.
   >
   > 📖 [Step-by-step instructions](DatabaseProfiler.App/Docs/QUICK_START_V3.md)

3. Configure connection settings in `appsettings.json` (optional - can also connect via UI):
   ```json
   {
	 "TableProfiling": {
	   "DefaultSampleSize": 1000000,
	   "DefaultTimeout": 300
	 }
   }
   ```

4. Run the application:
   ```bash
   cd DatabaseProfiler.App
   dotnet run
   ```

5. Navigate to `https://localhost:5001` in your browser

> ℹ️ **Note:** You can start using the application immediately without deploying the stored procedure.  
> Profile data will work right away using the built-in dynamic SQL approach.  
> Deploy the stored procedure later when you're ready for optimized performance.

### Configuration

#### Table Profiling Options

Configure profiling behavior in `appsettings.json`:

```json
{
  "TableProfiling": {
	"DefaultSampleSize": 1000000,
	"DefaultTimeout": 300,
	"DefaultFrequentValueLimit": 10
  }
}
```

#### Connection Security

- SQL Server Authentication (username/password)
- Windows Authentication (trusted connection)
- Credentials are session-based and never persisted

## Deployment

For production deployment, see [Deployment Guide](DatabaseProfiler.App/Docs/DEPLOYMENT_GUIDE.md) for:
- IIS configuration
- Application pool settings
- Timeout configuration for long-running reports

## Documentation

Additional documentation is available in the `DatabaseProfiler.App/Docs/` folder:

- [Deployment Guide](DatabaseProfiler.App/Docs/DEPLOYMENT_GUIDE.md) - Production deployment settings
- [Schema and Profile Design](DatabaseProfiler.App/Docs/SCHEMA_AND_PROFILE_DESIGN.md) - Architecture documentation
- [Field Reference](DatabaseProfiler.App/Docs/FIELD_REFERENCE.md) - Complete field catalog
- [V3 Implementation Notes](DatabaseProfiler.App/Docs/V3_IMPLEMENTATION_COMPLETE.md) - Performance optimization details

## Architecture

- **Presentation Layer**: Razor Pages with server-side rendering
- **Service Layer**: 
  - `SchemaDiscoveryService` - Database metadata and relationship discovery
  - `TableProfilingService` - Data analysis and statistics
  - `TableReportService` - Excel table report generation
  - `RelationshipReportService` - Excel relationship report generation
  - `ErdGenerationService` - ERD export in SQL DDL and Mermaid formats
- **Background Processing**: Report queue with progress tracking
- **Session State**: Connection context and user selections

## Performance

The V3 stored procedure (`usp_ProfileTable_v3_OPTIMIZED`) provides:
- **30-40% faster** profiling compared to dynamic SQL
- Efficient varchar tracking with configurable sample sizes
- Optimized frequent value detection
- Reduced memory overhead

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## License

[Your License Here]

## Support

For questions or issues, please open a GitHub issue or contact [your contact information].

---

**Built with ❤️ using ASP.NET Core and SQL Server**
