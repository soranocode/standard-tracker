# Standard Tracker — project notes

## 2026-10-02 — Main workspace UI and portable build

The top menu is now a dark button toolbar: new deck and import, selected-deck
actions, and statistics on the right. Existing commands and migration entry points
remain. Favorite stars use centered vector icons. The Hearthstone launch button
was removed and all three main columns align at the top.

Recent results show the player's class icons for the latest ten matches in one
row, oldest on the left. A new match displaces the leftmost result. History rows
show player and opponent icons plus opponent nickname. Their borders show opposite
win/loss outcomes; blank nicknames have a fallback and long names have a tooltip.

Validation: Debug and Release x64 builds passed. Release WPF smoke passed window,
migration-command, deck saving, Wild import/persistence and mid-game deck binding
checks with synthetic profiles. Native renders verified ten-result ordering,
opposite borders and result changes, toolbar availability, and star centering;
the layout was inspected at 1440x760 and 1200x640. Interactive menu checks are
limited by mouse-capture failure in this automation session (the unchanged WPF
menu fails identically). Existing whole-repository web mockup audit findings
remain outside these native changes. Screenshots in `design/` use synthetic data.

Diagnostic scripts detach production startup to keep their profiles isolated and
exit promptly. `package-standard.ps1` creates a portable standalone ZIP with
manifest-selected runtime files, card assets, theme images, licenses and source
commit metadata, excluding diagnostic executables, profiles and logs. The inherited
upstream installer/release scripts remain unused.

## 2026-10-01 — Independent library and tracker data migration

`StandardTracker.Library` is our separate deck/history project, with its own
versioned JSON contract and repository. It has no dependency on HDT, WPF,
HearthDb or HearthMirror. The desktop scaffold connects through
`LibraryRuntimeAdapter` / `StandardLibrarySession`; all deck and statistics save
entry points now write one `%APPDATA%/StandardTracker/library.json`. The library
owns decks, historical versions, cards/sideboards, match progress, ranks, tags,
recent deck selection and import identities. Recent replays are derived from the
native match history. Runtime library code no longer writes any of the HDT XML
profile files or `LastGames.xml`. `LIBRARY.md` describes the contract and boundary.

The Import menu now supports explicit migration from Hearthstone Deck Tracker
profile XML and Firestone match-history JSON / Electron SQLite. HDT decks,
historical versions and Standard/Wild match progress merge into the local library;
same-content decks retain their saved local names. Firestone reconstructs decks
from history deck codes. Missing/bad codes preserve the match in default statistics.
The menu also includes a read-only IndexedDB export helper for Firestone Overwolf.
`TRACKER-IMPORT.md` documents extraction, supported shapes, backups and limitations.

Previews operate on detached data. Matching versions are remapped, same-source
matches deduplicate by persistent IDs, and skipped/invalid/unassigned records are
reported. Saving flushes a staged native library, preserves the previous file in
`ImportBackups` and atomically replaces the whole library, then refreshes existing
deck objects and statistics. Import is blocked during a match and duplicate
submission is prevented. HDT/Firestone remain read-only source adapters; their
files are no longer needed after migration. Original HDT profiles are never
automatically attached. Earlier Standard Tracker XML in our isolated profile is
upgraded once with originals retained. Daily/manual backups now contain the
native library, and explicitly restoring an earlier development ZIP converts its
old data. Corrupt native files can recover from `.bak` with damaged bytes retained;
unsupported schema versions are rejected before interpreting deck/match shapes.

Validation: Debug and Release x64 builds, 73 library/migration/isolation/deck-sync/import
tests (12 native-library and 18 migration tests), Debug and Release WPF smoke, and four export-helper
success/failure scenarios passed. Native SQLite source bytes remain unchanged;
failed atomic commits retain the original library and import backup, verified with
a locked destination. Migration survives removing the original HDT files and
restarting from the native store. Scoped UI static audit
and diff whitespace check passed. Whole-repository UI audit still reports 11
existing findings in old browser design mockups, outside the native import feature.
Tests use synthetic profiles; a real HDT/Firestone profile and the export helper in
actual Overwolf DevTools still need end-to-end validation. Firestone's obsolete
JSON cache can be incomplete; its current Overwolf LevelDB files are not read
directly. Cross-source duplicate matches with unrelated IDs are not inferred.

## 2026-09-30 — Cross-device checkpoint and web test space

The current work is saved on GitHub in the repository's `standard-tracker`
branch. The web overlay prototype is a permanent test space in
`design/overlay-review/index.html`, with Windows launcher `preview.ps1` and
portable Python server `design/serve-preview.py`. Both serve the sandbox on
loopback, default port 8765. It also opens directly as a self-contained HTML file.
No WPF build, installed game, Node runtime or third-party Python package is
required to edit and review this interface on another device.

Start here when continuing UI work: use the root README's clone/start commands,
edit the sandbox and refresh the browser. The three-panel design and interactions
remain a prototype; connect them to the real tracker only as an implementation
task. `TRACKER-DATA.md` preserves the audit of play events, creator metadata and
the opponent's known-entity counts. Desktop source changes and their tests/smoke
scripts are included in this checkpoint. The desktop build still needs its
explicitly provisioned libraries and localizations as documented below.
Preview validation passed from another working directory: HTTP GET/HEAD, UTF-8
content, Windows launcher port forwarding, invalid/occupied port errors and
serving only the prototype directory. The temporary test server was stopped.

