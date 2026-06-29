#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if PROJECT_DESIGNER_TMP
using TMPro;
#endif

namespace GameSpear.ProjectDesigner.Editor
{
    // Renders previews for prefabs that Unity's built-in AssetPreview leaves blank. Unity already
    // thumbnails mesh/sprite/etc. prefabs in the Project window, so re-drawing those adds nothing. A few
    // kinds genuinely show only the generic icon, and this fills them, leaving everything else to Unity:
    //
    //   * UI / Canvas prefabs  — a single rendered thumbnail (AssetPreview never renders screen-space UI).
    //   * Particle systems     — a short looping animation (AssetPreview shows them un-simulated / empty).
    //   * World-space TMP text — a static thumbnail (AssetPreview can't render TMP's generated mesh).
    //
    // A "combined" prefab — a sprite/mesh character that also carries UI (e.g. a world-space health bar) —
    // is rendered through the UI path but framed on the CHARACTER, folding in the UI only when it sits close
    // enough not to shrink the body. A bar floating far above the head (or an oversized world-space canvas)
    // is cropped instead of dominating the thumbnail (see TryGetPreviewBounds).
    //
    // All are rendered in an isolated preview scene via a camera bound to that scene (Camera.scene), read
    // back into an owned Texture2D. Particle frames are pre-simulated and packed into one horizontal atlas;
    // the Project window cycles through them. PreviewRenderUtility can't do either — UGUI only emits
    // geometry for a camera attached to a real/preview scene, not PRU's internal camera.
    //
    // Cost is kept off the GUI thread: a prefab is rendered at most once (deferred to a one-per-tick work
    // queue) and cached per GUID, keyed by the asset's dependency hash so edits regenerate. The animation
    // only drives repaints while a particle preview is actually on screen. We own the cached textures, so
    // they are destroyed on eviction / cache clear to avoid leaks.
    internal static class PrefabPreview
    {
        private enum Kind { None, Ui, Particle, TmpText }

        private const int ThumbSize = 128;
        private const int MaxCache = 256;
        private const int MinParticleFrames = 6;        // floor on captured frames (very short loops)
        private const int MaxParticleFrames = 36;       // cap on captured frames (atlas width = N * ThumbSize)
        private const double AnimVisibleWindow = 0.5;   // keep animating this long after the last visible frame

        // A combined prefab frames on its character; nearby UI (e.g. a health bar) is folded into the frame
        // only while it keeps the framed size within this factor of the character alone. Beyond it the UI is
        // far enough away (or large enough) that including it would shrink the character to a speck, so it is
        // left out of the framing and simply cropped.
        private const float MaxCombinedFrameFactor = 1.6f;

        // Opaque neutral backdrop (≈ AssetPreview's) so the thumbnail fully covers the generic icon.
        private static readonly Color Background = new Color(0.32f, 0.32f, 0.32f, 1f);

        private static readonly Dictionary<string, Texture2D> _cache = new();
        private static readonly Dictionary<string, Hash128> _hashes = new();
        private static readonly Dictionary<string, Hash128> _failed = new();
        private static readonly Dictionary<string, Kind> _kind = new();
        private static readonly Dictionary<string, int> _frames = new();    // particle: captured frame count
        private static readonly Dictionary<string, float> _fps = new();     // particle: real-time playback rate
        private static readonly List<string> _lru = new();
        private static readonly List<string> _queue = new();
        private static readonly HashSet<string> _queued = new();
        private static bool _hooked;
        private static double _animVisibleTime;   // last time an animated preview was drawn
        private static double _lastAnimRepaint;

        public static void Initialize()
        {
            // Keep Unity's own (mesh/sprite) previews from dropping out while scrolling large folders.
            try { AssetPreview.SetPreviewTextureCacheSize(256); }
            catch { /* non-fatal */ }

            if (!_hooked) { _hooked = true; EditorApplication.update += OnEditorUpdate; }
        }

        public static bool IsPrefab(string path)
            => !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            foreach (Texture2D tex in _cache.Values)
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            _cache.Clear();
            _hashes.Clear();
            _failed.Clear();
            _kind.Clear();
            _frames.Clear();
            _fps.Clear();
            _lru.Clear();
            _queue.Clear();
            _queued.Clear();
        }

