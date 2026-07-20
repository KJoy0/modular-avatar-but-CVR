#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Live edit-mode preview for the reactive components (ShapeChanger, ObjectToggle,
    /// MaterialSwap, MaterialSetter), mirroring VRC MA's real-time preview.
    ///
    /// A component previews its ON state while:
    ///   - its GameObject is selected,
    ///   - a MA Menu Item driving the same parameter is selected, or
    ///   - its inspector "Preview" pin is enabled.
    ///
    /// Originals are cached before anything is touched and restored when the preview
    /// ends. To guarantee preview state is never persisted, everything is restored
    /// around scene saves (and re-applied after), before play mode, and before
    /// assembly reloads.
    /// </summary>
    [InitializeOnLoad]
    internal static class CVRMAReactivePreview
    {
        private interface IPreviewState
        {
            Component Owner { get; }
            /// <summary>Apply the ON state incrementally (caches originals on first touch).</summary>
            void Sync();
            /// <summary>Put everything back exactly as it was.</summary>
            void Restore();
        }

        private static readonly Dictionary<Component, IPreviewState> _active =
            new Dictionary<Component, IPreviewState>();
        private static readonly HashSet<Component> _pinned = new HashSet<Component>();

        static CVRMAReactivePreview()
        {
            EditorApplication.update += OnUpdate;
            EditorSceneManager.sceneSaving += (scene, path) => RestoreEverything();
            AssemblyReloadEvents.beforeAssemblyReload += RestoreEverything;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode) RestoreEverything();
            };
            EditorApplication.quitting += RestoreEverything;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Restores all previews immediately. MUST be called before the build passes
        /// read scene state, or previewed values would be baked as authored ones.
        /// </summary>
        internal static void RestoreForBuild() => RestoreEverything();

        internal static bool IsPinned(Component c) => _pinned.Contains(c);

        /// <summary>True while the component's preview is currently applied in the scene.</summary>
        internal static bool IsPreviewing(Component c) => _active.ContainsKey(c);

        internal static void SetPinned(Component c, bool pinned)
        {
            if (pinned) _pinned.Add(c);
            else _pinned.Remove(c);
        }

        /// <summary>Inspector helper: draws the preview pin toggle + hint line.</summary>
        internal static void DrawPreviewToggle(Component c)
        {
            EditorGUILayout.Space(4);
            bool pinned = IsPinned(c);
            bool selectedPreview = !pinned && _active.ContainsKey(c);

            bool newPinned = GUILayout.Toggle(pinned,
                pinned ? "■ Preview pinned (click to stop)" : "▶ Pin Preview (editor only)",
                "Button");
            if (newPinned != pinned) SetPinned(c, newPinned);

            EditorGUILayout.LabelField(
                selectedPreview
                    ? "Previewing ON state while selected — deselect to restore."
                    : "The ON state also previews automatically while this object (or its Menu Item) is selected.",
                EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ update loop

        private static void OnUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (_active.Count > 0) RestoreEverything();
                return;
            }

            var desired = ComputeDesiredSet();

            // Restore components that left the preview set (or were destroyed).
            foreach (var key in _active.Keys.ToList())
            {
                if (key == null || !desired.Contains(key))
                {
                    _active[key].Restore();
                    _active.Remove(key);
                }
            }

            // Apply / keep in sync everything in the set.
            foreach (var c in desired)
            {
                if (c == null) continue;
                if (!_active.TryGetValue(c, out var state))
                {
                    state = CreateState(c);
                    if (state == null) continue;
                    _active[c] = state;
                }
                state.Sync();
            }
        }

        private static HashSet<Component> ComputeDesiredSet()
        {
            var desired = new HashSet<Component>();

            _pinned.RemoveWhere(c => c == null);
            foreach (var c in _pinned) desired.Add(c);

            var selected = Selection.activeGameObject;
            if (selected == null) return desired;

            foreach (var c in selected.GetComponents<CVRMAComponent>())
                if (IsReactive(c)) desired.Add(c);

            // Selecting a Toggle/Button menu item previews everything driven by its parameter.
            var item = selected.GetComponent<CVRMAMenuItem>();
            if (item != null &&
                (item.controlType == CVRMAControlType.Toggle || item.controlType == CVRMAControlType.Button))
            {
                var machineName = item.GetEffectiveMachineName();
                var avatar = item.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true);
                var root = avatar != null ? avatar.transform : item.transform.root;

                foreach (var c in root.GetComponentsInChildren<CVRMAComponent>(true))
                    if (IsReactive(c) && GetEffectiveParameter(c) == machineName)
                        desired.Add(c);
            }

            return desired;
        }

        private static bool IsReactive(Component c) =>
            c is CVRMAShapeChanger || c is CVRMAObjectToggle ||
            c is CVRMAMaterialSwap || c is CVRMAMaterialSetter;

        private static string GetEffectiveParameter(Component c)
        {
            switch (c)
            {
                case CVRMAShapeChanger sc:   return sc.GetEffectiveParameter();
                case CVRMAObjectToggle ot:   return ot.GetEffectiveParameter();
                case CVRMAMaterialSwap ms:   return ms.GetEffectiveParameter();
                case CVRMAMaterialSetter st: return st.GetEffectiveParameter();
                default: return null;
            }
        }

        private static IPreviewState CreateState(Component c)
        {
            switch (c)
            {
                case CVRMAShapeChanger sc:   return new ShapeChangerState(sc);
                case CVRMAObjectToggle ot:   return new ObjectToggleState(ot);
                case CVRMAMaterialSwap ms:   return new MaterialSwapState(ms);
                case CVRMAMaterialSetter st: return new MaterialSetterState(st);
                default: return null;
            }
        }

        private static void RestoreEverything()
        {
            foreach (var state in _active.Values)
                state.Restore();
            _active.Clear();
        }

        // ------------------------------------------------------------------ ShapeChanger

        private sealed class ShapeChangerState : IPreviewState
        {
            private readonly CVRMAShapeChanger _changer;
            private readonly Dictionary<(SkinnedMeshRenderer, int), float> _originalWeights =
                new Dictionary<(SkinnedMeshRenderer, int), float>();
            // Delete preview: renderer → (authored mesh, generated cut clone, config hash)
            private readonly Dictionary<SkinnedMeshRenderer, (Mesh original, Mesh clone, int hash)> _meshSwaps =
                new Dictionary<SkinnedMeshRenderer, (Mesh, Mesh, int)>();

            public ShapeChangerState(CVRMAShapeChanger changer) => _changer = changer;
            public Component Owner => _changer;

            public void Sync()
            {
                SyncDeletePreviews();
                SyncSetWeights();
            }

            /// <summary>
            /// Delete entries preview as actual polygon removal: the renderer temporarily
            /// gets a clone of its mesh with the displaced triangles cut out. The clone is
            /// rebuilt only when the configuration changes and never saved.
            /// </summary>
            private void SyncDeletePreviews()
            {
                var deleteByMesh = new Dictionary<SkinnedMeshRenderer, List<string>>();
                foreach (var shape in _changer.shapes)
                {
                    if (shape?.changeType != CVRMAShapeChangeType.Delete) continue;
                    if (shape.targetMesh == null || string.IsNullOrEmpty(shape.shapeName)) continue;
                    if (!deleteByMesh.TryGetValue(shape.targetMesh, out var list))
                        deleteByMesh[shape.targetMesh] = list = new List<string>();
                    list.Add(shape.shapeName);
                }

                foreach (var key in _meshSwaps.Keys.ToList())
                    if (key == null || !deleteByMesh.ContainsKey(key))
                        RestoreSwap(key);

                foreach (var kv in deleteByMesh)
                {
                    var smr = kv.Key;
                    var names = kv.Value;
                    names.Sort();

                    bool hasSwap = _meshSwaps.TryGetValue(smr, out var swap);
                    var original = hasSwap ? swap.original : smr.sharedMesh;
                    if (original == null) continue;

                    int hash = (string.Join("|", names), _changer.displacementThreshold,
                                original.GetInstanceID()).GetHashCode();
                    if (hasSwap && swap.hash == hash) continue;

                    if (hasSwap) RestoreSwap(smr);

                    var matched = CVRMAShapeChangerPass.ComputeDisplacedVertices(
                        original, names, _changer.displacementThreshold, _changer.gameObject.name);
                    if (matched == null) continue;

                    // Cut triangles whose vertices are all displaced (boundary tris stay).
                    var kept = new List<int>[original.subMeshCount];
                    for (int s = 0; s < original.subMeshCount; s++)
                    {
                        var tris = original.GetTriangles(s);
                        kept[s] = new List<int>(tris.Length);
                        for (int t = 0; t < tris.Length; t += 3)
                        {
                            if (matched[tris[t]] && matched[tris[t + 1]] && matched[tris[t + 2]]) continue;
                            kept[s].Add(tris[t]); kept[s].Add(tris[t + 1]); kept[s].Add(tris[t + 2]);
                        }
                    }

                    var clone = CVRMAMeshCutterUtil.BuildCompactedMesh(
                        original, kept, original.name + "_DelPreview");
                    clone.hideFlags = HideFlags.HideAndDontSave;

                    _meshSwaps[smr] = (original, clone, hash);
                    smr.sharedMesh = clone;
                }
            }

            private void SyncSetWeights()
            {
                var desired = new Dictionary<(SkinnedMeshRenderer, int), float>();
                foreach (var shape in _changer.shapes)
                {
                    if (shape?.changeType != CVRMAShapeChangeType.Set) continue;
                    if (shape.targetMesh == null || shape.targetMesh.sharedMesh == null) continue;
                    if (string.IsNullOrEmpty(shape.shapeName)) continue;
                    int idx = shape.targetMesh.sharedMesh.GetBlendShapeIndex(shape.shapeName);
                    if (idx < 0) continue;

                    desired[(shape.targetMesh, idx)] = shape.value;
                }

                // Entries removed from the component while previewing go back to authored values.
                foreach (var key in _originalWeights.Keys.ToList())
                {
                    if (desired.ContainsKey(key)) continue;
                    if (key.Item1 != null) key.Item1.SetBlendShapeWeight(key.Item2, _originalWeights[key]);
                    _originalWeights.Remove(key);
                }

                foreach (var kv in desired)
                {
                    var (smr, idx) = kv.Key;
                    if (!_originalWeights.ContainsKey(kv.Key))
                        _originalWeights[kv.Key] = smr.GetBlendShapeWeight(idx);
                    if (!Mathf.Approximately(smr.GetBlendShapeWeight(idx), kv.Value))
                        smr.SetBlendShapeWeight(idx, kv.Value);
                }
            }

            private void RestoreSwap(SkinnedMeshRenderer smr)
            {
                if (_meshSwaps.TryGetValue(smr, out var swap))
                {
                    if (smr != null && smr.sharedMesh == swap.clone) smr.sharedMesh = swap.original;
                    if (swap.clone != null) Object.DestroyImmediate(swap.clone);
                }
                // Dictionary.Remove works on the reference even when Unity reports it destroyed.
                _meshSwaps.Remove(smr);
            }

            public void Restore()
            {
                foreach (var kv in _originalWeights)
                    if (kv.Key.Item1 != null)
                        kv.Key.Item1.SetBlendShapeWeight(kv.Key.Item2, kv.Value);
                _originalWeights.Clear();

                foreach (var smr in _meshSwaps.Keys.ToList())
                    RestoreSwap(smr);
                _meshSwaps.Clear();
            }
        }

        // ------------------------------------------------------------------ ObjectToggle

        private sealed class ObjectToggleState : IPreviewState
        {
            private readonly CVRMAObjectToggle _toggle;
            private readonly Dictionary<GameObject, bool> _originals = new Dictionary<GameObject, bool>();

            public ObjectToggleState(CVRMAObjectToggle toggle) => _toggle = toggle;
            public Component Owner => _toggle;

            public void Sync()
            {
                var desired = new Dictionary<GameObject, bool>();
                foreach (var obj in _toggle.objects)
                    if (obj?.target != null)
                        desired[obj.target.gameObject] = obj.activeWhenOn;

                foreach (var key in _originals.Keys.ToList())
                {
                    if (desired.ContainsKey(key)) continue;
                    if (key != null) key.SetActive(_originals[key]);
                    _originals.Remove(key);
                }

                foreach (var kv in desired)
                {
                    if (!_originals.ContainsKey(kv.Key))
                        _originals[kv.Key] = kv.Key.activeSelf;
                    if (kv.Key.activeSelf != kv.Value)
                        kv.Key.SetActive(kv.Value);
                }
            }

            public void Restore()
            {
                foreach (var kv in _originals)
                    if (kv.Key != null && kv.Key.activeSelf != kv.Value)
                        kv.Key.SetActive(kv.Value);
                _originals.Clear();
            }
        }

        // ------------------------------------------------------------------ MaterialSwap

        private sealed class MaterialSwapState : IPreviewState
        {
            private readonly CVRMAMaterialSwap _swap;
            private readonly Dictionary<(Renderer, int), Material> _originals =
                new Dictionary<(Renderer, int), Material>();

            public MaterialSwapState(CVRMAMaterialSwap swap) => _swap = swap;
            public Component Owner => _swap;

            private Transform SwapRoot()
            {
                if (_swap.swapRoot != null) return _swap.swapRoot;
                var avatar = _swap.GetComponentInParent<ABI.CCK.Components.CVRAvatar>(true);
                return avatar != null ? avatar.transform : _swap.transform.root;
            }

            public void Sync()
            {
                // Desired is computed against ORIGINAL slot materials (cached when a slot
                // was first swapped), so already-swapped slots keep matching their rule.
                var desired = new Dictionary<(Renderer, int), Material>();
                foreach (var renderer in SwapRoot().GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var original = _originals.TryGetValue((renderer, i), out var cached) ? cached : mats[i];
                        if (original == null) continue;

                        foreach (var rule in _swap.swaps)
                        {
                            if (rule?.from == null || rule.to == null || rule.from != original) continue;
                            desired[(renderer, i)] = rule.to;
                            break;
                        }
                    }
                }

                foreach (var key in _originals.Keys.ToList())
                {
                    if (desired.ContainsKey(key)) continue;
                    SetSlot(key.Item1, key.Item2, _originals[key]);
                    _originals.Remove(key);
                }

                foreach (var kv in desired)
                {
                    var (renderer, slot) = kv.Key;
                    if (renderer == null) continue;
                    if (!_originals.ContainsKey(kv.Key))
                        _originals[kv.Key] = renderer.sharedMaterials[slot];
                    if (renderer.sharedMaterials[slot] != kv.Value)
                        SetSlot(renderer, slot, kv.Value);
                }
            }

            public void Restore()
            {
                foreach (var kv in _originals)
                    SetSlot(kv.Key.Item1, kv.Key.Item2, kv.Value);
                _originals.Clear();
            }
        }

        // ------------------------------------------------------------------ MaterialSetter

        private sealed class MaterialSetterState : IPreviewState
        {
            private readonly CVRMAMaterialSetter _setter;
            private readonly Dictionary<(Renderer, int), Material> _originals =
                new Dictionary<(Renderer, int), Material>();

            public MaterialSetterState(CVRMAMaterialSetter setter) => _setter = setter;
            public Component Owner => _setter;

            public void Sync()
            {
                var desired = new Dictionary<(Renderer, int), Material>();
                foreach (var entry in _setter.entries)
                {
                    if (entry?.targetRenderer == null || entry.material == null) continue;
                    if (entry.materialIndex < 0 ||
                        entry.materialIndex >= entry.targetRenderer.sharedMaterials.Length) continue;
                    desired[(entry.targetRenderer, entry.materialIndex)] = entry.material;
                }

                foreach (var key in _originals.Keys.ToList())
                {
                    if (desired.ContainsKey(key)) continue;
                    SetSlot(key.Item1, key.Item2, _originals[key]);
                    _originals.Remove(key);
                }

                foreach (var kv in desired)
                {
                    var (renderer, slot) = kv.Key;
                    if (renderer == null) continue;
                    if (!_originals.ContainsKey(kv.Key))
                        _originals[kv.Key] = renderer.sharedMaterials[slot];
                    if (renderer.sharedMaterials[slot] != kv.Value)
                        SetSlot(renderer, slot, kv.Value);
                }
            }

            public void Restore()
            {
                foreach (var kv in _originals)
                    SetSlot(kv.Key.Item1, kv.Key.Item2, kv.Value);
                _originals.Clear();
            }
        }

        private static void SetSlot(Renderer renderer, int slot, Material material)
        {
            if (renderer == null) return;
            var mats = renderer.sharedMaterials;
            if (slot < 0 || slot >= mats.Length) return;
            if (mats[slot] == material) return;
            mats[slot] = material;
            renderer.sharedMaterials = mats;
        }
    }
}
#endif
