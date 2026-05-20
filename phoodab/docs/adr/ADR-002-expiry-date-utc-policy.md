# ADR-002: Lot expiry status uses UTC date-only policy

## Status
Accepted

## Context
Expiry is lot-specific and must be deterministic across API and tests.

## Decision
- Expiry status is computed in backend application logic per lot.
- The current date source is injectable and read as UTC date-only (`DateOnly` from `DateTime.UtcNow`).
- `ExpiresInDays = expiryDate.DayNumber - todayUtc.DayNumber`.
- Status thresholds:
  - `Unknown`: no expiry date
  - `Expired`: `< 0`
  - `Urgent`: `0..2`
  - `Soon`: `3..7`
  - `Safe`: `> 7`

## Consequences
- Avoids local-time ambiguity near midnight.
- Keeps frontend presentation-only; no expiry classification in React.
- Ensures multiple lots of same item can have independent statuses.
