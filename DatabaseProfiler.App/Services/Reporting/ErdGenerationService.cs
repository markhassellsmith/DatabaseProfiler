using DatabaseProfiler.App.Models;
using DatabaseProfiler.App.Services.Connections;
using DatabaseProfiler.App.Services.Discovery;
using System.Text;

namespace DatabaseProfiler.App.Services.Reporting;

/// <summary>
/// Service for generating Entity-Relationship Diagrams in multiple formats.
/// </summary>
public sealed class ErdGenerationService
{
    private readonly SchemaDiscoveryService _schemaDiscoveryService;

    public ErdGenerationService(SchemaDiscoveryService schemaDiscoveryService)
    {
        _schemaDiscoveryService = schemaDiscoveryService ?? throw new ArgumentNullException(nameof(schemaDiscoveryService));
    }

    /// <summary>
    /// Generates SQL DDL script for importing into Microsoft Visio.
    /// </summary>
    public async Task<string> GenerateSqlDdl(
        ConnectionSessionModel connection,
        string databaseName,
        List<string> selectedTableIds,
        bool includeExplicitFKs,
        bool includeSuggestedRelationships,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(selectedTableIds);

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name is required.", nameof(databaseName));
        }

        if (selectedTableIds.Count == 0)
        {
            throw new ArgumentException("At least one table must be selected.", nameof(selectedTableIds));
        }

        // Load table and column metadata
        var allTables = await _schemaDiscoveryService.DiscoverTablesAsync(connection, databaseName, cancellationToken);
        var selectedTables = ParseSelectedTables(allTables, selectedTableIds);

        // Load relationships
        var relationships = await _schemaDiscoveryService.DiscoverRelationshipsAsync(
            connection,
            databaseName,
            cancellationToken);

        // Filter relationships to only those between selected tables
        var relevantRelationships = relationships.Relationships
            .Where(r => IsTableSelected(r.ParentSchema, r.ParentTable, selectedTableIds) &&
                       IsTableSelected(r.ChildSchema, r.ChildTable, selectedTableIds))
            .ToList();

        var explicitFKs = relevantRelationships
            .Where(r => r.Type == RelationshipType.Explicit)
            .ToList();

        var suggestedFKs = relevantRelationships
            .Where(r => r.Type == RelationshipType.Suggested)
            .ToList();

        // Generate SQL DDL
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("-- ========================================================================");
        sb.AppendLine("-- Entity-Relationship Diagram Export (SQL DDL)");
        sb.AppendLine("-- ========================================================================");
        sb.AppendLine($"-- Database:  {databaseName}");
        sb.AppendLine($"-- Server:    {connection.ServerName}");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Tables:    {selectedTables.Count}");
        sb.AppendLine($"-- Explicit Foreign Keys: {explicitFKs.Count}");
        sb.AppendLine($"-- Suggested Relationships: {suggestedFKs.Count}");
        sb.AppendLine("-- ========================================================================");
        sb.AppendLine();
        sb.AppendLine("-- USAGE:");
        sb.AppendLine("-- 1. Open Microsoft Visio Professional");
        sb.AppendLine("-- 2. File → New → Database Model Diagram");
        sb.AppendLine("-- 3. Database → Reverse Engineer");
        sb.AppendLine("-- 4. Select this .sql file");
        sb.AppendLine("-- 5. Visio will generate a professional ERD with crow's foot notation");
        sb.AppendLine("--");
        sb.AppendLine("-- NOTE: This script is for documentation/diagramming purposes.");
        sb.AppendLine("--       Execute with caution if applying to an actual database.");
        sb.AppendLine("-- ========================================================================");
        sb.AppendLine();

