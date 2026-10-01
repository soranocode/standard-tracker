# Standard Tracker

## Web test space

The in-game overlay has a permanent browser sandbox in
[design/overlay-review/index.html](design/overlay-review/index.html). It includes
the current three-panel design, real example card art, simulated match stages,
shuffling, play history, opponent revelation and long-list checks. These are demo
states; the sandbox is not connected to a running Hearthstone match.

To continue on another device:

```powershell
git clone --branch standard-tracker https://github.com/soranocode/standard-tracker.git
cd standard-tracker
./preview.ps1
```

Open `http://127.0.0.1:8765/`. The launcher needs Python 3; no Python packages,
Node.js, game installation or WPF build are needed for the web sandbox. On macOS
or Linux, run `python3 design/serve-preview.py`. To use a different port, run
`./preview.ps1 -Port 8766` or `python3 design/serve-preview.py --port 8766`.
The HTML is also self-contained and can be opened directly in a browser.

Edit `design/overlay-review/index.html` and refresh the page to review changes.
[Sandbox notes](design/overlay-review/README.md) describe the interactions;
[tracker-data audit](design/overlay-review/TRACKER-DATA.md) explains how the
prototype maps to the actual tracker. Preserve those count and source meanings
when implementing the design in WPF. Build requirements for the desktop tracker
are separate and listed below.

## Desktop tracker

Standalone Standard/Wild tracker built on Hearthstone Deck Tracker's desktop/game
scaffold. Its deck library and match history use the independent
`StandardTracker.Library` project and `%APPDATA%/StandardTracker/library.json`.
Original HDT profiles are not imported automatically. See [native library architecture](LIBRARY.md).
Cloud services and
upstream automatic data downloads are disabled except full rendered card images
requested by hover previews. At the start of a Standard or Wild 1v1 match,
the tracker selects the deck chosen in Hearthstone. It matches an existing local deck
by card list or imports that one deck from the installed game if needed. This uses
local game data and requires automatic deck detection to be enabled.
The chosen deck is also imported during matchmaking, even with collection-wide
auto-import disabled. Missing or partial game data is retried once per second.
Starting the tracker during a match reuses the captured/retained game selection
or a unique full-deck match to original revealed cards, and binds it to the
current game's statistics without resetting the match. Ambiguous matches wait
for more data instead of selecting a different deck.
Use the `+` next to “Библиотека колод” to paste a Hearthstone deck code into the
deck library. Deck import, saving, and library visibility do not depend on the
bundled Standard card-set classification. The `###` title becomes the deck name; a bare code uses the
Russian class name. A saved deck with the same class, cards, and sideboards is
reused. Click the code in the selected-deck panel or the copy icon under a class
icon in the library to copy the code with its saved title.

Use the Import menu to migrate decks and match progress from Hearthstone Deck
Tracker or Firestone into our native library. HDT is a read-only adapter for its
three profile XML files; Firestone accepts
JSON match history and Electron SQLite databases. A preview shows new/duplicate/
skipped matches before saving, and every import creates a recoverable profile
backup. The menu includes an export helper for Firestone Overwolf's full history.
See [migration instructions and limitations](TRACKER-IMPORT.md).

Deck-list card art is extracted on demand from the installed Hearthstone files and
cached in `%APPDATA%/StandardTracker/Images/CardTiles`. This optional local feature
requires Python on `PATH` with `UnityPy` and `Pillow` installed (`python -m pip install
UnityPy Pillow`). If either dependency or the game files are unavailable, the deck
list shows its usual placeholder. Full rendered cards used in hover previews are
fetched on demand from HearthstoneJSON and cached in
`%APPDATA%/StandardTracker/Images/CardImages`. Hover over a card in the
selected-deck list or game overlay to see the full rendered card. If it is not
cached and the server is unavailable, the tracker shows its usual loading image.
Russian card names and descriptions are bundled with the app and work offline;
English text is used only when a card is absent from the bundled Russian data.
The in-game deck panel adapts to shuffled cards: rows shrink to a readable minimum
and then scroll within the configured panel height, preserving the title and counters.

This is a tested development checkpoint, not a finished release. Read [PROJECT.md](PROJECT.md)
for validation and unfinished work. Dormant upstream binary dependencies remain.

Build: `./bootstrap.ps1` (Windows, Visual Studio MSBuild, .NET SDK, net472 targeting pack).
Requires provisioned local `lib/` and translations. NuGet may require internet.
Test: `./test-standard.ps1`.
WPF smoke: `powershell.exe -NoProfile -STA -File build-scripts/smoke-standard.ps1`.
Dynamic overlay smoke (after a Release build):
`powershell.exe -NoProfile -STA -File build-scripts/smoke-overlay-layout.ps1 -Configuration Release`.
Do not use inherited release/sync workflows or packaging scripts before adapting them.

Based on HearthSim Hearthstone Deck Tracker v1.57.7. Original copyright and third-party
license notices remain in source and `licenses/`.
