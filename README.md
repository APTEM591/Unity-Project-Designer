# Project Designer

A lightweight, zero-dependency Unity Editor extension that enhances the Project window with better visualization and organization tools.

## Features

- **Content-Based Folder Icons**: Automatically adds small emblems to folder icons based on their contents (e.g., Scripts, Prefabs, Scenes, Materials).
- **Custom Folder Colors**: Color-tint your folders to make them stand out. (Access via right-click context menu on any folder).
- **Tree Branch Lines**: Adds visual connector lines to the project hierarchy in list view, making it easier to see parent-child relationships.
- **Alternating Rows**: Adds subtle alternating background colors to rows in the list view for better readability.

## Installation

### Via OpenUPM (recommended)

Using the [openupm-cli](https://github.com/openupm/openupm-cli):

```
openupm add com.gamespear.project-designer
```

Or add the scoped registry manually in `Edit > Project Settings > Package Manager`:

- **Name:** `package.openupm.com`
- **URL:** `https://package.openupm.com`
- **Scope(s):** `com.gamespear.project-designer`

Then install **Project Designer** from `Window > Package Manager` (My Registries).

### Via Unity Package Manager (Git URL)

1. Open Unity and go to `Window > Package Manager`.
2. Click the `+` button in the top-left corner.
3. Select `Add package from git URL...`.
4. Enter: `https://github.com/APTEM591/com.gamespear.project-designer.git`

### Via .unitypackage

Download the latest `.unitypackage` from the [Releases](https://github.com/APTEM591/com.gamespear.project-designer/releases) page and import it via `Assets > Import Package > Custom Package...`.

### Via Local Folder

1. Clone or download this repository.
2. Add it via the Package Manager using `Add package from disk...` and selecting `package.json`, or copy the folder into your project's `Assets`.

## Usage & Settings

All features are enabled by default. You can configure or toggle them at any time:
- Go to `Tools > Project Designer > Settings...` to open the configuration window.
- Alternatively, use the quick toggles under the `Tools > Project Designer` menu to turn individual features on or off.

### Folder Colors
Right-click on any folder in the Project window and navigate to `Project Designer > Folder Color`. Choose from preset colors or click `Clear` to return to the default.
