# Bulk Visibility & Graphics Manager — Implementation Plan

## Goal

Copy category and filter visibility/graphic overrides from one View or View Template to many Views, View Templates, or all Viewports on selected Sheets. Preview differences before applying and keep a restorable in-session backup.

## Tasks

1. Add pure target-expansion and difference models with tests.
2. Read Revit View/View Template/Sheet catalogs and source Category/Filter settings.
3. Preview per-target differences without starting a transaction.
4. Apply selected properties in `Merge`, `Replace`, or `Update Existing` mode, one target per transaction.
5. Save the pre-apply state in memory and expose `Khôi phục lần Apply gần nhất`; retain native Revit Undo.
6. Add a separate ribbon command/window, responsive UI, contracts, user guide and Revit 2024 checklist.

## Review fix contract (2026-10-02)

- Every mode preserves unchecked property groups on existing Categories/Filters. New filters have only Revit defaults to retain.
- Replace uses only selected source Filters as the final target list, in source order. A Category-only selection does not replace Filters.
- Revit 2024 only appends Filters, so an order rebuild snapshots/restores complete existing selected Filter settings before copying checked groups.
- Preview and Apply share one pure filter membership/order planner. Preview explicitly lists removals, reordering, additions and Update Existing skips.
- Update Existing changes only selected Filters already present; it never adds/removes/reorders. Category copying still applies checked groups.
- Visibility preview compares the visibility and enabled flags separately. Automated core coverage does not replace the NOT RUN interactive Revit checklist.
