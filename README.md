# Standard Tracker

Local Standard-mode fork of Hearthstone Deck Tracker. Data lives in
`%APPDATA%/StandardTracker`; original HDT profiles are not imported. Cloud services and
upstream automatic data downloads are disabled. Import a Standard deck manually.

Deck-list card art is extracted on demand from the installed Hearthstone files and
cached in `%APPDATA%/StandardTracker/Images/CardTiles`. This optional local feature
requires Python on `PATH` with `UnityPy` and `Pillow` installed (`python -m pip install
UnityPy Pillow`). If either dependency or the game files are unavailable, the deck
list shows its usual placeholder. No card art is fetched from a remote server.

This is a tested development checkpoint, not a finished release. Read [PROJECT.md](PROJECT.md)
for validation and unfinished work. Dormant upstream binary dependencies remain.

Build: `./bootstrap.ps1` (Windows, Visual Studio MSBuild, .NET SDK, net472 targeting pack).
Requires provisioned local `lib/` and translations. NuGet may require internet.
Test: `./test-standard.ps1`.
WPF smoke: `powershell.exe -NoProfile -STA -File build-scripts/smoke-standard.ps1`.
Do not use inherited release/sync workflows or packaging scripts before adapting them.

Based on HearthSim Hearthstone Deck Tracker v1.57.7. Original copyright and third-party
license notices remain in source and `licenses/`.
