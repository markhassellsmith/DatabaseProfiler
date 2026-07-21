namespace DataProfiler.App.Services.Profiling;

public sealed class TableProfilingPolicyOptions
{
    public long LookupTableMaxRowCount { get; set; } = 50_000;

    public int LookupTableMaxColumnCount { get; set; } = 25;

    public long DetailTableMaxRowCount { get; set; } = 500_000;

    public int DetailTableMaxColumnCount { get; set; } = 50;

    public long LargeTableMaxRowCount { get; set; } = 10_000_000;

    public int LargeTableMaxColumnCount { get; set; } = 100;

    public int LargeTableMaxCountDistinctColumns { get; set; } = 8;

    public int LargeTableMaxFrequencyColumns { get; set; } = 4;

    public int MassiveTableMaxCountDistinctColumns { get; set; } = 2;

    public int MassiveTableMaxFrequencyColumns { get; set; } = 1;
}