        // Drop cached particle previews so they regenerate at the current capture rate (called when the
        // Animation FPS setting changes). Classification is kept and UI previews are left untouched.
        public static void InvalidateParticles()
        {
            List<string> particles = new();
            foreach (KeyValuePair<string, Kind> kv in _kind)
                if (kv.Value == Kind.Particle) particles.Add(kv.Key);

            foreach (string guid in particles)
            {
                if (_cache.TryGetValue(guid, out Texture2D tex) && tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
                _cache.Remove(guid);
                _hashes.Remove(guid);
                _failed.Remove(guid);
                _frames.Remove(guid);
                _fps.Remove(guid);
                _lru.Remove(guid);
            }
            EditorApplication.RepaintProjectWindow();
        }

        // A custom thumbnail for a prefab Unity can't preview (UI, particle, or TMP text), or false to leave
        // Unity's native preview / generic icon untouched. 'uv' selects the current animation frame within
        // the texture (the whole texture for static thumbnails). Enqueues generation when not yet cached or
        // when the cached copy has gone stale.
        public static bool TryGetPreview(string guid, out Texture2D tex, out Rect uv)
        {
            tex = null;
            uv = new Rect(0f, 0f, 1f, 1f);
            if (string.IsNullOrEmpty(guid)) return false;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return false;

            if (!Settings.UiPreviewEnabled && !Settings.ParticlePreviewEnabled) return false;

            Kind kind = ClassifyCached(guid, path);
            if (kind == Kind.None) return false;
            if (kind == Kind.Ui && !Settings.UiPreviewEnabled) return false;
            if (kind == Kind.Particle && !Settings.ParticlePreviewEnabled) return false;
            if (kind == Kind.TmpText && !Settings.UiPreviewEnabled) return false;

            Hash128 hash = AssetDatabase.GetAssetDependencyHash(path);
            if (_cache.TryGetValue(guid, out Texture2D cachedTex) && cachedTex != null &&
                _hashes.TryGetValue(guid, out Hash128 cachedHash) && cachedHash == hash)
            {
                Touch(guid);
                tex = cachedTex;
                if (kind == Kind.Particle)
                {
                    uv = CurrentFrameUv(guid);
                    _animVisibleTime = EditorApplication.timeSinceStartup; // keep the animation pumping
                }
                return true;
            }

            // Already failed to render at this hash (e.g. an empty Canvas or a system that emits nothing) —
            // keep the generic icon and don't re-queue every repaint until the asset changes.
            if (_failed.TryGetValue(guid, out Hash128 failedHash) && failedHash == hash) return false;

            if (_queued.Add(guid)) _queue.Add(guid);
            return false;
        }

        // The atlas sub-rect for the frame that should show right now, using the frame count and real-time
        // playback rate captured for this prefab. All previews share the editor clock, so they stay in sync.
        private static Rect CurrentFrameUv(string guid)
        {
            if (!_frames.TryGetValue(guid, out int frames) || frames <= 0)
                return new Rect(0f, 0f, 1f, 1f);
            float fps = _fps.TryGetValue(guid, out float f) && f > 0f ? f : 12f;
            int frame = (int)(EditorApplication.timeSinceStartup * fps) % frames;
            if (frame < 0) frame += frames;
            return new Rect((float)frame / frames, 0f, 1f / frames, 1f);
        }

        // What kind of preview, if any, we should render for this prefab. Cached per GUID — the only place
        // we load the prefab asset, and only for visible prefabs.
        private static Kind ClassifyCached(string guid, string path)
        {
            if (_kind.TryGetValue(guid, out Kind k)) return k;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            k = Kind.None;
            if (go != null)
            {
                bool hasCanvasRenderer = go.GetComponentInChildren<CanvasRenderer>(true) != null;
                bool hasParticle = go.GetComponentInChildren<ParticleSystem>(true) != null;
#if PROJECT_DESIGNER_TMP
                // TextMeshPro (non-UGUI) has no CanvasRenderer; TextMeshProUGUI has one, so it is already
                // caught as UI above. Only the 3D/world-space variant needs a dedicated path here.
                bool hasTmpText = !hasCanvasRenderer && go.GetComponentInChildren<TextMeshPro>(true) != null;
#endif
                // Any CanvasRenderer means UI (standalone or fragment) — takes priority over particle.
                // UIParticle components add both a ParticleSystem and a CanvasRenderer; the prefab is
                // still fundamentally a UI widget and must go through RenderUi (RenderParticle finds no
                // standard Renderer bounds for UIParticle, so it always returns null for such prefabs).
                // Only prefabs with no CanvasRenderer at all (pure VFX) are rendered as particles.
                // A prefab that ALSO has standard Renderers (a sprite/mesh character with a world-space
                // UI health bar) is still Kind.Ui, but RenderUi frames the character (folding in nearby UI),
                // not just the UI.
                if (hasCanvasRenderer) k = Kind.Ui;
                else if (hasParticle) k = Kind.Particle;
#if PROJECT_DESIGNER_TMP
                else if (hasTmpText) k = Kind.TmpText;
#endif
            }
            _kind[guid] = k;
            return k;
        }

        private static void OnEditorUpdate()
        {
            // Drive the particle animation: repaint the Project window at a throttled rate, but only while
            // an animated preview was recently on screen (so idle folders cost nothing).
            double now = EditorApplication.timeSinceStartup;
            double interval = 1.0 / Mathf.Max(1, Settings.ParticlePreviewFps);
            if (now - _animVisibleTime < AnimVisibleWindow && now - _lastAnimRepaint >= interval)
            {
                _lastAnimRepaint = now;
                EditorApplication.RepaintProjectWindow();
            }

            ProcessQueue();
        }

        private static void ProcessQueue()
        {
            if (_queue.Count == 0) return;
            // Generating during play mode would run the prefab's scripts inside the running game; defer.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string guid = _queue[0];
            _queue.RemoveAt(0);
            _queued.Remove(guid);

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            Kind kind = ClassifyCached(guid, path);
            if (kind == Kind.None) return;

            Hash128 hash = AssetDatabase.GetAssetDependencyHash(path);
            int frames = 0;
            float fps = 0f;
            Texture2D tex = kind == Kind.Particle ? RenderParticle(path, out frames, out fps)
#if PROJECT_DESIGNER_TMP
                : kind == Kind.TmpText ? RenderTmpText(path)
#endif
                : RenderUi(path);
            if (tex != null)
            {
                Store(guid, tex, hash);
                if (kind == Kind.Particle) { _frames[guid] = frames; _fps[guid] = fps; }
                _failed.Remove(guid);
            }
            else { _failed[guid] = hash; }

            EditorApplication.RepaintProjectWindow();
        }

        private static void Store(string guid, Texture2D tex, Hash128 hash)
        {
            if (_cache.TryGetValue(guid, out Texture2D old) && old != null && old != tex)
                UnityEngine.Object.DestroyImmediate(old);

            _cache[guid] = tex;
            _hashes[guid] = hash;
            Touch(guid);

            while (_lru.Count > MaxCache)
            {
                string evict = _lru[0];
                _lru.RemoveAt(0);
                if (_cache.TryGetValue(evict, out Texture2D t))
                {
                    if (t != null) UnityEngine.Object.DestroyImmediate(t);
                    _cache.Remove(evict);
                    _hashes.Remove(evict);
                    _frames.Remove(evict);
                    _fps.Remove(evict);
                }
            }
        }

        private static void Touch(string guid)
        {
            _lru.Remove(guid);
            _lru.Add(guid);
        }

        // Renders a UI prefab to an owned Texture2D via an isolated preview scene. Returns null on any
        // failure (e.g. an empty or transparent canvas), leaving the generic icon in place.
        //
        // Handles three cases:
        //   * Standalone canvas prefabs: the prefab owns its Canvas; used directly.
        //   * Fragment UI prefabs (buttons, cards, list items): have CanvasRenderer children but no Canvas
        //     of their own (they live inside the scene's root canvas). We create a temporary wrapper Canvas
        //     sized to the fragment's RectTransform so it renders at its authored scale.
        //   * Combined prefabs: a sprite/mesh character that also carries UI (e.g. a world-space health
        //     bar). The camera frames the character and folds in the UI only when it is close
        //     (TryGetPreviewBounds), rendering everything (cullingMask = ~0) so the body is the subject
        //     rather than the UI alone.
        private static Texture2D RenderUi(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;

            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture prevActive = RenderTexture.active;
            GameObject instance = null;
            GameObject canvasWrapper = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                ActivateForPreview(instance);

                Canvas canvas = instance.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    // Fragment UI: wrap in a temporary Canvas sized to the fragment's authored dimensions.
                    canvasWrapper = new GameObject("PD_Canvas");
                    SceneManager.MoveGameObjectToScene(canvasWrapper, scene);
                    canvas = canvasWrapper.AddComponent<Canvas>(); // also attaches a RectTransform
                    RectTransform wrapRt = canvasWrapper.GetComponent<RectTransform>();
                    RectTransform fragRt = instance.GetComponent<RectTransform>();
                    if (wrapRt != null)
                    {
                        // Use the fragment's authored size only when both axes are positive.
                        // Negative sizeDelta means the fragment uses stretch anchors (fills its parent minus
                        // an offset) — there is no meaningful standalone size, so fall back to 400×400.
                        bool hasPositiveSize = fragRt != null
                            && fragRt.sizeDelta.x > 0f && fragRt.sizeDelta.y > 0f;
                        wrapRt.sizeDelta = hasPositiveSize ? fragRt.sizeDelta : new Vector2(400f, 400f);
                    }
                    instance.transform.SetParent(canvasWrapper.transform, false);
                    // Reset the fragment's position so it sits at the wrapper origin rather than at
                    // whatever absolute anchoredPosition it had in its original scene hierarchy.
                    RectTransform instRt = instance.GetComponent<RectTransform>();
                    if (instRt != null) instRt.anchoredPosition = Vector2.zero;
                }
                canvas.renderMode = RenderMode.WorldSpace;

                // A "combined" prefab is a sprite/mesh character carrying its OWN world-space canvas (a
                // health bar) alongside standard renderers. Its canvas is authored at a real world size and
                // must be left untouched — the rescue below is only for pure-UI prefabs.
                bool combined = canvasWrapper == null && instance.GetComponentInChildren<Renderer>(true) != null;

                // CanvasScaler in "Scale With Screen Size" mode derives a scaleFactor from the editor
                // screen size, which is arbitrary in a preview scene and can shrink or enlarge content
                // to the point where nothing visible falls inside the camera frustum.
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;

                // Rescue a degenerate canvas (no usable rect) by giving it a renderable size. NOT for a
                // combined prefab: a world-space health bar legitimately has a small rect (e.g. 2.39 x 0.42
                // world units, height < 1), and forcing it to 400x400 stretches the bar's anchored fill into
                // a full-frame backdrop that buries the character behind a solid block of the bar's colour.
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                if (!combined && canvasRect != null && (canvasRect.rect.width < 1f || canvasRect.rect.height < 1f))
                    canvasRect.sizeDelta = new Vector2(400f, 400f);

                GameObject camGo = new GameObject("PD_PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                Camera cam = camGo.AddComponent<Camera>();
                cam.scene = scene; // bind the camera to the preview scene so it (and only it) renders the UI
                canvas.worldCamera = cam;

                Canvas.ForceUpdateCanvases();
                // Canvas.ForceUpdateCanvases() only flushes dirty marks; nested layout groups (Grid,
                // HorizontalLayout, ContentSizeFitter, etc.) need an explicit rebuild to produce correct
                // world-space bounds before we frame the camera.
                GameObject layoutRoot = canvasWrapper != null ? canvasWrapper : instance;
                foreach (RectTransform childRt in layoutRoot.GetComponentsInChildren<RectTransform>(true))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(childRt);

                if (!TryGetPreviewBounds(instance, canvas, out Bounds b)) return null;

                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y, 0.01f) * 1.1f;
                cam.transform.position = b.center + new Vector3(0f, 0f, -50f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 1000f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
                cam.cullingMask = ~0;

                // Combined prefabs may include lit 3D meshes; a directional light keeps them from rendering
                // black. UI and 2D sprites use unlit shaders and ignore it, so pure-UI previews are unchanged.
                GameObject lightGo = new GameObject("PD_PreviewLight");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1f;

                rt = RenderTexture.GetTemporary(ThumbSize, ThumbSize, 16, RenderTextureFormat.ARGB32);
                RenderCamera(cam, rt);

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(ThumbSize, ThumbSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                tex.ReadPixels(new Rect(0f, 0f, ThumbSize, ThumbSize), 0, 0);
                tex.Apply();
                if (LooksEmpty(tex, 4)) { UnityEngine.Object.DestroyImmediate(tex); return null; }
                return tex;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                // Destroy from the top of the hierarchy: if the instance was parented to a wrapper canvas,
                // destroying the wrapper also destroys the instance.
                if (canvasWrapper != null) UnityEngine.Object.DestroyImmediate(canvasWrapper);
                else if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Renders a particle prefab to an owned horizontal atlas Texture2D ('frameCount' columns of
        // ThumbSize) by simulating the system across one loop, sampling more frames at higher capture FPS.
        // 'playbackFps' is the real-time rate to cycle those frames at. Returns null when there's nothing to
        // show (no renderers, or the captured frames are effectively empty), leaving the generic icon.
        private static Texture2D RenderParticle(string path, out int frameCount, out float playbackFps)
        {
            frameCount = 0;
            playbackFps = 0f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;

            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture prevActive = RenderTexture.active;
            GameObject instance = null;
            GameObject camGo = null;
            GameObject lightGo = null;
            Texture2D atlas = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                ActivateForPreview(instance);

                // Root systems are those not driven by a parent system (their children are simulated along
                // with them via Simulate(withChildren)). Fixing the random seed makes the captured frames a
                // smooth, repeatable loop instead of a different roll per frame.
                List<ParticleSystem> roots = new();
                foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.useAutoRandomSeed = false;
                    Transform parent = ps.transform.parent;
                    if (parent == null || parent.GetComponentInParent<ParticleSystem>() == null)
                        roots.Add(ps);
                }
                if (roots.Count == 0) return null;

                // Choose the time window to animate. Looping systems: pre-roll to steady state and span one
                // duration (so the loop tiles seamlessly). One-shot / burst systems: start at emission with
                // NO warm-up and span the full lifecycle — warming up past the burst is exactly why bursts
                // showed up empty, since the burst fires at its time and its particles are already dead by
                // the time a lifetime-long warm-up finishes.
                float maxLife = 0f, maxDur = 0f, maxEmit = 0f;
                bool anyLoop = false;
                foreach (ParticleSystem ps in roots)
                {
                    ParticleSystem.MainModule m = ps.main;
                    maxLife = Mathf.Max(maxLife, MaxLifetime(m));
                    maxDur = Mathf.Max(maxDur, m.duration);
                    maxEmit = Mathf.Max(maxEmit, EmissionEndTime(ps));
                    anyLoop |= m.loop;
                }
                // Non-looping span ends when the last particle dies: (when emission stops) + lifetime. For a
                // single t=0 burst that's just the lifetime, so the whole strip animates instead of going
                // dead halfway through.
                float warm = anyLoop ? Mathf.Clamp(maxLife, 0f, 4f) : 0f;
                float loop = Mathf.Clamp(anyLoop ? maxDur : maxEmit + maxLife, 0.25f, 6f);

                // Capture one frame per (1 / FPS) seconds of effect time, so a higher FPS samples MORE frames
                // (a smoother animation) rather than a faster one — the strip is played back at real time
                // (playbackFps = frames / loop), so the speed stays constant and only the smoothness changes.
                int fps = Mathf.Clamp(Settings.ParticlePreviewFps, 1, 60);
                int frames = Mathf.Clamp(Mathf.RoundToInt(loop * fps), MinParticleFrames, MaxParticleFrames);
                float step = loop / frames;
                frameCount = frames;
                playbackFps = frames / loop;

                // Pass 1: pre-roll, then step through the loop accumulating renderer bounds for a stable
                // framing. We capture AFTER each step (not before), so frame 0 already shows the emitted /
                // burst particles rather than an empty system at t=0.
                Simulate(roots, warm, true);
                Bounds b = default;
                bool anyBounds = false;
                for (int i = 0; i < frames; i++)
                {
                    Simulate(roots, step, false);
                    if (TryGetRendererBounds(instance, out Bounds fb))
                    {
                        if (!anyBounds) { b = fb; anyBounds = true; }
                        else b.Encapsulate(fb);
                    }
                }
                if (!anyBounds) return null;

                camGo = new GameObject("PD_PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                Camera cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
                cam.cullingMask = ~0;
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 5000f;
                float radius = Mathf.Max(b.extents.magnitude, 0.05f);
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + radius;
                cam.transform.position = b.center - Vector3.forward * dist;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

                lightGo = new GameObject("PD_PreviewLight");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1f;

                atlas = new Texture2D(ThumbSize * frames, ThumbSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                rt = RenderTexture.GetTemporary(ThumbSize, ThumbSize, 16, RenderTextureFormat.ARGB32);

                // Pass 2: replay the same simulation, capturing each frame into its atlas column (advance
                // first, mirroring pass 1, so the captured frames line up with the bounds we computed).
                Simulate(roots, warm, true);
                for (int i = 0; i < frames; i++)
                {
                    Simulate(roots, step, false);
                    CaptureFrame(cam, rt, atlas, i);
                }
                atlas.Apply();

                if (LooksEmpty(atlas)) return null;

                Texture2D result = atlas;
                atlas = null; // ownership handed to the cache; don't destroy it in 'finally'
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (lightGo != null) UnityEngine.Object.DestroyImmediate(lightGo);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Prefabs can be saved with their root GameObject inactive; instantiated as-is they draw nothing, so
        // the render comes back empty and we keep the generic icon. When the user opts in, force the root
        // active for the preview. We touch only the root (not inactive children) so the thumbnail matches how
        // the prefab looks when simply enabled, without revealing intentionally hidden child objects.
        // Activating runs the instance's OnEnable, but only inside this throwaway preview scene.
        private static void ActivateForPreview(GameObject instance)
        {
            if (instance != null && Settings.InactivePreviewEnabled && !instance.activeSelf)
                instance.SetActive(true);
        }

        // Advance each root system by 't' seconds (restart=true simulates from zero; restart=false steps on
        // from the current state so successive captures form a continuous animation).
        private static void Simulate(List<ParticleSystem> roots, float t, bool restart)
        {
            foreach (ParticleSystem ps in roots)
                ps.Simulate(t, true, restart, true);
        }

        private static void CaptureFrame(Camera cam, RenderTexture rt, Texture2D atlas, int frame)
        {
            RenderCamera(cam, rt);
            RenderTexture.active = rt;
            atlas.ReadPixels(new Rect(0f, 0f, ThumbSize, ThumbSize), frame * ThumbSize, 0);
        }

#if PROJECT_DESIGNER_TMP
        // Renders a world-space TextMeshPro prefab (non-UGUI) to a static thumbnail. ForceMeshUpdate
        // generates the text geometry synchronously; the camera frame is computed from the actual
        // generated glyph vertices (TMP_TextInfo) rather than Renderer.bounds (zero pre-render in a
        // preview scene) or MeshFilter.sharedMesh.bounds (unreliable for TMP meshes in-editor — see below).
        private static Texture2D RenderTmpText(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;

            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture prevActive = RenderTexture.active;
            GameObject instance = null;
            GameObject camGo = null;
            GameObject lightGo = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                ActivateForPreview(instance);

                // Generate each TMP component's mesh, then accumulate world-space bounds from the ACTUAL
                // generated glyph vertices. We deliberately do NOT use MeshFilter.sharedMesh.bounds: for a
                // TMP-generated mesh that AABB is unreliable in the editor — it reports a box covering only
                // part of the text (measured: center x=0.14, extent x=0.24 for text that truly spans
                // x:[-0.38,0.38]), which shifts the camera sideways and clips the leading glyph. The
                // per-character vertices in TMP_TextInfo are the real rendered geometry, so the frame is
                // exact and needs no fudge factor. Vertices are TMP-local; push them through the text
                // transform's localToWorld to handle scaled/rotated text objects.
                Bounds b = default;
                bool anyBounds = false;
                foreach (TMP_Text tmp in instance.GetComponentsInChildren<TMP_Text>(true))
                {
                    tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                    TMP_TextInfo info = tmp.textInfo;
                    if (info == null || info.characterCount == 0) continue;
                    Matrix4x4 localToWorld = tmp.transform.localToWorldMatrix;
                    for (int m = 0; m < info.meshInfo.Length; m++)
                    {
                        Vector3[] verts = info.meshInfo[m].vertices;
                        if (verts == null) continue;
                        // vertexCount (not verts.Length): the array is over-allocated and padded with
                        // zeros that would wrongly drag the bounds toward the origin.
                        int vertexCount = info.meshInfo[m].vertexCount;
                        for (int v = 0; v < vertexCount; v++)
                        {
                            Vector3 world = localToWorld.MultiplyPoint3x4(verts[v]);
                            if (!anyBounds) { b = new Bounds(world, Vector3.zero); anyBounds = true; }
                            else b.Encapsulate(world);
                        }
                    }
                }
                if (!anyBounds) return null;

                camGo = new GameObject("PD_PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                Camera cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.orthographic = true;
                // The vertex AABB already covers the real glyph quads (which include the SDF padding TMP
                // bakes into each quad), so this 8% is just uniform visual breathing room, not a fix for a
                // bad fit. The target RT is square, so the ortho half-size must satisfy the larger axis.
                cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y, 0.01f) * 1.08f;
                cam.transform.position = b.center + new Vector3(0f, 0f, -50f);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 1000f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;
                cam.cullingMask = ~0;

                lightGo = new GameObject("PD_PreviewLight");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1f;

                rt = RenderTexture.GetTemporary(ThumbSize, ThumbSize, 16, RenderTextureFormat.ARGB32);
                RenderCamera(cam, rt);

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(ThumbSize, ThumbSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                tex.ReadPixels(new Rect(0f, 0f, ThumbSize, ThumbSize), 0, 0);
                tex.Apply();
                if (LooksEmpty(tex, 4)) { UnityEngine.Object.DestroyImmediate(tex); return null; }
                return tex;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (lightGo != null) UnityEngine.Object.DestroyImmediate(lightGo);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
#endif

        // Render 'cam' into 'rt' under whichever render pipeline is active. Camera.Render() is the legacy
        // built-in path; under a Scriptable Render Pipeline (URP / HDRP) it draws nothing, so we must route
        // through SubmitRenderRequest — without this, every preview comes back blank in SRP projects. The
        // render-request API is 2022.2+, so older Unity versions fall back to the built-in call.
        private static void RenderCamera(Camera cam, RenderTexture rt)
        {
#if UNITY_2022_2_OR_NEWER
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    cam.SubmitRenderRequest(request);
                    return;
                }
            }
#endif
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
        }

        // Best-effort time at which a system stops emitting, used to size one-shot loops. Continuous
        // emission (rate over time/distance) runs for the whole duration; bursts run until their last cycle.
        private static float EmissionEndTime(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            ParticleSystem.EmissionModule em = ps.emission;
            if (!em.enabled) return main.duration;

            float end = 0f;
            if (CurveMax(em.rateOverTime) > 0f || CurveMax(em.rateOverDistance) > 0f)
                end = main.duration;

            int count = em.burstCount;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Burst burst = em.GetBurst(i);
                float burstEnd = burst.cycleCount == 0
                    ? main.duration // repeats for the whole duration
                    : burst.time + Mathf.Max(0, burst.cycleCount - 1) * burst.repeatInterval;
                end = Mathf.Max(end, burstEnd);
            }
            return end;
        }

        private static float CurveMax(ParticleSystem.MinMaxCurve c) => Mathf.Max(c.constant, c.constantMax);

        // Best-effort upper bound on a system's particle lifetime, used to warm up and to size one-shot loops.
        private static float MaxLifetime(ParticleSystem.MainModule m)
        {
            ParticleSystem.MinMaxCurve c = m.startLifetime;
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant: return Mathf.Max(c.constant, 0.1f);
                case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(c.constantMax, 0.1f);
                default: return Mathf.Max(c.constant, c.constantMax, 1f);
            }
        }

        // World bounds of all enabled Renderers on the instance, used to frame the camera.
        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                Bounds rb = r.bounds;
                if (rb.size == Vector3.zero) continue;
                if (!any) { bounds = rb; any = true; }
                else bounds.Encapsulate(rb);
            }
            return any;
        }

        // Sparse check for "the render is basically just the background" so we keep the generic icon instead
        // of showing a flat gray square (e.g. a system that emits nothing under simulation).
        // minDiffering: particle atlases have many frames so 12 is appropriate; a single thumbnail is
        // 128x128 so sparse content (thin bars, small icons) only hits a handful of the 169 samples — use 4.
        private static bool LooksEmpty(Texture2D atlas, int minDiffering = 12)
        {
            Color32[] px = atlas.GetPixels32();
            Color32 bg = Background;
            int differing = 0;
            for (int i = 0; i < px.Length; i += 97)
            {
                Color32 p = px[i];
                if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) > 24)
                {
                    if (++differing >= minDiffering) return false;
                }
            }
            return true;
        }

