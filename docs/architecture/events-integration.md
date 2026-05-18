# Events Integration (MVP)

## Decision

PHOODAB reuses `Yaref92.Events` now through a thin adapter around `IEventHistoryStore`.

## MVP wiring

- Register `EventAggregator` in DI.
- Register PHOODAB event type with `RegisterEventType<BatchHistoryEvent>()`.
- Register async handler(s) implementing `IAsyncEventHandler<TEvent>`.
- Subscribe handlers with `Subscribe(...)`.
- Handler appends events into `IEventHistoryStore` so batch timelines can be queried later.

## Hard boundaries

- Event/history records are append-only audit history.
- Event/history is not canonical state in MVP.
- Current-state tables remain primary query truth.
- No replay-based state reconstruction.

## Fallback

If external repo/package is unavailable, keep the internal `IEventHistoryStore` contract and swap only the infrastructure eventing adapter/wiring.
