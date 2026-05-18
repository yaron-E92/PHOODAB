# Events Integration (MVP)

## Decision

PHOODAB will reuse `https://github.com/yaron-E92/events` through a **thin adapter**.

For MVP, PHOODAB keeps its internal contract (`IEventHistoryStore`) as the application boundary and maps it to the external events repository via adapter implementation in Infrastructure.

## Why this path

- Reuses your existing events repository now.
- Keeps PHOODAB insulated from external model/API churn by containing coupling in one adapter.
- Preserves ability to evolve either side without forcing PHOODAB domain/application model changes.

## Hard boundaries

- Event/history records are **append-only audit history**.
- Event/history is **not canonical state** in MVP.
- **Current-state tables remain the primary query truth**.
- No replay-based state reconstruction.

## Internal contract requirements

The PHOODAB contract supports:

- Event ID
- Aggregate/entity reference
- Occurred-at timestamp
- Actor metadata
- Source metadata
- Correlation ID
- Import batch ID
- Append operation
- Timeline query/filter

## External adapter mapping (MVP)

The adapter maps the internal event-history contract to the external repository model/API fields for:

- identity (`EventId`)
- aggregate reference
- occurred timestamp
- actor/source metadata
- correlation/import-batch metadata
- append-only writes
- timeline reads with filtering/order

## Fallback if external repo is unavailable

- Continue with internal `IEventHistoryStore` implementations (in-memory/local persistence).
- Keep application/domain behavior unchanged.
- Re-enable external integration by restoring the adapter only; no domain/application contract changes required.

## Suggested upstream alignment in `events` repo (if needed)

If any of these are missing upstream, add/confirm them there for clean adapter mapping:

- first-class `CorrelationId`
- first-class `ImportBatchId`
- query by aggregate reference + time range + ascending timeline order