        // World-space bounds the preview camera should frame. The sprite/mesh character (its standard
        // Renderers) is the subject and drives the framing; UI is folded in only when it stays close.
        //
        //   * Pure UI prefab (no standard renderers): frames the Canvas's drawable rects, falling back to
        //     the canvas RectTransform when none are found — unchanged from before.
        //   * Combined prefab (sprite/mesh character + world-space UI such as a health bar): frames the
        //     character, then expands to include the UI only while that keeps the framed size within
        //     MaxCombinedFrameFactor of the character alone. A health bar floating far above the head, or an
        //     oversized world-space canvas, would otherwise dominate the thumbnail — shrinking the character
        //     to a speck or letting the bar fill the frame (the "all-green" health-bar thumbnail). In those
        //     cases the UI is left out of the framing and simply cropped (it is still drawn if it falls in
        //     view, since the camera renders everything).
        private static bool TryGetPreviewBounds(GameObject instance, Canvas canvas, out Bounds bounds)
        {
            bounds = default;
            Vector3[] corners = new Vector3[4];

            bool hasRenderers = TryGetRendererBounds(instance, out Bounds rendererBounds);

            // World-space bounds of every drawable UI element (CanvasRenderer) on the prefab.
            bool hasUi = false;
            Bounds uiBounds = default;
            foreach (CanvasRenderer cr in instance.GetComponentsInChildren<CanvasRenderer>(true))
            {
                RectTransform rt = cr.GetComponent<RectTransform>();
                if (rt == null) continue;
                rt.GetWorldCorners(corners);
                foreach (Vector3 c in corners)
                {
                    if (!hasUi) { uiBounds = new Bounds(c, Vector3.zero); hasUi = true; }
                    else uiBounds.Encapsulate(c);
                }
            }

            // Combined prefab: frame the character, including the UI only when it stays close enough.
            if (hasRenderers)
            {
                bounds = rendererBounds;
                if (hasUi)
                {
                    Bounds combined = rendererBounds;
                    combined.Encapsulate(uiBounds);
                    float rFrame = Mathf.Max(rendererBounds.extents.x, rendererBounds.extents.y, 0.0001f);
                    float cFrame = Mathf.Max(combined.extents.x, combined.extents.y);
                    if (cFrame <= rFrame * MaxCombinedFrameFactor) bounds = combined;
                }
                return true;
            }

            // Pure UI prefab: frame the drawable UI rects.
            if (hasUi) { bounds = uiBounds; return true; }

            // Nothing drawable found: fall back to the canvas RectTransform.
            if (canvas != null)
            {
                RectTransform crt = canvas.GetComponent<RectTransform>();
                if (crt != null)
                {
                    crt.GetWorldCorners(corners);
                    bounds = new Bounds(corners[0], Vector3.zero);
                    foreach (Vector3 c in corners) bounds.Encapsulate(c);
                    return bounds.size.sqrMagnitude > 0f;
                }
            }

            return false;
        }
    }
}
#endif
