# Plan 3 — Link manager and synchronization smoke test

Status: **NOT RUN** (requires an interactive Revit 2024 session and a workshared test model).

- [ ] Create five links: unchanged, changed, missing, locked, and a view owned by another user.
- [ ] Open TableGen once; invoking the Ribbon command again activates the same modeless manager window.
- [ ] Check Sources reports `Ready`, `Changed`, `FILE_MISSING`, `FILE_LOCKED`, and `VIEW_NOT_EDITABLE` as appropriate.
- [ ] Update all five links. One failure rolls back only its view and later links continue.
- [ ] Pause while a view is updating: the current transaction completes, then the batch pauses.
- [ ] Resume continues with the next view. Stop completes the current transaction and starts no further view.
- [ ] Recreate leaves an untagged, user-created TextNote in the target view untouched.
- [ ] Data-only updates linked TextNotes/Schedule header text without deleting user elements.
- [ ] Image mode replaces only the tagged ImageInstance/ImageType pair.
- [ ] Relink Folder chooses a unique filename + worksheet + region match and never auto-selects an ambiguous match.
- [ ] Enable Auto Sync, close and reopen the model: changed links queue after the model becomes active.
- [ ] Confirm Auto Sync does not save, Save As, Synchronize with Central, or post a Revit command.
- [ ] Close/reopen the model and confirm registry data remains; delete registry DataStorage and confirm marker recovery.
- [ ] Inspect `%LocalAppData%\TUNG_DIROOT\Logs`: entries contain metadata/error details but no Excel cell values.

Record actual result, Revit build number, model path, tester, and date before marking any item PASS.
