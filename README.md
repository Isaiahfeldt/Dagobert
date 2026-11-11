# Dagobert

FFXIV Dalamud plugin for automatically adjusting retainer Market Board prices.

## Installation

Add this repository to your Dalamud custom plugin repositories:

```text
https://raw.githubusercontent.com/Isaiahfeldt/ffxiv/main/pluginmaster.json
```

Install Dagobert from that custom repository in `/xlplugins`. If you already have the original version installed, uninstall it first and select the entry from this repository. Back up your plugin configuration before switching.

This fork includes cached prices for duplicate items. Its package builds against Dalamud API 15; in-game behavior still needs testing.

Open the configuration window with `/dagobert`.

## Configuration

The `/dagobert` configuration window has two tabs:

- `General` contains the normal pricing, timing, retainer, hotkey, chat, and TTS settings.
- `Min/Max Prices` contains per-item minimum and maximum prices.

### Pricing

| Option | Default | Description |
| --- | --- | --- |
| Use HQ price | On | Uses HQ listings for HQ items. If no HQ listing is available, Dagobert may not find a price. |
| Undercut Mode | FixedAmount | Choose between subtracting a fixed gil amount or a percentage from the lowest price. |
| Undercut amount | 1 gil | How much to undercut by. In Percentage mode this is 1-99%. |
| Max Undercut percentage | 100% | Safety limit. Price changes that would cut more than this percentage are skipped. |
| Max Undercut amount (gil) | 0 gil | Additional safety limit per unit. Reductions exceeding either this amount or the percentage limit are skipped. `0` disables the gil limit. |
| Allow price increases | On | When off, automatic price adjustments cannot exceed the current asking price, including fallback prices and per-item minimums. |
| Max Price Increase (%) | 0% | Increases above this percentage leave the current asking price unchanged, including increases from per-item minimums. `0` means unlimited. Requires `Allow price increases`. |
| Undercut Self | Off | When off, Dagobert will not undercut listings from your own retainers. |
| Use Universalis data center prices | Off | Uses the cheapest listing on your current data center from Universalis instead of only the in-game Market Board result. |
| Default amount | 0 gil | Fallback price when no price can be found. `0` disables the fallback. |
| Show inventory context menu entry | On | Adds the right-click inventory entry used to add or configure per-item min/max prices. |

### Per-Item Min/Max Prices

Use the `Min/Max Prices` tab to set item-specific price limits. `0` means no limit.

| Option | Description |
| --- | --- |
| Min | Lowest price Dagobert may set for that item. |
| Max | Highest price Dagobert may set for that item. |
| Remove | Deletes the item-specific limits. |

Right-click an item in your inventory and choose `Add Dagobert price limits` to add it to the table. If the item is already configured, the same menu entry opens the configuration window as `Configure Dagobert price limits`.

Disable `Show inventory context menu entry` in the `General` tab to hide this right-click entry.

Per-item limits are applied after Dagobert finds a candidate price, including prices from Universalis, and before the price is written to the retainer listing.

### Timing

| Option | Default | Description |
| --- | --- | --- |
| Market Board Price Check Delay | 3000 ms | Delay before opening the Market Board price list. Lower values are faster but less reliable. |
| Market Board Keep Open Time | 1000 ms | How long the Market Board window stays open while fetching prices. Lower values are faster but less reliable. |

Recommended timing values are around `3000-4000 ms` for the price check delay and `1000-2000 ms` for the keep-open time.

### Retainers and Chat

| Option | Default | Description |
| --- | --- | --- |
| Retainer Selection | All enabled | Choose which retainers are included when using Auto Pinch from the retainer list. Open the retainer list in-game to populate or refresh this list. |
| Show errors in chat | On | Prints pinching errors and skipped items to chat. |
| Show Price Adjustments | On | Prints detailed price changes to chat. |
| Show Retainer Names | On | Prints each retainer name while pinching all retainers. |
| Clear retainer Cache | - | Clears the stored list of your own retainers used for self-undercut detection. |

### Hotkeys

| Option | Default | Description |
| --- | --- | --- |
| Enable Post Pinch Hotkey | On | Lets you hold the configured key while posting a new item to automatically get the lowest price. |
| Auto Post Pinch Key | Shift | Key used for post pinch. |
| Enable Pinch Hotkey | Off | Press the configured key to start Auto Pinch from the retainer list or retainer sell list. Press it again to cancel. Holding the key does not repeat the action. |
| Auto Pinch Key | Q | Key used for Auto Pinch. |

Configured hotkeys still perform their normal in-game actions.

### Text-to-Speech

The Text-to-Speech options are shown when Windows TTS is available.

