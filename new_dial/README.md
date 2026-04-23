# New Dial Dialogue Editor

Node-based dialogue and cutscene editor MVP for Unity 6.x.

## Included

- Reusable runtime data model based on a dialogue database asset
- Graph editor with text nodes, comment nodes, links, conditions, and preview
- Autosave snapshots stored outside tracked project assets
- Sample bootstrap that creates a branching adventure dialogue database

## Package Layout

- `Runtime`: public data model, condition contracts, and traversal player
- `Editor`: start window, graph editor, preview, and autosave tooling
- `Samples~`: sample bootstrap scripts and usage notes
- `Tests`: EditMode tests for data traversal and editor utilities

## Opening the Editor

Use `Window/New Dial/Dialogue Editor`.

## Notes

- The graph surface uses `UnityEditor.Experimental.GraphView`, which is still marked experimental in Unity 6.x.
- MVP scope intentionally excludes production in-game UI, advanced condition expressions, undo/redo, and Function/Scene/Debug execution logic.
