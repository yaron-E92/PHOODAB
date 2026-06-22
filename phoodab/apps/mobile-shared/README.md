# PHOODAB Mobile Shared Presentation

Reusable MAUI-facing presentation composition for PHOODAB hosts.

## Project boundary

This project owns code that can be consumed by both the standalone PHOODAB app
and future SecondBrain presentation hosts:

- application-facing service registration
- reusable ViewModel/composition dependencies
- shared presentation contracts that do not assume a specific shell or view

This project intentionally does not own runnable app startup, shell
navigation, visual resources, platform services, or MAUI pages. Those stay in
host projects so SecondBrain can compose its own presentation without inheriting
the standalone PHOODAB experience.

## Current shared inventory

| Area | Shared here | Host-specific |
| --- | --- | --- |
| Service composition | `AddPhoodabSharedPresentation` registers the PHOODAB application-facing services used by presentation hosts. | Host projects decide when to call the extension and which views/pages to register. |
| ViewModels | No standalone ViewModel classes exist yet; future host-neutral ViewModels should live here. | View state embedded in current standalone views remains in `../mobile` until it is extracted into reusable ViewModels. |
| Views | None. | `MainPage` remains host-specific because its layout, navigation flow, and controls are standalone app presentation choices. |
| Navigation and shell | None. | `App`, `NavigationPage`, page selection, and shell flow stay with each host. |
| Resources and platforms | None. | Fonts, images, colors, platform startup, Android, Windows, and app identifiers stay with each runnable host. |

## Host usage

Standalone PHOODAB references this project from `../mobile` and calls
`AddPhoodabSharedPresentation` during MAUI startup. A SecondBrain host should
reference this same project for shared service and ViewModel-facing composition,
then register its own pages, navigation, resources, platform integrations, and
any SecondBrain-specific views.
