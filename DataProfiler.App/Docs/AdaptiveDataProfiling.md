# Adaptive Data Profiling

## Purpose
Adaptive profiling adjusts query cost based on table size so profiling remains useful and predictable across small and large tables.

The goal is to keep detailed profiling on small tables while switching to sampling and reduced column coverage as row counts increase.

## Core Strategy

Adaptive profiling should use two inputs:

- `rowCount`
- `columnCount`

These determine a profiling scope and a sampling strategy.

The current size strategy should remain the first decision point, but row count should also control how aggressively the engine samples rows for expensive metrics.

## Table Size Strategy Integration

The profiling engine should first classify the table, then apply the appropriate sampling policy.

Current size-oriented scopes:

- `Lookup`: small tables with low row count and low column count
- `Detail`: moderate tables that still fit interactive exploration
- `Large`: tables where full profiling becomes expensive
- `Massive`: tables where full scans are not practical

The row count should influence both the scope and the sampling percentage.

Recommended row-count bands:

- up to `50,000` rows: full profiling
- `50,001` to `500,000` rows: mostly full profiling, with targeted reduction for expensive metrics
- `500,001` to `2,000,000` rows: sampling recommended for frequency and distinct analysis
- `2,000,001` to `10,000,000` rows: sampling required for expensive metrics
- above `10,000,000` rows: aggressive sampling and strict column caps

## Table Size Scopes

### 1. Lookup
Small tables with low row count and low column count.

- Full-table profiling
- Full frequency analysis
- Full distinct counts
- Full aggregate statistics
- No sampling

### 2. Detail
Moderate tables that still fit interactive exploration.

- Full-table profiling for low-cost metrics
- Frequency analysis limited to high-value columns
- Distinct counts limited to a smaller column set
- Sampling may be enabled for frequency or distinct analysis when row count rises toward the upper bound
- Prefer accuracy over coverage

### 3. Large
Large tables where full profiling becomes expensive.

- Sampling enabled for expensive metrics
- Column coverage reduced for frequency and distinct analysis
- Aggregate metrics may still use full scan for selected columns
- Sampling should be consistent and repeatable
- Use a moderate sample rate that decreases as row count increases

### 4. Massive
Very large tables where full scans are not practical.

- Sampling is required for most expensive statistics
- Only a small number of columns should receive distinct/frequency analysis
- Numeric and temporal statistics may be limited to sampled rows or a narrow set of columns
- The system should prioritize responsiveness over exhaustive coverage
- Use the smallest safe sample rate that still preserves useful trends

## Sampling Design

### Sampling Goal
Use a percentage-based sampling strategy to normalize profiling cost across table sizes.

Smaller large tables can use a higher sampling percentage.
Very large tables should use a smaller percentage.

The sample percentage should be driven primarily by row count and secondarily by table scope.

### Sampling Rules
- Sampling percentage is derived from `rowCount`
- Sampling must be deterministic when possible
- Sampling should target stable results across repeated runs
- Sampling should preserve the same logical shape of the table profile
- Sampling should be applied only where it materially reduces cost

### Suggested Sampling Bands
These are design targets, not fixed values:

- `Lookup`: `100%`
- `Detail`: `75%` to `100%` depending on row count and metric cost
- `Large`: `10%` to `25%`
- `Massive`: `1%` to `10%`

The exact percentage should decrease as row count increases.

Suggested operational defaults:

- `<= 50,000` rows: `100%`
- `50,001` to `500,000` rows: `100%` for core aggregates, `50%` to `100%` for frequency candidates
- `500,001` to `2,000,000` rows: `25%` to `50%`
- `2,000,001` to `10,000,000` rows: `10%` to `25%`
- `> 10,000,000` rows: `1%` to `10%`

These ranges are intended to preserve a similar user experience while keeping runtime bounded.

## Column Strategy

Adaptive profiling should not rely on table size alone. It should also consider column type and profile value.

Priority should be higher for columns that are:

- short text
- bit
- temporal
- numeric
- uniqueidentifier for select metrics

Priority should be lower for:

- long text
- low-value freeform text
- columns unlikely to benefit from frequency or distinct analysis

## Metric Strategy

### Full Scan Candidates
These may remain full-scan longer:

- `COUNT(*)`
- `NULL` counts
- basic metadata
- min/max for selected supported types

### Sample-Friendly Metrics
These should switch to sampling earlier:

- frequency analysis
- distinct counts on wide tables
- standard deviation on very large tables
- expensive text aggregation

### Frequency Analysis
Frequency queries are especially expensive on larger tables.

Design intent:

- limit the number of columns included
- apply sampling for large and massive tables
- avoid running full frequency aggregation on every profile candidate
- prefer short, selective, and business-relevant columns

## Adaptive Policy

The profiling policy should combine:

1. `rowCount`
2. `columnCount`
3. column type
4. metric cost
5. table scope

Policy output should determine:

- whether a metric is enabled
- whether a sampled query is used
- which columns are eligible
- how many columns are included per metric
- the sample percentage, if any
- whether the sample is applied globally or only to expensive metrics

The policy should not treat all metrics equally. It should allow full-table aggregates where affordable, while using sampling for expensive frequency and distinct calculations first.

## Recommended Behavior by Scope

### Lookup
- Full profiling
- Full frequency statistics
- Full distinct counts
- No sampling

### Detail
- Mostly full profiling
- Frequency and distinct analysis limited by column priority
- Sampling optional for expensive operations
- Prefer selective sampling before reducing core aggregates

### Large
- Sampling enabled for expensive metrics
- Column caps enforced for frequency and distinct analysis
- Prefer fast results over exhaustive metrics
- Use row-count-based sampling to keep runtime predictable

### Massive
- Sampling required
- Minimal expensive metrics
- Strict column caps
- Focus on fast, representative profiling
- Sampling should be the default behavior, not an exception

## Implementation Notes

- Sampling percentage should be configurable
- Column caps should be configurable
- Scope thresholds should remain configurable
- Row-count bands should be documented so behavior is explainable to users
- Profiling should fail gracefully if a sampled query cannot be formed
- Results should indicate when sampling was used
- The profiling output should surface the final scope and sample percentage so the user knows what was changed by the policy

## Reporting Notes

When sampling is used, the output should record:

- scope
- sample percentage
- whether full scan or sampled scan was used
- any column reductions applied

This helps users understand the tradeoff between speed and completeness.

## Design Outcome

This approach should level performance across different table sizes by:

- keeping small-table results complete
- reducing cost for large tables
- limiting expensive frequency queries
- using percentage sampling to approximate profiles at scale
- using row count as the main driver for how much profiling work is performed

The result is a more predictable profiling experience without requiring every table to be fully scanned.