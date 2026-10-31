Added opt-in scheduled Auto Pinch. Configure it in `/dagobert` → Scheduled Auto Pinch, set an interval, and park within reach of a summoning bell with menus closed.

- Opens a nearby bell and processes enabled retainers without a button press each cycle.
- Includes countdown, Run Now, Pause/Resume, and Cancel and pause.
- Starts the next interval after the current cycle finishes; missed intervals do not accumulate.
- Waits while busy or out of bell range; pauses on errors, timeouts, or interruptions during a run.
- Fetches fresh prices each cycle while keeping the duplicate-item cache within a run.
- Restores acquired AutoRetainer/modern YesAlready coordination on completion or cancellation.

Built against Dalamud API 15. Automated scheduling checks pass; bell interaction and repeated in-game cycles still require testing.
