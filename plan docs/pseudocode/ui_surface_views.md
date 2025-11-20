# UI / Surface View Pseudocode Prompts

These diagrams explain how users interact with any VBA-driven solution through forms and worksheets; invSys is just a sample fixture. Pseudocode must capture control hierarchies and event wiring.

## Shared Metadata Needs
- Inventory of UserForms and Worksheets (names, captions, roles).
- Control tree per form (buttons, combos, listboxes, labels).
- Event handler mapping (control → procedure → module).
- Navigation hints (which form launches which).

## 23. Forms Map
- **Goal:** show all forms and worksheet entry points.
- **Pseudocode:** iterate modules tagged `UI.Forms` or `UI.Sheets`, emit nodes per form/sheet, annotate with purpose, draw edges for navigation (Show/Hide calls).
- **Details:** deduce hidden/utility forms vs. primary UI, include icons for sheet vs. form.

## 24. Controls Interaction View
- **Goal:** visualize buttons/controls and how they interact with shared state.
- **Pseudocode:** traverse forms, list controls, map each control to event handlers, show shared data targets (tables, modules).
- **Questions:** how to limit to meaningful controls (skip labels), naming conventions for events.

## 25. Event Entry Point View
- **Goal:** map events (button clicks, Worksheet_Change) to modules and downstream procedures.
- **Pseudocode:** detect event handler signatures, tie them to controls/worksheets, draw edges to core modules invoked.
- **Need clarity on:** representing multi-event handlers, distinguishing synchronous vs. scheduled events.

## 26. User Navigation View
- **Goal:** show how users move between forms/screens.
- **Pseudocode:** analyze `Show`, `Unload`, `Activate` calls, build navigation graph, annotate with triggers (button names).
- **Outstanding:** capturing implicit navigation (macro assigned to buttons on sheets), dealing with modal vs. modeless forms.
