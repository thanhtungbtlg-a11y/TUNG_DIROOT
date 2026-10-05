# TUNG_DIROOT 1.3.0 RC2 — review fixes

User approved inline continuation, Revit 2024 only, local master integration and recoverable upgrades.

## Implementation batch

1. Installer: copy Samples/Docs, automatic owned-install backup, verified non-destructive restore, sandbox regressions.
2. TableGen: retain original style mapping keys through black/white, Schedule profile on initial import/Recreate, preserve DataOnly text/structure scope and reject destructive fallback; explicit confirmed rebuild, shared-image deletion guard, confirmed transaction outcome and UI reentrancy guard.
3. FamilyReviser: invalidate stale Preview, reject duplicate identity rows, revalidate only confirmed Ready rows and report failures.
4. Sheet tools: old→new Preview, optional REVISION tab and same-batch revision references, reject unsupported view changes, invalidate stale Preview.
5. Bulk V/G: selected-only Replace, preserve unchecked groups, source order, Update Existing and explicit Preview for removals/order/skips.
6. Independent review, complete automated gate, clean source commit, RC2 artifacts/SHA-256, local master fast-forward. Preserve RC1 separately.

## Evidence and limits

Use failing behavioral regressions before fixes where runnable. Revit API build/contracts and sandbox installs do not prove live Revit operation; all direct Revit checklists remain NOT RUN until actually exercised.

Advanced all-tab V/G copying remains outside this corrective batch and is still recorded as remaining roadmap work. No real-user installation or automatic model save/central sync in the build flow.

## Batch result — 2026-10-03

Steps 1–6 completed for the corrective RC2 scope: independent review findings addressed, 502 automated tests pass on merged master, Revit 2024 builds without warnings/errors, sandbox install/backup/restore and final ZIP payload verified. RC2 artifacts and hashes are preserved separately from RC1. See [release report](../../testing/release-1.3.0-rc2-report.md). Live Revit checklists and advanced all-tab V/G remain open; the continuation worktree is retained.
