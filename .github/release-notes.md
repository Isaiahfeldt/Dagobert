Auto Pinch caches the first valid price for duplicate items during a run and skips repeated marketboard lookups and delays. HQ and NQ remain separate, and every listing still gets its own price limits and maximum-undercut safety check.

Updated ECommons for compatibility with current stable Dalamud. This package compiles against Dalamud API 15; in-game behavior still needs testing.

To test: extract latest.zip to a permanent folder, disable the normally installed Dagobert, add the extracted Dagobert.dll to Dalamud Dev Plugin Locations, and load it through the developer plugin tools. Check duplicate listings, HQ/NQ separation, and a fresh lookup when starting another Auto Pinch run.
