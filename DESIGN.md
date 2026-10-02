---
version: alpha
name: Standard Tracker
description: Russian WPF deck library and match progress for Hearthstone players
colors:
  foreground: "#EDF2FA"
  muted: "#A3B0C4"
  surface: "#1C2533"
  border: "#34445A"
  hover: "#29384E"
  focus: "#79B4FF"
typography:
  sans:
    fontFamily: Segoe UI
rounded:
  button: 6px
spacing:
  button-horizontal: 10px
  button-vertical: 6px
components:
  button: {}
  menu: {}
  dialog: {}
---

# Standard Tracker design context

The Windows WPF application helps Russian-speaking Hearthstone players manage
their constructed decks and preserve match progress. The established dark library
is the visual reference. Existing UI identity takes precedence over new visual
directions for a single feature.

Runtime ownership stays with `Hearthstone Deck Tracker/Controls/StandardDesign.xaml`
and the application's MahApps theme. This document records that existing system;
it does not introduce a second token source.

The scoped library uses Segoe UI, foreground #EDF2FA, muted text #A3B0C4,
surfaces #1C2533, borders #34445A, hover #29384E and keyboard focus #79B4FF.
`STButton` owns button hover, pressed, disabled and focus states. The top toolbar
uses native WPF `MenuItem` controls styled by `STToolbarMenuItem`, its primary
variant and `STToolbarSubmenuItem` in the same resource dictionary. Modal
feedback continues to use the MahApps theme.

The toolbar groups creation and import on the left, selected-deck actions beside
them and statistics on the right. New deck is the primary action. Buttons are
40px high with sentence-case labels, and drop-downs share the dark palette and
keyboard focus states. Keep the three library, match-history and selected-deck
columns below; the toolbar uses their outer horizontal alignment.

Canonical ownership for migration: `MainWindowMenuView` owns its entry points;
Windows `OpenFileDialog` owns local file selection; MahApps `ShowProgressAsync`
owns pending feedback; `ShowMessageAsync` owns instructions, preview, errors and
completion. Follow the active WPF owner's keyboard/focus handling. New custom
dialogs or screen-local palettes are unnecessary for this workflow.

Migration is explicitly initiated, additive and local. Before commit, show new
decks, merged decks, matches, duplicates, skipped records and unassigned matches.
Allow cancellation without modifying the profile. Prevent concurrent submission;
explain why migration is unavailable during a match. Preserve errors in a readable
dialog and give a correction action. Save with a backup and atomic replacement before
refreshing existing deck/statistics surfaces. Data input files remain read-only.

Russian migration labels follow the existing Russian library and deck-code dialog.
Dates in imported history use the Windows local timezone, as existing match records
do. This native WPF task has no browser routes, CSS, or web responsive breakpoints.

The library belongs to Standard Tracker: migration converts source files into
the independent `StandardTracker.Library` schema in `library.json`. HDT XML is
only an input adapter. Keep format names and storage mechanics out of the
player's migration steps; show meaningful counts and the actual backup location.
