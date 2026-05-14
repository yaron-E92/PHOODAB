# ADR-001: Initial Architecture Boundaries and Modeling Rules (MVP)

- **Status:** Accepted
- **Date:** 2026-05-14

## Context

PHOODAB MVP must remain .NET-first, MAUI-ready, and prevent business logic leakage into React or other presentation clients. The team also needs explicit, non-negotiable rules for computed fields and event/history usage so modeling and persistence do not drift during implementation.

## Decision

### 1) Layer boundaries are strict and non-negotiable

- `backend/src/Phoodab.Domain` **must** contain core domain types, invariants, and domain rules.
- `backend/src/Phoodab.Application` **must** contain use-case orchestration and application/business workflows.
- `backend/src/Phoodab.Infrastructure` **must** contain persistence and external integration concerns only.
- `backend/src/Phoodab.Api` **must** expose HTTP/OpenAPI contracts and endpoint composition only.
- `apps/web` **must** be presentation-only and **must not** host domain or business logic.
- A future MAUI app **must** be implemented as a sibling presentation layer and **must not** become a business-logic host.

### 2) Computed-field authority

- `RequiredAmount`, `ExpiresInDays`, and `ExpiryStatus` are derived/computed values.
- Derived/computed values **must not** be treated as source-of-truth columns.
- Persisted current state **must** store input/source data, not derived outputs as canonical truth.
- Computation of derived values **must** be owned by backend domain/application layers.
- Presentation clients (web, future MAUI) **must** consume computed results and **must not** redefine computation rules.

### 3) Event/history policy for MVP

- Current-state tables **must** remain the query truth.
- Events **must** be append-only audit/history records.
- Event records **must not** replace current-state tables as the canonical query model in MVP.
- Full event sourcing is **forbidden** for MVP.

### 4) Anti-scope-creep guardrail

Adopting full event sourcing during MVP is **forbidden** unless GitHub Issue #3 documents a hard technical blocker that requires it.

## Consequences

- Domain and use-case logic stay centralized in backend layers, reducing UI logic drift.
- Web and future MAUI clients can evolve independently as thin presentation layers.
- Computed-field persistence drift is prevented by making source inputs canonical and derived values backend-owned.
- Audit/history is preserved without introducing full event-sourcing complexity during MVP.
- Any exception to MVP event-sourcing prohibition requires explicit, documented escalation via Issue #3.

## Alternatives considered

1. **Allow business logic in `apps/web` for speed** — rejected; increases duplication, inconsistency, and long-term maintenance risk.
2. **Persist derived values as canonical source-of-truth** — rejected; risks stale/inconsistent data and conflicts with domain-owned computation.
3. **Adopt full event sourcing from day one** — rejected for MVP; adds substantial complexity and delivery risk not justified unless Issue #3 proves a hard blocker.
