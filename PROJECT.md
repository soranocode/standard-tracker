# Standard Tracker — project notes

## 2026-09-28 — M.O.T.H.E.R. cost preview

During the Battlecry target selection for M.O.T.H.E.R. (`BE_036`), the game overlay
shows green projected mana costs over affected hand cards. Moving across targets
recalculates the selected card at -5 and each neighbor at one less reduction per
step, down to -1. Displayed costs never fall below zero. The preview disappears
after a target click, right click,
leaving the game, or a timeout. The target-selection phase is inferred from mouse
input and hand positions because the current game integration does not expose it
directly. A live match is still needed to validate this timing and badge placement.

## 2026-09-28 — Card language and hover images

Selected-deck cards use the tracker's original hover tooltip and finished card
renders from HearthstoneJSON. The ruRU CardDefs data from
`https://api.hearthstonejson.com/v1/latest/CardDefs.ruRU.xml`
(build 253216, retrieved 2026-09-28) is bundled as a compressed offline file and
loaded with HearthDb after the base data. Newer cards missing from that snapshot
still use the bundled English text instead of showing an empty description.


## 2026-09-28 — Deck-code names, copying, and art in the selected deck

Import now extracts the base64 deck code and `###` title separately, ignoring
trailing source links. A bare code gets the Russian class name. Reimporting a
matching deck repairs an erroneous source-link name. The selected-deck code row
and a small library action copy `### <saved deck name>` plus the code. The selected
deck list now displays cached/local-game card tiles under a readable gradient and
starts extraction for the whole deck; missing tiles retain placeholders. The `+`
uses a centered vector icon.

## 2026-09-28 — Library deck-code import

The `+` beside the deck-library heading opens a compact deck-code dialog. Valid
Standard codes are decoded locally and saved in the library. Importing identical
cards and sideboards again selects the existing deck (including an older version)
instead of creating a duplicate; archived matches are restored. Library search and
favorite filters are cleared to reveal it. At match start the selected game deck is
matched by class and cards, with sideboards compared when game memory includes them.
The game provides deck ID and contents, not the pasted deck-code text itself.

## 2026-09-28 — Standard match deck selection

At the start of a Standard Ranked, Casual, or Friendly match, the selected deck
captured from Hearthstone is matched to a local deck by its cards, including saved
versions. If no local deck matches, the selected Standard deck is imported from
the local game data. This avoids depending on a Hearthstone deck ID for decks
previously imported by deck code. Release x64 build, WPF smoke, three matching
tests, and three isolation tests passed. A live match is still needed to verify
the game-memory data and overlay timing end to end.

## Earlier checkpoint — 2026-09-23

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
- Deck-list tiles come from local Hearthstone Unity assets when Python, UnityPy,
  and Pillow are available. Full rendered cards in hover previews download
  from HearthstoneJSON on demand and are cached; other upstream downloads stay disabled.
- Pin/provision ignored lib/localizations for fresh checkout.
- Legacy packaging/release scripts and GitHub workflows still target HearthSim. DO NOT use
  them unchanged. Auto-review rejected renaming workflows to disable them, requiring explicit
  user approval because it affects CI. No workflow changes were made.

Local logs: tests.log, smoke.log. Temporary editing scripts are not product files.
No reset credits used. Preserve original HDT profile and licenses/provenance.
