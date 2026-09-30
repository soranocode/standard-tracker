# In-game overlay concept

## Run the saved preview

On another device, clone or download this repository and run the saved prototype
locally. It needs no packages or build step. From the repository root on Windows:

```powershell
.\preview.ps1
# Choose another port if needed:
.\preview.ps1 -Port 8877
```

The launcher finds `python` or falls back to `py -3`. On macOS/Linux or directly
with Python 3:

```sh
python3 design/serve-preview.py --port 8765
```

Open `http://127.0.0.1:8765/` (or your chosen port). Ctrl+C stops the server.
It binds only to the current device; run it separately on the other device.
Script paths are resolved independently of the working directory, so the Windows
launcher also works when invoked by its full path. You can instead open
`design/overlay-review/index.html` directly in a browser without Python: the
prototype is self-contained. Both routes run the same simulated review space,
with no live game connection or public deployment.

Status: proposal for user review; not integrated into WPF. `index.html` is a
self-contained offline prototype with bundled Russian card names and cached art
from the example Dragon Warrior deck. All match states, entities and interactions
are simulations. The old panel is an actual WPF render, available in the
expandable "Current version" section.

The main composition has the opponent's revealed cards on the left. On the right,
your deck sits above your played-card history in one height-bounded column.
At 700px and below, the opponent panel moves above that column; your history
always remains below your deck. Each of the three lists scrolls independently.

The prototype preserves the approved library palette from
`Controls/StandardDesign.xaml` and `Controls/SelectedDeckPanel.xaml`: background
`#121721`, surface `#1C2533`, text `#EDF2FA`, muted `#A3B0C4`, accent `#79B4FF`,
divider `#34445A`. CSS custom properties own the prototype tokens. Proposed
additions are hand marker `#94CFBE`, legendary marker `#D9B77C`, 280px panel width
and 33px/39px row density. These are draft choices, not new global tokens.

The visual signature is a thin hand-state rail beside a fixed mana column.
Names use Segoe UI; Bahnschrift handles titles and tabular counters. Deck art
uses 90% opacity with a short fade on the left. Zero-count cards still in hand
retain that bright art; exhausted cards outside the hand use 48%. Your played
history uses 44% art opacity while names, mana and play counts remain bright.
Separate, non-shrinking 15px legendary stars remain visible beside long names.
Known opponent cards in hand or deck are dimmed to distinguish their state.

Layout and long lists:

- The right column uses natural content height when it fits. Its review limit is
  the viewport minus 220px, clamped between 420px and 950px. When both lists are
  long, they share roughly 60/40 of the available height. A short list gives its
  spare space to the other. Fixed headers, footers and at least one readable row
  are protected; an empty history protects its full explanatory text. Their
  combined required minimum is recalculated after every stage or density change.
  If a very short viewport cannot fit that minimum, it takes priority over the
  review height ceiling and the outer document scrolls; individual panels keep
  their headers, footers and content minimum inside their bounds.
- The opponent panel has a separate review limit, between 200px and 778px using
  the same viewport allowance. The review document itself can scroll.
- Only card lists scroll. Titles and counters stay visible; rows and fonts never
  shrink to fit larger lists. All three panels share density and opacity controls.
- The keyed updater preserves existing row nodes, list position and keyboard
  focus while their keys remain. Hover, focus and click expose the same details;
  Escape closes them. Rows sort by mana, then Russian card name.
- The 1920x1080 placement schematic uses the same renders and scroll positions:
  opponent left at approximately x10/y135 with a 778px ceiling, own column right
  at approximately x1630/y22 with a 950px ceiling. Your history remains below your
  deck, inside that column. These are proposed 280px panels near the actual
  tracker anchors, not a Hearthstone screenshot or live overlay integration.

Your deck model:

- Remaining counts mean copies still in the deck; zero does not mean played.
  Hand presence is tracked separately. Singles always show a numeric count,
  and counts can exceed two after shuffling extra copies into the simulated deck.
- Deck rows use card ID plus original/created origin. Shuffling the known example
  `CATA_584` creates a marked `+` row; another copy increments it without creating
  another row. The original row and original 30-card composition remain separate.
- The 30/50-row controls replace their synthetic series with `DEMO_ONLY_*` IDs,
  explicitly artificial names and neutral placeholders. They change the live
  simulated deck count, but never the starting 30-card composition. Replacing a
  series also removes that series' simulated hand copies.
- Drawing moves one copy from deck to hand. The hand limit is 10; its message
  suggests playing a card when full. Reset restores the selected stage and
  removes every added real or artificial copy from all panels.

Your played-card history:

- Only explicit successful simulated play events enter this pool. Draws,
  discards, destruction and graveyard contents do not populate it.
