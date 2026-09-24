# One-time handoff for the next Codex session

This file is a temporary continuation note for the user's move to another computer.
Read it together with `README.md` and `PROJECT.md`. After you have captured the
context and begun the work, remove this file with `git rm AGENTS.md` and include
that removal in your next commit. It is deliberately a one-use instruction.

## Current state (2026-09-24)

- Branch: `standard-tracker`; remote: `https://github.com/soranocode/standard-tracker.git`.
- The user's current priority is offline card images from the locally installed
  Hearthstone game. The latest commit implements deck-list **tiles only**:
  `extract_card_tiles.py` maps card IDs through `asset_manifest.unity3d`, opens
  only the required Unity bundles, crops the artwork and writes JPEGs under
  `%APPDATA%/StandardTracker/Images/CardTiles`. `LocalCardTileExtractor.cs`
  batches missing requests and runs the helper from the app. Existing image
  loading and placeholders continue to work. No image files are committed.
- This currently needs a system Python on `PATH` with `UnityPy` and `Pillow`.
  The development machine had UnityPy 1.25.3 and Pillow 12.3.0. Those packages
  and the game installation are **not** bundled with the app; a fresh clone may
  need the other computer's toolchain, `lib/`, translations, and NuGet packages.
- The local game used for verification was at `C:\Program Files (x86)\Hearthstone`.
  Verified extraction for `CORE_EX1_011`, `EX1_011` and `TLC_100`. Some IDs may
  have no usable local art and retain placeholders.
- Successful checks: final Debug x64 build,
  `build-scripts/smoke-standard.ps1`, and
  `build-scripts/smoke-local-card-art.ps1` with multiple real card IDs.

## Continue the work

1. On the new machine, verify a clean checkout, the Hearthstone installation
   path, and dependencies. Build and run the two smoke checks above. The
   bootstrap needs Visual Studio MSBuild, .NET Framework 4.7.2 targeting pack,
   a compatible .NET SDK, and locally provisioned dependencies. NuGet restore
   may need network access.
2. Make local art usable in a packaged standalone build: bundle a compatible
   extraction runtime or implement a managed extractor so users do not have to
   install Python and packages manually. Preserve offline behavior.
3. Add explicit cache refresh after game updates and clear diagnostics when the
   game files or extraction runtime are unavailable. Verify multiple cards
   requested together, cards from current Standard sets, and missing art.
4. Decide how to show full rendered card images and localization offline. This
   has **not** been implemented; extracting a raw portrait alone does not
   produce a framed, localized card. Do not describe this feature as finished.
5. Resume the previously approved per-deck match history when card art is in a
   suitable state. `design/match-history-preview.html` is a demo preview, not
   integrated WPF UI. `PROJECT.md` records the approved design and remaining
   isolation/release work.

Keep `%APPDATA%/StandardTracker` separate from original HDT data. Cloud calls
and automatic upstream downloads are intentionally disabled. Do not run the
inherited release or GitHub workflow scripts unchanged; they still target
HearthSim. Do not commit local game assets, profiles, cached images, or secrets.
