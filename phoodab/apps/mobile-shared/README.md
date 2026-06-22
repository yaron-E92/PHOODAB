# PHOODAB Mobile Shared Presentation

Reusable MAUI-facing presentation composition for PHOODAB hosts.

This project owns code that can be consumed by both the standalone PHOODAB app
and future SecondBrain presentation hosts:

- application-facing service registration
- reusable ViewModel/composition dependencies
- shared presentation contracts that do not assume a specific shell or view

The current standalone code-built views remain in `../mobile` because their
layout, shell, navigation, resources, and platform startup are host-specific.
SecondBrain can reference this project without inheriting those standalone
views.
