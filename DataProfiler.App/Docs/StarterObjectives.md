Below is a revised Markdown starter document, rewritten to assume that application development begins immediately and evolves alongside the profiling of the first few tables. No extended characters are used.

---

# IRIS Database Documentation and Profiling Starter Objectives With Early Application Development

## Purpose of This Document
This document outlines initial objectives and approaches for documenting the IRIS SQL Server database while simultaneously developing an application that automates schema exploration, profiling, and reporting. It serves as a starting point that can be expanded into a complete project plan.

## MVP Statement
The minimum viable product is a team-friendly web application that connects to a SQL Server database, discovers schema objects and programmable objects, profiles selected tables, and produces exportable reports and SQL scripts. The first release should focus on one working end-to-end workflow rather than full coverage of every possible feature.

## MVP Scope
The MVP should include:
- Schema discovery with table lists, column lists, column types, defaults, and core metadata.
- Discovery and display of views, stored procedures, and functions.
- Basic data profiling statistics for selected tables and columns.
- Min, max, average, and count where those statistics are meaningful.
- Min and max display for date and date-time columns.
- Sorted unique values for simple lookup-style columns where the results are practical to read and report.
- Outlier identification for basic profiling of numeric and other suitable columns.
- Exportable reports and generated SQL scripts for discovered objects.

The MVP should avoid trying to fully profile every column type or generate large exhaustive value lists when those results would be unwieldy.

## MVP Guidance
- Prefer complete schema and programmable object inventory before attempting broad profiling automation.
- Treat sorted unique values as a selective output for small or lookup-like columns only.
- Include min, max, outlier indicators, and unique values for simple lookup-style columns as standard basic profiling output.
- Use date and date-time min/max as standard profiling output because they are easy to read and useful in reports.
- Keep generated scripts and reports available for team review, but do not require every report to contain every possible profile detail.

## Primary Objectives
- Begin application development at the start of the project to support early profiling work.
- Build a reusable engine that retrieves schema information and profiling metrics from the IRIS database.
- Test data profiling features as early as the first few tables.
- Generate management-ready reports directly from the application and refine the output as the project progresses.
- Produce documentation that is repeatable, consistent, and exportable.

## Scope
The scope includes:
- SQL Server schema discovery and extraction.
- Column-level profiling including min, max, null rates, distinct value counts, and frequency distributions.
- Development of an application for schema exploration, profiling, and report export.
- User interface or API endpoints for interactive review of schema and profiling output.
- Export formats including markdown, CSV, Excel, and PDF.

## Development Approach

### Approach A: Build Application Features While Profiling the First Tables
- Start with a lightweight code base that connects to the IRIS database.
- As the first tables are profiled manually, create corresponding automated profiling functions.
- Store results in simple serializable types, such as JSON or CSV, to support early testing.
- Refine output formatting and report structure based on real profiling results.

### Approach B: Incremental Feature Growth
- Add schema discovery modules as needed while exploring new tables.
- Add profiling modules table by table, using lessons learned from earlier work.
- Validate application output against manual checks during early profiling.
- Allow flexibility to adjust the feature set as patterns become clear.

### Approach C: Early Output Testing and Report Refinement
- Export initial reports directly from the application and review with the team.
- Adjust formatting, terminology, or structure based on feedback.
- Use these early tests to align the final report with management expectations.
- Ensure that the application can regenerate reports quickly as the database evolves.

## Expected Deliverables
- A running application capable of reading schema metadata and profiling selected tables.
- A set of profiling queries embedded in the application or generated dynamically.
- Early versions of the management report exported from the application.
- Documentation of each table as it is processed.
- A growing set of reusable methods supporting consistent analysis across the database.

## Team Roles (Suggested)
- Application development lead: responsible for early code structure, database access, and automated profiling modules.
- Schema and profiling lead: responsible for examining tables and validating early results produced by the application.
- Documentation and reporting lead: responsible for organizing outputs, refining report formats, and preparing summary documentation.

## Next Steps
1. Create a minimal application or project structure in Visual Studio.
2. Select one table and prototype schema extraction and profiling in code.
3. Verify that the exported report meets basic expectations.
4. Document the first table using both manual observations and automated results.
5. Expand application features as new tables are examined.
6. Establish a consistent reporting format and refine it during each iteration.

---

If you want, I can generate companion documents such as a design outline, early architecture sketch, or a milestone timeline for the development and profiling phases.