        // Generate CREATE TABLE statements for each selected table
        foreach (var table in selectedTables)
        {
            // Load columns for this table
            var browserViewModel = await _schemaDiscoveryService.DiscoverColumnBrowserAsync(
                connection,
                databaseName,
                allTables,
                table.SchemaName,
                table.Name,
                cancellationToken);

            var columns = browserViewModel.Columns;

            sb.AppendLine("-- ========================================================================");
            sb.AppendLine($"-- Table: {table.DisplayName}");
            sb.AppendLine($"-- Rows: {table.RowCount:N0} | Columns: {table.ColumnCount}");
            sb.AppendLine("-- ========================================================================");
            sb.AppendLine($"CREATE TABLE [{table.SchemaName}].[{table.Name}] (");

            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var comma = i < columns.Count - 1 ? "," : "";

                // Build column definition
                var nullability = column.IsNullable ? "NULL" : "NOT NULL";
                var identity = column.IsIdentity ? $" IDENTITY({column.IdentitySeed},{column.IdentityIncrement})" : "";

                sb.AppendLine($"    [{column.Name}] {column.DataType} {nullability}{identity}{comma}");
            }

            // Add PRIMARY KEY constraint if exists
            var pkColumns = columns.Where(c => c.IsPrimaryKey).ToList();
            if (pkColumns.Count > 0)
            {
                var pkColumnNames = string.Join(", ", pkColumns.Select(c => $"[{c.Name}]"));
                sb.AppendLine($"    CONSTRAINT [PK_{table.Name}] PRIMARY KEY ({pkColumnNames})");
            }

