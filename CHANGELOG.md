# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-05-31

### Added
- Configurable alternating row color: the row-striping tint is now editable in the Settings window under a new **Alternating Rows** section (`Tools > Project Designer > Settings...`). Adjust the alpha for subtler or stronger striping. Defaults to the previous skin-matched shade and reverts to it on "Reset to Defaults".

## [1.0.0] - 2026-05-31

### Added
- Content-based folder icons: small emblems overlaid on folder icons based on their contents (Scripts, Prefabs, Scenes, Materials, etc.).
- Custom folder colors: per-folder color tint assigned from the Project window right-click context menu (stored by asset GUID, stable across renames/moves).
- Tree branch connector lines in the Project window list view, with Minimal and Default modes and solid/dotted/dashed line styles.
- Alternating row shading in the list view for better readability.
- Settings window under `Tools > Project Designer > Settings...`, plus quick toggles under the `Tools > Project Designer` menu.
- Zero external dependencies; Editor-only assembly definition (`GameSpear.ProjectDesigner.Editor`).

[1.1.0]: https://github.com/APTEM591/com.gamespear.project-designer/releases/tag/1.1.0
[1.0.0]: https://github.com/APTEM591/com.gamespear.project-designer/releases/tag/1.0.0
