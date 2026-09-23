# Standard Tracker — checkpoint 2026-09-23

Stopped at user-requested quota threshold (4% remaining). Branch standard-tracker.
Remote: https://github.com/soranocode/standard-tracker.

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
- Inspect offline card art/language presentation; downloads are disabled.
- Pin/provision ignored lib/localizations for fresh checkout.
- Legacy packaging/release scripts and GitHub workflows still target HearthSim. DO NOT use
  them unchanged. Auto-review rejected renaming workflows to disable them, requiring explicit
  user approval because it affects CI. No workflow changes were made.

Local logs: tests.log, smoke.log. Temporary editing scripts are not product files.
No reset credits used. Preserve original HDT profile and licenses/provenance.
