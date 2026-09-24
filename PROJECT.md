# Standard Tracker — checkpoint 2026-09-23

Paused at user request while waiting for token limits to reset. Branch standard-tracker.
Remote: https://github.com/soranocode/standard-tracker.

## UI checkpoint — 2026-09-23

- Implemented and user-approved: modern dark deck library, always-visible live search
  across name/class/archetype/tags, favorites, class/archetype grouping, editable
  archetypes persisted in deck XML and preserved when cloning, refreshed active-deck panel.
- Adjusted main-window layout and default dimensions. Library preview with synthetic
  decks: `library-preview.png`.
- Verified Debug build and `build-scripts/smoke-standard.ps1 -LibraryPreview`:
  WPF main window/options load, archetype search/grouping, favorites, cloning and XML round trip.
- User also approved the per-deck game-history concept in `design/match-history-preview.html`.
  It uses demo data and contains expandable matches, day groups, outcome/period filters
  and a compact summary matching the library palette. This is a standalone preview,
  NOT integrated into the WPF application yet. Preserve that distinction.
- Next requested design direction: implement the approved per-deck history when work resumes.
  Current turn only saves/pushes progress; do not start implementation while paused.
- Local SDK used for builds: `C:\Users\user\Documents\Codex\2026-09-22\new-chat\work\toolchain\sdk`.
  Build command: `./bootstrap.ps1 -UseLocalDependencies -DotNetRoot <sdk-path>`.

## Earlier isolation checkpoint

Implemented: fixed isolated %APPDATA%/StandardTracker profile; no portable/original config,
no deck/stat/replay/plugin migration; separate activation ID and startup registry key.
Visible title Standard Tracker. OAuth credentials/client and HSReplay API fetching removed;
analytics, uploads, Sentry initialization, plugins, streaming, online import disabled.
Shared HTTP is offline; card definitions are bundled HearthDb data. Bootstrap no longer
fetches upstream dependencies/translations. Local lib and localization payload still required.
Standard Ranked/Casual/Friendly only; unknown/Wild/Twist do not get recorded. UI hides
cloud and unsupported mode sections, deck picker only exposes Standard.

Verified: Debug build; test-standard.ps1 passed 3/3 isolation tests; Windows PowerShell
build-scripts/smoke-standard.ps1 loaded WPF window/options with empty test profile.
Original HDT data was not deleted: it is no longer used. No personal data files were found
under bin. Internal assembly name remains HearthstoneDeckTracker.exe for WPF resources.

UNFINISHED (do not call this a finished release):
- Rerun checks after last SaveDeck Standard guard and capturable overlay format gate.
- Build/package Release and test a real Standard match, secrets/cards/stats and mode changes.
- Audit auxiliary player/opponent/timer windows and remaining scene handler side effects.
- Remove dormant HSReplay/BobsBuddy/Sentry/Squirrel binaries and callers together; references
  remain for retained WPF models even though cloud access is disabled.
- Deck-list tiles now come from local Hearthstone Unity assets when Python, UnityPy,
  and Pillow are available. Full rendered cards and language presentation still need
  offline handling; downloads remain disabled.
- Pin/provision ignored lib/localizations for fresh checkout.
- Legacy packaging/release scripts and GitHub workflows still target HearthSim. DO NOT use
  them unchanged. Auto-review rejected renaming workflows to disable them, requiring explicit
  user approval because it affects CI. No workflow changes were made.

Local logs: tests.log, smoke.log. Temporary editing scripts are not product files.
No reset credits used. Preserve original HDT profile and licenses/provenance.
