# PDF/Word Import — Implementation Plan

**Goal:** Import selected PDF/Word pages as linked Revit images in a Drafting View, arrange many pages predictably, persist the link, and update or remove it safely later.

**Constraints:** Revit 2024 only; one transaction per document link; no automatic model Save or Synchronize with Central; source monitoring only reports changes and never mutates Revit from a watcher thread.

## Task 1 — Pure core domain (TDD)

- Parse page expressions such as `1,3,5-7`, reject invalid or excessive selections.
- Validate PDF/Word requests, DPI, column count, view name and supported extensions.
- Plan deterministic multi-page placement without Revit API dependencies.
- Store source fingerprint and document-link metadata.

## Task 2 — Source preparation

- Use PDF files directly.
- Convert Word `.doc/.docx/.docm` to a stable cached PDF using Microsoft Word automation when available.
- Always close the Word document, quit Word and release COM objects on success and failure.
- Produce actionable errors when Word is unavailable or conversion fails.

## Task 3 — Revit transaction layer

- Create a Drafting View and linked `ImageType` for every selected page through the native Revit 2024 PDF API.
- Arrange pages in the requested number of columns and tag generated types/instances.
- Persist records in a separate Extensible Storage registry.
- Update one link atomically; rollback that link only on failure.
- Unlink while keeping content, or delete link plus generated View, with Undo support.

## Task 4 — User interface

- Add a dedicated `PDF / Word` action to TableGen.
- Provide drag/drop and Browse, page expression, 150/300/600 DPI, columns and target View name.
- Show existing document links, Changed/Missing state, and commands for import, update selected, open source/View and remove.
- Keep the window responsive at low screen sizes and prevent cross-document execution.

## Task 5 — Verification and release

- Add unit tests, Revit API source contracts and responsive-layout contracts.
- Add a Revit 2024 manual checklist.
- Run the full repository suite and rebuild the installer/portable package.