- Rows aggregate only by card ID, combining original and created copies. Counts
  mean successful play occurrences, including another play of the same card.
  Per-source counts remain in the tooltip: "Original deck" and "Source not
  determined". No generator is invented for the created example.
- Plain play consumes a card in hand: hand minus one, history plus one, deck
  unchanged. "Draw and play another copy" performs both transitions: deck minus
  one and no net hand change. Creating and playing `CAP_107` directly in hand
  changes history without adding a false deck row or changing deck remaining.
- Start and turn-two stages have no past plays. The turn-seven stage has deck21,
  hand5 and four successful plays across three card IDs, accounting for all 30
  original copies. Stage changes and reset restore that coherent history.
- The separate 30/50-play series uses `DEMO_PLAYED_ONLY_*` IDs and an explicitly
  artificial source. Replacing it preserves real-example plays and does not
  change the hand or deck. Total plays and distinct card IDs stay in the footer.

Opponent revealed-card model:

- "Opponent cards / Revealed cards" shows known entities, not a complete unknown
  deck and not a count of play events. Unknown card IDs produce no rows.
- Groups use card ID, created/foreign status and `Jousted` state. Created or stolen
  copies keep a separate `+` row. Origin is explicit fixture metadata, never
  inferred from the number of copies. An unresolved created source stays unknown.
- `Jousted` means a known card currently in hand/deck, or a guessed set-aside
  card. These groups are dimmed; tooltip details give the concrete simulated zone.
  Other known groups may include cards on the board or in the graveyard.
- Hand and deck counters independently count simulated entities in those zones.
  They are not calculated as 30 minus revealed cards or play occurrences.
  "Opponent played a card" reveals a held copy and moves that entity from hand
  to board: hand minus one, deck unchanged. Returning and replaying the same
  entity preserves its identity and does not increase known-copy count.
- Start has hand4/deck26 and no revealed rows. Turn two has hand5/deck23 and five
  known entities in five groups. Turn seven has hand4/deck18 and 11 known entities
  in nine groups. These fixtures deliberately include known cards still in hand
  or deck and an explicitly created held copy.
- Opponent stress controls replace their own 30/50-entity artificial series,
  with `DEMO_OPPONENT_ONLY_*` definitions and no game art. They leave its hand/deck
  counters unchanged and do not affect your deck or history.
- This is a focused demonstration of grouping and counters. Discard grouping,
  extra information and original-ID transformations require the real tracker
  model; the prototype does not claim to reproduce every opponent-state rule.

Actual tracker findings and future integration rules are documented in
[TRACKER-DATA.md](TRACKER-DATA.md), including immutable play snapshots, cancelled
plays and source-resolution metadata. Live remaining counts must come from game
entities; starting composition must come from the resolved deck/version. A saved
library status requires confirmed capture/save. Hidden, unknown and predicted
cards must not be labelled complete. This HTML reads no game logs or memory.

Verification for this update: JavaScript syntax, model and DOM-ID checks passed.
Checks covered opponent stage counters, unknown IDs, created/foreign and Jousted
separation, guessed set-aside groups, revelation without deck decrement, entity
identity on replay, independent 30/50 replacement and reset. Your play history,
source aggregation, draw/play transitions and direct-created play remain valid.
The strict premium static audit reports zero findings; `git diff --check` passes.
Twenty-four allocator cases also passed across both densities and 420/480/740px
ceilings, checking that empty/populated stage changes recompute and preserve the
required minimum of each panel.

Browser verification passed at 1280x960 and 390x700 with simultaneous stress:
66 deck rows, 51 played rows and 55 opponent rows. Each list independently reaches
its final row, all footers stay inside their panels and no horizontal overflow
occurs. The placement schematic stays inside 1920x1080: opponent x10/y135/h778,
right column x1630/y22/h740 with history below the deck. Shared opacity at 70%,
both densities, tooltip/Escape, Standard/Wild and reset were checked. Reset to
start restores own deck26/hand4/history0 and opponent deck26/hand4/no revealed
rows. At 390x700, empty history is fully visible in both densities without
scrolling its explanation. IDs remain unique. Current proofs are
`overlay-opponent-played-review.jpg` and `overlay-opponent-played-preview.jpg`.
The shorter 390x600 start/roomy check also passes: the column uses its 471px
content minimum, the deck keeps a full 39px row and the empty explanation fits.
Switching to turn seven recomputes the minimum to 416px and returns the column
to its 420px ceiling; the larger empty-state minimum does not persist.

Previous review proofs `overlay-layout-safe.jpg` and `overlay-played-review.jpg`
cover the earlier deck/history versions, not the current three-panel arrangement.

Review scope: name legibility, bright deck versus dim history, panel width,
independent scrolling, remaining/played/known-copy meanings, hand state and source
details. No product DESIGN.md change is made until a direction is approved.
