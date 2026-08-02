
# Data Profiling Application Design Outline and Early Architecture Sketch

## Purpose
This document provides a high level design outline and early architecture sketch for the data profiling application. It defines the major components, flow, and early technical structure to support incremental development while profiling the first few tables.

## Design Outline

### 1. Core Goals
- Automate schema discovery and data profiling.
- Produce repeatable, exportable management reports.
- Allow incremental development as new tables are explored.
- Keep the application small, simple, and easy to extend.

### 2. Key Features for the First Iteration
- Connect to SQL Server and read basic schema metadata.
- Run profiling queries for selected tables and columns.
- Identify explicit foreign keys and infer likely relationships when metadata is absent.
- Inventory tables, columns, defaults, core metadata, views, stored procedures, and functions.
- Capture practical profile statistics such as min, max, average, count, outlier indicators, and limited unique-value lists for simple lookup-style columns.
- Save results in JSON or CSV for quick inspection.
- Export a simple management report and generated SQL scripts for discovered objects.
- Provide basic console output or minimal UI for testing.

### 3. Growth Path for Later Iterations
- Add automated profiling for all tables.
- Add basic rules for identifying outliers or anomalies.
- Build a simple web interface or desktop UI.
- Generate multiple report formats (markdown, CSV, Excel, PDF).
- Add search and filter capabilities to explore schema details.

### 4. Development Strategy
- Build the application while profiling a few tables manually.
- Use early profiling results to refine the app.
- Keep components simple, modular, and easy to replace.
- Validate app output against manual checks during each iteration.

---

## Early Architecture Sketch

### 1. Overall Structure
The application can follow a small layered structure:

- Data Access Layer
- Profiling Engine
- Schema Extraction Module
- Report Generator
- UI or Console Layer

Each part grows as new profiling needs are discovered.

### 2. Components

#### A. Database Connector
- Opens connections to SQL Server.
- Executes queries for both schema and profiling.
- Returns data in simple structures.

#### B. Schema Extraction Module
- Reads tables, columns, data types, keys, and constraints.
- Builds a lightweight in-memory model of the database.
- Provides objects like DatabaseInfo, TableInfo, and ColumnInfo.
- Captures explicit relationships first by reading foreign key constraints and related metadata.
- Emits inferred relationship candidates only after explicit relationships are loaded, by comparing naming patterns, key-like columns, and value overlap.
- Also discovers views, stored procedures, and functions so they can be reported and scripted.

#### C. Relationship Discovery Module
- Evaluates candidate parent and child columns.
- Scores inferred relationships based on naming conventions, uniqueness, nullability, data type compatibility, and sample join success.
- Marks relationships as confirmed, probable, or possible for reporting.
- Example output: `Orders.CustomerId -> Customers.CustomerId` (probable, 92%) with evidence such as matching names, compatible int types, low null rate, and high join match rate.

#### D. Profiling Engine
- Accepts a TableInfo or ColumnInfo object.
- Runs profiling queries such as min, max, average, count, null counts, distinct counts, and frequency values.
- Returns standardized profiling results for reporting.
- Supports incremental additions as new profiling needs emerge.
- Includes date and date-time min/max handling, outlier indicators, and limited unique-value output for simple lookup-style columns.

#### E. Result Storage Module
- Saves profiling data to JSON or CSV for easy debugging.
- Supports future export formats without changing the profiling engine.
- Stores generated script artifacts alongside profiling and discovery results.

#### F. Report Generator
- Reads populated schema and profiling data.
- Produces a structured report aligned with management expectations.
- Generates markdown and other formats, plus SQL scripts for tables, views, functions, and procedures.
- Allows refinement during early cycles.

Generated scripts should be exposed through the UI and report exports so users can review or download them alongside the object documentation.

#### G. Interface Layer
- Can be a console app in early development.
- Later can evolve into a small web UI or API.
- Offers commands such as:
  - Scan single table
  - Scan full database
  - Generate report
  - View profiling output

### 3. Data Flow Summary
1. The user selects a table or chooses full database scan.
2. Schema Extraction Module loads schema metadata.
3. Profiling Engine performs analysis on selected tables and columns.
4. Result Storage Module writes raw output to JSON or CSV.
5. Report Generator builds a formatted document.
6. Interface Layer presents results or triggers report export.

### 4. Early Development Notes
- Initial code can be built in a single project.
- Classes can be simple data containers.
- Profiling queries can be added table by table.
- Reports will evolve as you learn what managers expect.
- The architecture allows replacing manual steps with automated ones.

### 5. MVP Alignment Notes
- The first release should focus on one end-to-end workflow from connection to inventory to profiling to export.
- Limit unique-value lists to practical cases and avoid exhaustive output where it becomes hard to read or expensive to generate.
- Treat outlier detection and unique values for lookup-like columns as part of basic profiling rather than advanced analytics.
- Keep script generation and reporting available for all discovered object types, but do not require full automation for every profile detail on day one.

---

If you want, I can also generate:
- Project folder structure
- Suggested class names and responsibilities
- A lightweight development roadmap
- A first pass at typical profiling SQL queries

Just let me know which direction you would like to explore next.