namespace DatabaseProfiler.App.Models;

/// <summary>
/// View model for the Relationships Browser page.
/// </summary>
public sealed class RelationshipBrowserViewModel
{
    /// <summary>
    /// List of discovered foreign key relationships.
    /// </summary>
    public required List<RelationshipModel> Relationships { get; init; }

    /// <summary>
    /// Total number of foreign key relationships discovered.
    /// </summary>
    public int RelationshipCount => Relationships.Count;

    /// <summary>
    /// Number of explicit foreign key relationships.
    /// </summary>
    public int ExplicitCount => Relationships.Count(r => r.Type == RelationshipType.Explicit);

    /// <summary>
    /// Number of suggested/implicit relationships.
    /// </summary>
    public int SuggestedCount => Relationships.Count(r => r.Type == RelationshipType.Suggested);

    /// <summary>
    /// Number of high-confidence suggested relationships.
    /// </summary>
    public int HighConfidenceCount => Relationships.Count(r => r.Confidence == ConfidenceLevel.High);

    /// <summary>
    /// Number of unique parent (referenced) tables.
    /// </summary>
    public int ParentTableCount => Relationships.Select(r => $"{r.ParentSchema}.{r.ParentTable}").Distinct().Count();

    /// <summary>
    /// Number of unique child (referencing) tables.
    /// </summary>
    public int ChildTableCount => Relationships.Select(r => $"{r.ChildSchema}.{r.ChildTable}").Distinct().Count();

    /// <summary>
    /// Number of relationships with CASCADE on delete.
    /// </summary>
    public int CascadeDeleteCount => Relationships.Count(r => r.DeleteAction.Contains("CASCADE", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Number of relationships that are not indexed (potential performance issue).
    /// </summary>
    public int NotIndexedCount => Relationships.Count(r => !r.IsIndexed);

    /// <summary>
    /// Number of disabled foreign key constraints.
    /// </summary>
    public int DisabledCount => Relationships.Count(r => !r.IsEnabled);

    /// <summary>
    /// Number of untrusted foreign key constraints (data integrity not verified).
    /// </summary>
    public int UntrustedCount => Relationships.Count(r => !r.IsTrusted);
}