            sb.AppendLine(");");
            sb.AppendLine();
        }

        // Generate explicit FK constraints
        if (includeExplicitFKs && explicitFKs.Count > 0)
        {
            sb.AppendLine("-- ========================================================================");
            sb.AppendLine("-- FOREIGN KEY CONSTRAINTS (Explicit Relationships)");
            sb.AppendLine("-- ========================================================================");
            sb.AppendLine();

            foreach (var fk in explicitFKs)
            {
                var constraintName = !string.IsNullOrWhiteSpace(fk.ConstraintName)
                    ? fk.ConstraintName
                    : $"FK_{fk.ChildTable}_{fk.ParentTable}";

                sb.AppendLine($"-- {fk.RelationshipDisplay} ({fk.Cardinality})");
                sb.AppendLine($"ALTER TABLE [{fk.ChildSchema}].[{fk.ChildTable}]");
                sb.AppendLine($"    ADD CONSTRAINT [{constraintName}]");
                sb.AppendLine($"    FOREIGN KEY ([{fk.ChildColumn}])");
                sb.AppendLine($"    REFERENCES [{fk.ParentSchema}].[{fk.ParentTable}] ([{fk.ParentColumn}])");

                if (!string.IsNullOrWhiteSpace(fk.DeleteAction))
                {
                    sb.AppendLine($"    ON DELETE {fk.DeleteAction}");
                }
                if (!string.IsNullOrWhiteSpace(fk.UpdateAction))
                {
                    sb.AppendLine($"    ON UPDATE {fk.UpdateAction}");
                }

                sb.AppendLine(";");
                sb.AppendLine();
            }
        }

        // Generate suggested relationships as comments
        if (includeSuggestedRelationships && suggestedFKs.Count > 0)
        {
            sb.AppendLine("-- ========================================================================");
            sb.AppendLine("-- SUGGESTED RELATIONSHIPS (Inferred from Naming Patterns)");
            sb.AppendLine("-- ========================================================================");
            sb.AppendLine("--");
            sb.AppendLine("-- These relationships are NOT actual foreign key constraints in the database.");
            sb.AppendLine("-- They have been inferred based on column naming patterns and data types.");
            sb.AppendLine("-- Review carefully before uncommenting and applying to your database.");
            sb.AppendLine("--");
            sb.AppendLine("-- Confidence Levels:");
            sb.AppendLine("--   High   = Exact table name match (e.g., CustomerID → Customer.CustomerID)");
            sb.AppendLine("--   Medium = Partial pattern or ID suffix (e.g., CustID → Customer.CustomerID)");
            sb.AppendLine("--   Low    = Weak indicators or data type match only");
            sb.AppendLine("-- ========================================================================");
            sb.AppendLine();

            foreach (var suggested in suggestedFKs)
            {
                var constraintName = $"FK_{suggested.ChildTable}_{suggested.ParentTable}_Suggested";

                sb.AppendLine($"-- SUGGESTED ({suggested.ConfidenceDisplay} Confidence): {suggested.RelationshipDisplay}");
                sb.AppendLine($"-- {suggested.Cardinality}");
                sb.AppendLine($"-- ALTER TABLE [{suggested.ChildSchema}].[{suggested.ChildTable}]");
                sb.AppendLine($"--     ADD CONSTRAINT [{constraintName}]");
                sb.AppendLine($"--     FOREIGN KEY ([{suggested.ChildColumn}])");
                sb.AppendLine($"--     REFERENCES [{suggested.ParentSchema}].[{suggested.ParentTable}] ([{suggested.ParentColumn}]);");
                sb.AppendLine();
            }
        }

        sb.AppendLine("-- ========================================================================");
        sb.AppendLine("-- END OF SCRIPT");
        sb.AppendLine("-- ========================================================================");

        return sb.ToString();
    }

    /// <summary>
    /// Parses selected table IDs and returns matching table models.
    /// </summary>
    private static List<SchemaTableModel> ParseSelectedTables(
        IReadOnlyList<SchemaTableModel> allTables,
        List<string> selectedTableIds)
    {
        var selectedTables = new List<SchemaTableModel>();

        foreach (var id in selectedTableIds)
        {
            // Format: "SchemaName|TableName"
            var parts = id.Split('|');
            if (parts.Length != 2)
                continue;

            var schemaName = parts[0];
            var tableName = parts[1];

            var table = allTables.FirstOrDefault(t =>
                t.SchemaName.Equals(schemaName, StringComparison.OrdinalIgnoreCase) &&
                t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

            if (table != null)
            {
                selectedTables.Add(table);
            }
        }

        return selectedTables;
    }

    /// <summary>
    /// Checks if a table is in the selected list.
    /// </summary>
    private static bool IsTableSelected(string schemaName, string tableName, List<string> selectedTableIds)
    {
        var selectionValue = $"{schemaName}|{tableName}";
        return selectedTableIds.Contains(selectionValue, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates Mermaid markdown diagram for viewing in VS Code, GitHub, etc.
    /// </summary>
    public async Task<string> GenerateMermaidDiagram(
        ConnectionSessionModel connection,
        string databaseName,
        List<string> selectedTableIds,
        bool includeExplicitFKs,
        bool includeSuggestedRelationships,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(selectedTableIds);

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name is required.", nameof(databaseName));
        }

        if (selectedTableIds.Count == 0)
        {
            throw new ArgumentException("At least one table must be selected.", nameof(selectedTableIds));
        }

        // Load table and column metadata
        var allTables = await _schemaDiscoveryService.DiscoverTablesAsync(connection, databaseName, cancellationToken);
        var selectedTables = ParseSelectedTables(allTables, selectedTableIds);

        // Load relationships
        var relationships = await _schemaDiscoveryService.DiscoverRelationshipsAsync(
            connection,
            databaseName,
            cancellationToken);

        // Filter relationships to only those between selected tables
        var relevantRelationships = relationships.Relationships
            .Where(r => IsTableSelected(r.ParentSchema, r.ParentTable, selectedTableIds) &&
                       IsTableSelected(r.ChildSchema, r.ChildTable, selectedTableIds))
            .ToList();

        var explicitFKs = relevantRelationships
            .Where(r => r.Type == RelationshipType.Explicit)
            .ToList();

        var suggestedFKs = relevantRelationships
            .Where(r => r.Type == RelationshipType.Suggested)
            .ToList();

        // Build Mermaid markdown
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# Entity-Relationship Diagram: {databaseName}");
        sb.AppendLine();
        sb.AppendLine($"**Server:** {connection.ServerName}  ");
        sb.AppendLine($"**Database:** {databaseName}  ");
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Tables:** {selectedTables.Count}  ");
        sb.AppendLine($"**Explicit Foreign Keys:** {explicitFKs.Count}  ");
        sb.AppendLine($"**Suggested Relationships:** {suggestedFKs.Count}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Mermaid diagram
        sb.AppendLine("## Entity-Relationship Diagram");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("erDiagram");

        // Generate relationship lines
        if (includeExplicitFKs)
        {
            foreach (var fk in explicitFKs)
            {
                var parentEntity = SanitizeMermaidName(fk.ParentTable);
                var childEntity = SanitizeMermaidName(fk.ChildTable);
                var cardinality = GetMermaidCardinality(fk.Cardinality);
                var label = $"\"{fk.ConstraintName}\"";

                sb.AppendLine($"    {parentEntity} {cardinality} {childEntity} : {label}");
            }
        }

        if (includeSuggestedRelationships)
        {
            foreach (var suggested in suggestedFKs)
            {
                var parentEntity = SanitizeMermaidName(suggested.ParentTable);
                var childEntity = SanitizeMermaidName(suggested.ChildTable);
                var cardinality = GetMermaidCardinality(suggested.Cardinality);
                var label = $"\"suggested ({suggested.ConfidenceDisplay})\"";

                sb.AppendLine($"    {parentEntity} {cardinality} {childEntity} : {label}");
            }
        }

        sb.AppendLine();

        // Generate entity definitions
        foreach (var table in selectedTables)
        {
            // Load columns for this table
            var browserViewModel = await _schemaDiscoveryService.DiscoverColumnBrowserAsync(
                connection,
                databaseName,
                allTables,
                table.SchemaName,
                table.Name,
                cancellationToken);

            var columns = browserViewModel.Columns;
            var entityName = SanitizeMermaidName(table.Name);

            sb.AppendLine($"    {entityName} {{");

            foreach (var column in columns)
            {
                var keyIndicator = "";
                if (column.IsPrimaryKey)
                    keyIndicator = " PK";
                else if (column.IsForeignKey)
                    keyIndicator = " FK";

                var dataType = SanitizeMermaidDataType(column.DataType);
                sb.AppendLine($"        {dataType} {column.Name}{keyIndicator}");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("```");
        sb.AppendLine();

        // Add relationship details tables
        if (includeExplicitFKs && explicitFKs.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Explicit Foreign Key Relationships");
            sb.AppendLine();
            sb.AppendLine("| Parent Table | Column | Child Table | Column | Cardinality | Delete Rule | Update Rule |");
            sb.AppendLine("|--------------|--------|-------------|--------|-------------|-------------|-------------|");

            foreach (var fk in explicitFKs)
            {
                var deleteRule = fk.DeleteAction ?? "NO ACTION";
                var updateRule = fk.UpdateAction ?? "NO ACTION";
                sb.AppendLine($"| {fk.ParentTableDisplay} | {fk.ParentColumn} | {fk.ChildTableDisplay} | {fk.ChildColumn} | {fk.Cardinality} | {deleteRule} | {updateRule} |");
            }

            sb.AppendLine();
        }

        if (includeSuggestedRelationships && suggestedFKs.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Suggested Relationships");
            sb.AppendLine();
            sb.AppendLine("| Parent Table | Column | Child Table | Column | Confidence | Cardinality |");
            sb.AppendLine("|--------------|--------|-------------|--------|------------|-------------|");

            foreach (var suggested in suggestedFKs)
            {
                sb.AppendLine($"| {suggested.ParentTableDisplay} | {suggested.ParentColumn} | {suggested.ChildTableDisplay} | {suggested.ChildColumn} | {suggested.ConfidenceDisplay} | {suggested.Cardinality} |");
            }

            sb.AppendLine();
        }

        // Add legend
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Legend");
        sb.AppendLine();
        sb.AppendLine("### Cardinality Notation");
        sb.AppendLine();
        sb.AppendLine("- `||--o{` One-to-Many (1:N) - One parent has zero or many children");
        sb.AppendLine("- `||--||` One-to-One (1:1) - One parent has exactly one child");
        sb.AppendLine("- `}o--o{` Many-to-Many (N:M) - Many-to-many via junction table");
        sb.AppendLine("- `}o--||` Many-to-One (N:1) - Many children reference one parent");
        sb.AppendLine();
        sb.AppendLine("### Column Markers");
        sb.AppendLine();
        sb.AppendLine("- **PK** = Primary Key");
        sb.AppendLine("- **FK** = Foreign Key");
        sb.AppendLine();
        sb.AppendLine("### Confidence Levels (Suggested Relationships)");
        sb.AppendLine();
        sb.AppendLine("- **High**: Exact table name match (e.g., `CustomerID` → `Customer.CustomerID`)");
        sb.AppendLine("- **Medium**: Partial pattern or ID suffix (e.g., `CustID` → `Customer.CustomerID`)");
        sb.AppendLine("- **Low**: Weak indicators or data type match only");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Viewing Instructions");
        sb.AppendLine();
        sb.AppendLine("This file uses **Mermaid** syntax for diagrams. To view:");
        sb.AppendLine();
        sb.AppendLine("- **VS Code**: Install \"Markdown Preview Mermaid Support\" extension");
        sb.AppendLine("- **GitHub/GitLab**: Renders automatically in markdown files");
        sb.AppendLine("- **Online**: Paste into [mermaid.live](https://mermaid.live)");
        sb.AppendLine("- **Export**: Use Mermaid CLI to generate PNG/SVG");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Sanitizes table/column names for Mermaid entity names (no special chars, spaces).
    /// </summary>
    private static string SanitizeMermaidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unknown";

        // Replace spaces and special characters with underscores
        var sanitized = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
                sanitized.Append(c);
            else if (c == '_')
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }

        return sanitized.ToString();
    }

    /// <summary>
    /// Sanitizes data type names for Mermaid (lowercase, simplified).
    /// </summary>
    private static string SanitizeMermaidDataType(string dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return "unknown";

        // Simplify common SQL Server types
        var simplified = dataType.ToLowerInvariant()
            .Replace("nvarchar", "string")
            .Replace("varchar", "string")
            .Replace("nchar", "string")
            .Replace("char", "string")
            .Replace("bigint", "long")
            .Replace("datetime2", "datetime")
            .Replace("datetime", "datetime")
            .Replace("date", "date")
            .Replace("decimal", "decimal")
            .Replace("numeric", "decimal")
            .Replace("money", "decimal")
            .Replace("smallmoney", "decimal")
            .Replace("bit", "bool")
            .Replace("tinyint", "byte")
            .Replace("smallint", "short");

        return simplified;
    }

    /// <summary>
    /// Converts cardinality description to Mermaid notation.
    /// Note: The cardinality from the database is from the child's perspective (Many-to-One),
    /// but Mermaid notation is written as Parent {cardinality} Child, so we need to flip it.
    /// </summary>
    private static string GetMermaidCardinality(string cardinality)
    {
        if (string.IsNullOrWhiteSpace(cardinality))
            return "||--o{"; // Default to one-to-many (parent to child)

        var normalized = cardinality.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        return normalized switch
        {
            "onetoone" or "1:1" or "11" => "||--||",      // One-to-one
            "onetomany" or "1:n" or "1:many" => "||--o{", // One-to-many (parent perspective)
            "manytoone" or "n:1" or "many:1" => "||--o{", // Many-to-one (child perspective) → flip to one-to-many for Mermaid
            "manytomany" or "n:m" or "n:n" or "many:many" => "}o--o{", // Many-to-many
            _ => "||--o{" // Default to one-to-many
        };
    }
}