| Option | Default | Description |
| --- | --- | --- |
| All | Off | Speaks a message after Auto Pinch has processed all retainers. |
| Each | Off | Speaks a message after Auto Pinch has processed the current retainer. |
| TTS Volume | 20% | Volume for spoken messages. |

## Usage

Dagobert adds an `Auto Pinch` button to the retainer list and retainer sell list. While Auto Pinch is running, avoid interacting with the game until it finishes or you press `Cancel`.

During an Auto Pinch run, Dagobert remembers the first valid price for each item and reuses it for duplicate listings, skipping the marketboard lookup and its delays. HQ and NQ items are cached separately. Each listing still uses its own min/max limits and maximum-undercut safety check. The cache is cleared when you start a new Auto Pinch run.

## Scheduled Auto Pinch

Open `/dagobert` → **Scheduled Auto Pinch**. Scheduling is off by default. Enable it, choose a frequency with the slider in minutes (60 = one hour; supported range 1 minute to 7 days), and park within interaction range of a summoning bell with menus closed. Use **Run now** for an immediate first cycle; otherwise the first cycle waits the configured interval.

A cycle opens the nearby bell, processes your enabled retainers, and closes the retainer list. The next interval starts after that cycle finishes. It never walks, teleports, or attempts to find a bell outside interaction range. If you are busy, moving, or no bell is reachable, it waits and checks again rather than accumulating missed cycles. It cannot start while the retainer list or another occupied interaction is already open; finish that interaction first.

Each cycle clears the item-price cache, so it fetches current prices while duplicates within that cycle use the existing cache. Pricing limits and maximum-undercut safeguards still apply to every listing. A run completing with skipped listings still counts as a completed cycle.

**Cancel and pause** stops the current task queue and pauses the schedule. Errors, timeouts, combat, movement, logout, or territory/character changes during a scheduled cycle also pause it; the settings show the reason. Cancellation leaves any already-open menus for you to close. Use **Resume** to wait a fresh interval, or **Run now** to try again once available. Pause state is for the current plugin session; saved scheduling enablement and interval survive reloads.

The plugin coordinates with AutoRetainer through its suppression IPC and with modern YesAlready through its stop-request mechanism. It restores the AutoRetainer suppression state it observed when acquiring the lock; no unsupported legacy YesAlready reflection is used. Do not start competing retainer automation during a cycle. Third-party plugin coordination and bell interactions require in-game testing.

Build validation uses the normal Release build. Scheduling checks run with:

```sh
dotnet run --project Dagobert.Checks
```

The **Random extra delay** slider adds a uniformly selected extra wait from 0 up to the configured maximum (0–1440 minutes). For example, a 60-minute frequency plus up to 10 random minutes produces a fresh wait between 60 and 70 minutes after each completed cycle. Zero disables randomness. The countdown shows the selected wait. Resume and changing frequency/randomness choose a new wait; Run now bypasses it. Ctrl+click either slider to type an exact value.

Version 1.14.2.2 fixes bell recognition when the displayed name uses different capitalization from the game data, adds standard bell ID matching and inn-room range handling, and waits/retries until the retainer list is actually open. When a due run cannot begin, the settings status reports the nearest recognized bell distance or other blocker; active runs show the queued task. Existing AutoRetainer suppression is preserved without treating it as a busy flag.

### Automatic retainer selection

In `/dagobert` → **Scheduled Auto Pinch**, use **Retainers for automatic runs** to choose which retainers are repriced by timer cycles and Run now. Open the bell retainer list once to populate names if needed. Existing configurations initially follow the General-tab manual selection. Editing a scheduled checkbox creates an independent selection without changing manual settings; Use manual selection restores the link. Enable all and Disable all apply only to automation. Changing selection during a run affects the next cycle. If all automatic retainers are disabled, scheduling pauses until you select retainers and use Resume or Run now.

Version 1.14.3.1 binds price lookups to an exact marketable item ID, HQ/NQ identity, and request generation. Only matching network items and active responses are accepted. The open listing is checked again before caching or applying a price; mismatches or ambiguous item identification stop the run. Main, scheduled, and post-pinch runs start with fresh caches. A pending price is preserved while waiting for its adjustment window to become ready. This does not yet replace row-index listing traversal or prove that all causes of incorrect prices have been identified.

Upstream maintenance adds localized mannequin detection, edge-triggered hotkey cancellation, configurable price increases and gil/percentage safeguards. Run NUnit coverage with `dotnet test Dagobert.Tests/Dagobert.Tests.csproj -c Release -p:Use_DalamudPackager=false` in addition to `dotnet run --project Dagobert.Checks`.
