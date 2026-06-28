# Preview Examples

Demo prefabs that show off Project Designer's custom Project-window thumbnails.
Unity normally shows a blank or generic icon for these; Project Designer renders
a real preview over them.

| Folder | Prefab | Preview type |
| --- | --- | --- |
| `UI/` | `UI Card` | UI / Canvas prefab — a single rendered thumbnail |
| `Particle/` | `Sparkles` | Particle system — a short looping animation |
| `TextMeshPro/` | `Title Text` | World-space (3D) TextMeshPro text — a static thumbnail |

After importing this sample, select the folders in the Project window to see the
thumbnails. If they don't appear, open **Window → Project Designer** and click
**Refresh Previews**.

**Note:** the `Title Text` example uses TextMeshPro's default font
(`LiberationSans SDF`). If it looks unstyled, import TMP's essentials via
**Window → TextMeshPro → Import TMP Essential Resources**.
