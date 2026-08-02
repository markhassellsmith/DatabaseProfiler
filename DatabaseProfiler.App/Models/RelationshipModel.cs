namespace DatabaseProfiler.App.Models;

/// <summary>
/// Type of relationship between tables.
/// </summary>
public enum RelationshipType
{
    /// <summary>
    /// Explicit foreign key constraint defined in the database.
    /// </summary>
    Explicit,

    /// <summary>
    /// Suggested/implicit relationship inferred from naming patterns and data types.
    /// </summary>
    Suggested
}

/// <summary>
/// Confidence level for suggested relationships.
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>
    /// Not applicable (for explicit relationships).
    /// </summary>
    NotApplicable,

    /// <summary>
    /// High confidence (strong naming pattern + type match).
    /// </summary>
    High,

    /// <summary>
    /// Medium confidence (partial pattern match or type-only match).
    /// </summary>
    Medium,

    /// <summary>
    /// Low confidence (weak indicators).
    /// </summary>
    Low
}

/// <summary>
/// Represents a foreign key relationship between two tables.
/// </summary>
public sealed class RelationshipModel
{
    /// <summary>
    /// Type of relationship (Explicit FK or Suggested).
    /// </summary>
    public RelationshipType Type { get; init; }

    /// <summary>
    /// Confidence level for suggested relationships.
    /// </summary>
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.NotApplicable;
    /// <summary>
    /// Name of the foreign key constraint.
    /// </summary>
    public string? ConstraintName { get; init; }

    /// <summary>
    /// Schema name of the parent (referenced) table.
    /// </summary>
    public required string ParentSchema { get; init; }

    /// <summary>
    /// Name of the parent (referenced) table.
    /// </summary>
    public required string ParentTable { get; init; }

    /// <summary>
    /// Column name in the parent (referenced) table.
    /// </summary>
    public required string ParentColumn { get; init; }

    /// <summary>
    /// Schema name of the child (referencing) table.
    /// </summary>
    public required string ChildSchema { get; init; }

    /// <summary>
    /// Name of the child (referencing) table.
    /// </summary>
    public required string ChildTable { get; init; }

    /// <summary>
    /// Column name in the child (referencing) table.
    /// </summary>
    public required string ChildColumn { get; init; }

    /// <summary>
    /// Referential action on DELETE (e.g., CASCADE, NO_ACTION, SET_NULL, SET_DEFAULT).
    /// </summary>
    public string? DeleteAction { get; init; }

    /// <summary>
    /// Referential action on UPDATE (e.g., CASCADE, NO_ACTION, SET_NULL, SET_DEFAULT).
    /// </summary>
    public string? UpdateAction { get; init; }

    /// <summary>
    /// Whether the foreign key constraint is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether the foreign key constraint is trusted (data has been verified).
    /// </summary>
    public bool IsTrusted { get; init; }

    /// <summary>
    /// Whether the child column is covered by an index (performance indicator).
    /// </summary>
    public bool IsIndexed { get; init; }

    /// <summary>
    /// Cardinality of the relationship (e.g., "One-to-One", "Many-to-One").
    /// </summary>
    public required string Cardinality { get; init; }

    /// <summary>
    /// Fully qualified parent table name for display.
    /// </summary>
    public string ParentTableDisplay => $"{ParentSchema}.{ParentTable}";

    /// <summary>
    /// Fully qualified child table name for display.
    /// </summary>
    public string ChildTableDisplay => $"{ChildSchema}.{ChildTable}";

    /// <summary>
    /// Short relationship description for display.
    /// </summary>
    public string RelationshipDisplay => $"{ChildTableDisplay}.{ChildColumn} → {ParentTableDisplay}.{ParentColumn}";

    /// <summary>
    /// Display text for relationship type.
    /// </summary>
    public string TypeDisplay => Type == RelationshipType.Explicit ? "FK" : "Suggested";

    /// <summary>
    /// Display text for confidence level.
    /// </summary>
    public string ConfidenceDisplay => Confidence switch
    {
        ConfidenceLevel.High => "High",
        ConfidenceLevel.Medium => "Medium",
        ConfidenceLevel.Low => "Low",
        _ => ""
    };
}
