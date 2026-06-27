# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-06-27

### Added
- Prefab previews for the kinds Unity leaves blank in the Project window (it already thumbnails mesh/sprite prefabs):
  - **UI / Canvas prefabs** now show a rendered thumbnail of their UI.
  - **Particle systems** now show a short looping animation — the system is pre-simulated into a frame strip and the Project window cycles through it. Continuous emitters are pre-rolled to a steady-state loop; one-shot / burst systems play their full lifecycle (the burst fires and fades within the loop). Animation repaints are throttled and only run while a particle preview is actually visible, so idle folders cost nothing. An **Animation FPS** slider controls how many frames are sampled per second of the effect — higher captures more frames (smoother), played back at real-time speed rather than faster.

  Both are rendered in an isolated preview scene on a deferred, one-per-tick work queue and cached (invalidated by the asset's dependency hash), so the window stays responsive and nothing pollutes the open scene. Works in both list and grid layouts. Mesh/sprite/etc. prefabs keep Unity's own preview; content-less prefabs keep the generic icon. The two preview types toggle independently under **General** in the Settings window, or via `Tools > Project Designer > UI Previews` and `Tools > Project Designer > Particle Previews`. Both enabled by default. No new package dependencies.

### Changed
- Updated the shipped defaults: alternating rows are now **on**, the tree uses **Default** mode (elbow connectors), the line style is **Solid**, and the default row tint is a slightly stronger `#0000002A`. This only affects fresh installs and "Reset to Defaults" — existing projects keep their saved settings.

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

[1.2.0]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.2.0
[1.1.0]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.1.0
[1.0.0]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.0.0
