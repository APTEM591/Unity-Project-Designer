# Project Designer

A lightweight, zero-dependency Unity Editor extension that enhances the Project window with better visualization and organization tools.

<img width="325" height="215" alt="image" src="https://github.com/user-attachments/assets/0723d831-1a07-4295-846f-958bd1e52300" />

## Features

- **Content-Based Folder Icons**: Automatically adds small emblems to folder icons based on their contents (e.g., Scripts, Prefabs, Scenes, Materials).
- **Prefab Previews**: Unity already thumbnails mesh and sprite prefabs, but leaves UI/Canvas prefabs as a plain blue icon and shows particle systems un-simulated. This renders those in an isolated preview scene so UI prefabs get a real thumbnail and particle systems get a short looping animation (list and grid views). Generated lazily off the GUI thread and cached to stay lightweight; animation only repaints while a particle preview is on screen, and other prefabs keep Unity's own preview.
- **Custom Folder Colors**: Color-tint your folders to make them stand out. (Access via right-click context menu on any folder).
- **Tree Branch Lines**: Adds visual connector lines to the project hierarchy in list view, making it easier to see parent-child relationships.
- **Alternating Rows**: Adds subtle alternating background colors to rows in the list view for better readability. The tint color (and its strength via alpha) is customizable in the Settings window.

## Installation

### Via OpenUPM (recommended)

Using the [openupm-cli](https://github.com/openupm/openupm-cli):

```
openupm add com.gamespear.project-designer
```

### Via Unity Package Manager (Git URL)

1. Open Unity and go to `Window > Package Manager`.
2. Click the `+` button in the top-left corner.
3. Select `Add package from git URL...`.
4. Enter: `https://github.com/APTEM591/Unity-Project-Designer.git`

### Via .unitypackage

Download the latest `.unitypackage` from the [Releases](https://github.com/APTEM591/Unity-Project-Designer/releases) page and import it via `Assets > Import Package > Custom Package...`.

## Usage & Settings

All features are enabled by default. You can configure or toggle them at any time:
- Go to `Tools > Project Designer > Settings...` to open the configuration window.
- Alternatively, use the quick toggles under the `Tools > Project Designer` menu to turn individual features on or off.

### Folder Colors
Right-click on any folder in the Project window and navigate to `Project Designer > Folder Color`. Choose from preset colors or click `Clear` to return to the default.
