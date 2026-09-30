# Played-card history and origins

This is an implementation audit for the review prototype, not a claim that its
new played-card panel is connected to Hearthstone.

The played panel represents successful plays. Group by the card ID seen at the
time of each play and count play occurrences, including repeated plays of a
card returned to hand. A card drawn, discarded, destroyed, or summoned without
being played must not enter this history. Original and generated copies of the
same ID share a row; retain their different sources as counts in the details.
Playing an already drawn card reduces the hand count, not the deck count.

## Existing history

`Player.CardsPlayedThisMatch` is populated by `Player.Play`. It counts repeated
plays of the same entity, and normal hand-to-play detection checks for a PLAY
block. However, secrets, quests, sigils and objectives follow separate methods
that populate `SpellsPlayedCards` but not this general list. Entries are mutable
entity references, so later transformations and replayed cards can change what
an earlier entry appears to represent. The graveyard includes events unrelated
to playing and cannot substitute for a complete history.

`CANT_PLAY` removes provisional entries from the existing played lists. A future
panel must handle this cancellation rather than treating every provisional play
callback as final. The current `OnPlayerPlay` callback contains a card list but
does not expose entity identity or origin metadata by itself.

## Existing origin metadata

`Entity.Info.GetCreatorId()` uses `DISPLAYED_CREATOR`, then `CREATOR`, then the
internal `CreatorId`. It returns zero for a hidden target card. The returned
source ID still needs to resolve to a known, visible source entity. The actual
overlay already uses this metadata for its created-by/drawn-by hand markers.

Other relevant data includes `CREATOR_DBID`, `COPIED_FROM_ENTITY_ID`,
`CopyOfCardId`, `Created`, `Stolen`, `Returned`, `OriginalController`,
`OriginalZone`, and `DrawerId`. Some fields and copy relationships are populated
only by specific mechanics. Creator-tag handlers also handle exceptions such as
self-references, Far Sight, and original Whizbang cards.

There is no universal reason string. Power-log block type, source entity, card
ID, parent and trigger context can explain selected actions, but this context
is not automatically saved as every resulting card's origin. Missing, hidden,
or ambiguous origins must display "Источник неизвестен".

## Integration requirements

Capture an immutable record for each successful play: identity for cancellation,
the card ID/name/cost at that moment, turn, original/generated/stolen status,
and each resolved source's identity/name. Aggregate the display by card ID while
preserving occurrences and per-source counts. Count a repeated play as another
occurrence even when it uses the same entity. Include special play paths and
roll back cancelled plays. Do not infer a source from a currently visible card
merely because it might generate the result.

The prototype's source examples are simulation labels. They show original-deck
copies and generated copies with an unknown source; they do not invent a real
Hearthstone generator for the example shuffled card.

## Existing opponent panel

`Player.OpponentCardList` calls `GetOpponentCardList`. When the opponent's full
deck is unknown, the list uses revealed entities and predicted cards. It can
include played or discarded cards, known cards still in hand/deck, returned
cards, and created or stolen copies. It is not a play-event history.

`Player.cs:472` groups these entities by card ID, hidden/current-zone status,
created status, discarded highlighting and extra information. Transformed
entities use their original card ID. Each row's `Count` is the number of known
entities in its group. Replaying one returned entity therefore does not count
as a second known copy. Created/stolen copies may occupy a separate row from
original copies. Unknown card IDs do not become identified card rows.

The hidden/current-zone group becomes `Card.Jousted`: known cards in the
opponent's hand/deck, or selected predicted set-aside cards, are darkened by
`CardTile.IsDarkened` (`CardTile.xaml.cs:233`). Opponent hand and deck totals are
calculated separately from current entities (`OverlayWindow.Update.cs:218`),
not from the number of rows or successful plays.

If `KnownOpponentDeck` is available, a separate branch instead uses remaining
deck composition and the existing removal/hand-highlighting preferences. A
future redesign must preserve this branch rather than treating both cases as
the same history.

The default existing opponent panel is 218 px wide, at 0.5% from the left and
12.5% from the top, with a 72%-of-screen height limit and 100% scale
(`OverlayWindow.xaml:258`, `Config.cs:601`). The review uses its proposed 280 px
card styling while retaining a left opponent panel and a right player column.
The left heading is "Карты соперника"; only the player's lower panel counts
play occurrences. Demo zones, revealed entities and unknown sources are
explicit simulations, not a resolved live opponent deck.