## 2026-09-30 — Played-card panel and origin audit

The review concept now includes a separate scrollable played-card
panel. Its count means successful play occurrences, with identical card IDs
merged and per-source counts kept in details. Playing from the hand must not
decrement the deck a second time.

The tracker has `CardsPlayedThisMatch` and creator tags, but that played list
omits some special-card paths and stores mutable entities. A complete live
panel needs immutable play snapshots and cancellation handling for `CANT_PLAY`.
`GetCreatorId` resolves DISPLAYED_CREATOR, CREATOR, then internal CreatorId;
the current hand overlay already uses this for created-by/drawn-by labels.
There is no general reason string, and unknown sources must stay unknown.
The review's `TRACKER-DATA.md` records the audit and integration requirements.
Browser verification passed at 1280x960 and 390x700: original and created copies
merge into one row with separate source counts, play does not reduce the deck
again, both lists scroll independently, empty/reset/late-match states restore
correctly, and both panels remain within their own height bounds. This panel
is part of the review prototype and is not integrated into WPF.

The updated review places the opponent's existing known-card panel on the left,
and the player's played history below their deck on the right. Played art is
dimmed separately while deck art stays bright. The opponent panel must preserve
`OpponentCardList` semantics: counts represent known entities, created copies
and cards still known in hand/deck can form separate rows, and replaying a
returned entity does not create another known copy. Its hand/deck totals remain
separate from revealed-card counts. The detailed audit is in `TRACKER-DATA.md`.
Three-panel browser checks passed at 1280x960 and 390x700, including simultaneous
50-card test series, independent scrolling and the 1920x1080 placement schematic.
The 390x600 empty-history case exposed a panel minimum-height defect; the shared
column now protects both headers, footers, a readable row and the complete empty
message, using document scrolling in very short review windows. The minimum is
recomputed when history or density changes. The final screenshot is
`design/overlay-review/overlay-opponent-played-preview.jpg`.

## 2026-09-30 — Dynamic card-list layout

The in-game WPF panel keeps its configured height as cards are added. Rows scale
between 34 and 24px, then the card list scrolls while the title and counters stay
within the panel. Fixed insertion of a new row when an original and created card
share an ID: insertion now uses the exact incoming card object rather than
ID-only equality, preserving both rows and their requested order.

Validation: Release x64 build and actual WPF runtime checks passed for new cards,
four copies in one row, separate original/created rows, 30/50-row overflow,
scrolling to the last row, returning to 16 rows, and both original/created orders
on reset. The repeatable `build-scripts/smoke-overlay-layout.ps1` checks 13
scenarios in an isolated profile. This verifies layout and card-list updates with synthetic game state;
live Hearthstone shuffle effects still need end-to-end verification.

## 2026-09-30 — In-game overlay design proposal

`design/overlay-review/index.html` is a self-contained interactive concept for
user review, not integrated into WPF. It compares the current overlay with a
280px panel using restrained art, explicit 2/1/0 remaining counts, separate
legendary/hand markers, labelled counters, and the library palette. Includes
simulated match stages, Standard/Wild, density, opacity, card details, known-card
shuffling, 30/50-card test series, and a schematic screen-placement view. The
prototype now bounds panel height and scrolls its list independently of the
header and counters. Strict static audit, JavaScript syntax, and
wide/narrow in-app browser checks passed. Await user design feedback before
changing the actual overlay.

## 2026-09-30 — Deck capture in queue and in-progress matches

Queue monitoring now starts from the live client/scene as well as loading-screen
logs. The selected constructed deck is imported independently of the disabled
collection-wide auto-import setting. Missing deck data and incomplete snapshots
are retried once per second in queue and gameplay. The captured full deck survives
the transition into gameplay; late startup also tries the client's retained deck
selection and then a unique match by class and original revealed cards. Ambiguous
matches stay unresolved until more data arrives.

An existing saved deck/version is reused, archived matches are restored, and
automatic capture reveals the deck through library search/favorite filters.
Mid-game selection and reconnect recovery preserve entities and current stats;
the full deck, Hearthstone ID, local deck ID, and saved version are bound to the
match for normal end-of-game recording. A running game can be recovered from the
live gameplay scene and game entity when its start event was missed.

Validation: Debug/Release x64 builds and 43 isolation, deck sync/selection, and
import tests passed. Release WPF smoke passed full mid-game import with bulk
auto-import disabled, repeated detection without duplicates, filter clearing,
reconnect state preservation, and statistics binding. A real queue and late
startup in a live Standard/Wild match still need end-to-end verification.

## 2026-09-30 — Standard and Wild support

Standard and Wild Ranked, Casual, and Friendly matches now use the overlay,
automatic selection of the game deck, and game recording. Removed Standard
legality checks from deck-code/clipboard import, saving, library visibility,
and match deck selection: the bundled card-set classification can disagree
with the current game. The library shows constructed decks together even when
an older profile saved a Standard-only filter. Unknown formats, Twist, and
other game modes remain unsupported. Earlier Standard-only notes below describe
the previous behavior.

Validation: Debug and Release x64 builds; 32 isolation, deck-selection, and
import tests passed. Release WPF smoke passed Wild deck-code import, editor
saving, XML persistence, and visibility with a legacy Standard filter. Test
profile persistence requires running outside the filesystem sandbox because it
blocks File.Replace. A live Wild match remains to be checked in Hearthstone.

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
