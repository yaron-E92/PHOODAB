# Durable Item Follow-Up Handoff

PHOODAB durable items can imply follow-up work, but PHOODAB does not own
general task management or cross-domain orchestration. Durable follow-ups are
modeled as future candidates for SecondBrain orchestration.

## Candidate follow-up types

The backend-owned handoff contract names these follow-up candidates:

- `Repair`
- `WarrantyCheck`
- `Service`
- `Clean`
- `ReplacePart`
- `InspectCondition`

These candidates describe user intent that may later be sent to SecondBrain.
They are not PHOODAB task states, scheduled jobs, reminders, assignments,
calendar events, or task-board items.

## PHOODAB-owned state

PHOODAB keeps durable item state where it describes the current item, such as:

- active inventory
- needs repair
- loaned out
- stored
- retired
- lost

This state remains separate from any future task workflow. For example,
`NeedsRepair` describes the condition of the durable item in PHOODAB; it does
not mean PHOODAB has created or scheduled a repair task.

## Future SecondBrain payload

When a future host or API exposes durable follow-up creation, PHOODAB should
send SecondBrain a payload shaped like
`DurableFollowUpSecondBrainHandoff` from the Application project:

- durable entry identity
- item definition identity
- item display name
- current durable item status
- follow-up candidate type
- optional due date hint
- optional urgency hint
- optional notes

SecondBrain is responsible for deciding whether that intent becomes a task,
reminder, calendar event, integration workflow, or no downstream action.

## ShuffleTask boundary

PHOODAB should not call ShuffleTask directly for durable item follow-ups. If
ShuffleTask becomes involved, it should be downstream of SecondBrain after
SecondBrain accepts the handoff and chooses that integration path.

## UI affordance direction

MAUI or other presentation hosts may eventually show disabled or future-facing
affordances such as `Create follow-up`, `Needs repair`, or
`Warranty check needed`. Until real SecondBrain integration exists, those
affordances should not schedule work, create tasks, send notifications, assign
owners, or persist workflow state in PHOODAB.
