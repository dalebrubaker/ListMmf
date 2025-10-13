# Release Notes

## 1.1.1

- fix: prevent potential hang in interpolation search loops for `ListMmfTimeSeriesDateTimeSeconds`
  - Ensures progress when interpolation lands on `pos == high` by decrementing `high` (and symmetric guard in upper bound).
  - Affected methods: `InterpolationLowerBound`, `InterpolationUpperBound`.
  - Symptom: Rare infinite loop (observed as a computational hang) during `LowerBound`/`UpperBound` when searching near the end of a large, uniformly increasing dataset.
  - Impact: No API changes; correctness preserved. Performance characteristics unchanged aside from eliminating the hang.

Internal: Added regression tests to cover last-element interpolation edge cases.
