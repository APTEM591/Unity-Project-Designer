# Changelog

All notable changes to this package, listed by commit. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.1] - 2026-06-28
- SRP/URP rendering support via `SubmitRenderRequest` (previews no longer blank in URP/HDRP projects)
- Fragment UI prefab rendering: prefabs without their own Canvas are wrapped in a temporary Canvas sized to the fragment's `RectTransform`
- UIParticle fix: any `CanvasRenderer` child takes priority over `ParticleSystem` for prefab-kind detection
- `CanvasScaler` disabled during preview to prevent scale artifacts
- `LayoutRebuilder.ForceRebuildLayoutImmediate` call after `ForceUpdateCanvases` for correct layout group bounds
- Empty-canvas guard: UI renders that are effectively transparent are discarded (falls back to generic icon)
- **Refresh Previews** button added to the Project Designer settings window

## [1.2.0] - 2026-06-27
- 57eef5b - Release 1.2.0: UI & particle prefab previews
- 4e32e98 - Update README

## [1.1.0] - 2026-05-31
- 17ca013 - Release 1.1.0: configurable alternating row color
- 083d6e0 - Update README

## [1.0.0] - 2026-05-31
- e277907 - Initial release: Project Designer 1.0.0

[1.2.1]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.2.1
[1.2.0]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.2.0
[1.1.0]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.1.0
[1.0.0]: https://github.com/APTEM591/Unity-Project-Designer/releases/tag/1.0.0
