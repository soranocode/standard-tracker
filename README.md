# Standard Tracker

Local Standard-mode fork of Hearthstone Deck Tracker. Data lives in
`%APPDATA%/StandardTracker`; original HDT profiles are not imported. Cloud services and
upstream automatic data downloads are disabled except full rendered card images
requested by hover previews. At the start of a Standard 1v1 match,
the tracker selects the deck chosen in Hearthstone. It matches an existing local deck
by card list or imports that one deck from the installed game if needed. This uses
local game data and requires automatic deck detection to be enabled.
Use the `+` next to “Библиотека колод” to paste a Hearthstone deck code into the
Standard library. The `###` title becomes the deck name; a bare code uses the
Russian class name. A saved deck with the same class, cards, and sideboards is
reused. Click the code in the selected-deck panel or the copy icon under a class
icon in the library to copy the code with its saved title.

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

This is a tested development checkpoint, not a finished release. Read [PROJECT.md](PROJECT.md)
for validation and unfinished work. Dormant upstream binary dependencies remain.

Build: `./bootstrap.ps1` (Windows, Visual Studio MSBuild, .NET SDK, net472 targeting pack).
Requires provisioned local `lib/` and translations. NuGet may require internet.
Test: `./test-standard.ps1`.
WPF smoke: `powershell.exe -NoProfile -STA -File build-scripts/smoke-standard.ps1`.
Do not use inherited release/sync workflows or packaging scripts before adapting them.

Based on HearthSim Hearthstone Deck Tracker v1.57.7. Original copyright and third-party
license notices remain in source and `licenses/`.
