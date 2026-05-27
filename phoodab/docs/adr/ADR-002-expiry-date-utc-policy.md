# ADR-002: Consumable entry expiry status uses UTC date-only policy

## Status
Accepted

## Context
Expiry is consumable-entry-specific and must be deterministic across API and tests.

## Decision
- Expiry status is computed in backend application logic per consumable entry.
- The current date source is injectable and read as UTC date-only (`DateOnly` from `DateTime.UtcNow`).
- `ExpiresInDays = expiryDate.DayNumber - todayUtc.DayNumber`.
- Status thresholds:
  - `Unknown`: no expiry date
  - `Expired`: `< 0`
  - `Urgent`: `0..expiryWarningDays`
  - `Soon`: `(expiryWarningDays + 1)..(expiryWarningDays + 5)`
  - `Safe`: `> expiryWarningDays + 5`

## Consequences
- Avoids local-time ambiguity near midnight.
- Keeps frontend presentation-only; no expiry classification in React.
- Ensures multiple consumable entries of same item can have independent statuses.
