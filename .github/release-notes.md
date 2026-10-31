Fixed scheduled Auto Pinch failing to recognize nearby summoning bells.

- Match localized bell names without case sensitivity and recognize the standard bell object ID.
- Include the appropriate interaction range for inn rooms as well as housing and world bells.
- Select the bell before interacting, retry opening it, and wait for the actual retainer list.
- Preserve existing AutoRetainer suppression instead of treating suppression as evidence of a busy operation.
- Run Now immediately checks availability and is not postponed by a setting change in the same frame.
- Show nearby bell distances and the current queued task in scheduling settings.

Build and automated scheduling/bell-selection checks pass. In-game validation is still required.
