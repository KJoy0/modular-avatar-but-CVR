#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Repoints serialized object references away from objects a pass is about to destroy.
    ///
    /// Any pass that destroys a GameObject must call this FIRST. Nothing else in the package
    /// repoints references — CVRMAMergeArmaturePass.RemapRenderers only handles a
    /// SkinnedMeshRenderer's bones/rootBone — so without this, everything holding a direct
    /// reference to a removed object is left null: Magica Cloth root bones and collider symmetry
    /// targets, renderer probe anchors, constraints, and MA-CVR's own components.
    ///
    /// Deliberately type-free: it walks SerializedProperties instead of referencing any
    /// third-party type, so the package still compiles when Magica Cloth isn't installed.
    ///
    /// Scope is the avatar being built. A component OUTSIDE the avatar that references one of the
    /// destroyed objects still ends up with a null reference.
    /// </summary>
    internal static class CVRMAReferenceRemapUtil
    {
        /// <summary>Guards against a pathological replacement chain (a → b → c → …).</summary>
        private const int MaxChainHops = 16;

        /// <summary>
        /// Caches, per serialized element type name, whether that type's subtree can hold an object
        /// reference — so huge primitive arrays (Magica's baked pose data) are skipped after being
        /// probed once. Shared across every call in a build.
        /// </summary>
        private static readonly Dictionary<string, bool> ElementTypeHoldsRefs =
            new Dictionary<string, bool>();

        /// <summary>
        /// Rewrites every serialized reference under <paramref name="scanRoot"/> that points at a
        /// doomed object so it points at its replacement instead. Call BEFORE destroying anything.
        /// Returns the number of properties rewritten.
        /// </summary>
        internal static int RemapReferences(
            GameObject scanRoot,
            IReadOnlyDictionary<Transform, Transform> replacements,
            string passLabel,
            bool verbose = false)
        {
            if (scanRoot == null || replacements == null || replacements.Count == 0) return 0;

            var lookup = BuildLookup(replacements, passLabel);
            if (lookup.Count == 0) return 0;

            int rewritten = 0;

            foreach (var component in scanRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue; // missing script

                // A Transform's serialized data includes m_Father and m_Children. Rewriting those
                // would reparent objects without fixing sibling lists — instant hierarchy
                // corruption. Covers RectTransform too, since it derives from Transform.
                if (component is Transform) continue;

                using (var so = new SerializedObject(component))
                {
                    int changed = RemapComponent(so, lookup, component, passLabel, verbose);
                    if (changed == 0) continue;

                    // No undo records: the upload path has no undo group at all, and Manual Bake
                    // registers the whole copy as one created-object undo, so per-property records
                    // would only bloat history and slow the bake.
                    so.ApplyModifiedPropertiesWithoutUndo();
                    rewritten += changed;
                }
            }

            return rewritten;
        }

        /// <summary>
        /// Builds an instance-ID keyed lookup. Instance IDs rather than Object keys, because
        /// UnityEngine.Object's fake-null Equals/GetHashCode make it a poor dictionary key.
        ///
        /// Each doomed Transform contributes TWO entries — itself and its GameObject — mapped to
        /// the matching half of the survivor. Instance IDs are unique, so a reference always
        /// resolves to a replacement of its own type. That matters: objectReferenceValue's setter
        /// does not type-check, and writing a Transform into a GameObject-typed field would
        /// silently corrupt it.
        /// </summary>
        private static Dictionary<int, Object> BuildLookup(
            IReadOnlyDictionary<Transform, Transform> replacements, string passLabel)
        {
            var lookup = new Dictionary<int, Object>(replacements.Count * 2);

            foreach (var pair in replacements)
            {
                var doomed = pair.Key;
                if (doomed == null) continue;

                var survivor = ResolveChain(pair.Value, replacements, doomed, passLabel);
                if (survivor == null || survivor == doomed) continue;

                lookup[doomed.GetInstanceID()] = survivor;
                lookup[doomed.gameObject.GetInstanceID()] = survivor.gameObject;
            }

            return lookup;
        }

        /// <summary>
        /// Follows a → b → c so a rewritten reference never lands on another doomed object.
        /// (MergeArmature cannot produce such a chain — its keys live under the outfit and its
        /// values under the avatar — but this keeps the utility safe for future callers.)
        /// </summary>
        private static Transform ResolveChain(
            Transform survivor,
            IReadOnlyDictionary<Transform, Transform> replacements,
            Transform origin,
            string passLabel)
        {
            var seen = new HashSet<Transform> { origin };
            int hops = 0;

            while (survivor != null && replacements.TryGetValue(survivor, out var next))
            {
                if (!seen.Add(survivor) || ++hops > MaxChainHops)
                {
                    Debug.LogWarning(
                        $"[MA-CVR] {passLabel}: replacement chain for '{origin.name}' is cyclic or " +
                        "too long — references are left pointing at the last resolved target.");
                    break;
                }
                survivor = next;
            }

            return survivor;
        }

        private static int RemapComponent(
            SerializedObject so, Dictionary<int, Object> lookup,
            Component owner, string passLabel, bool verbose)
        {
            int changed = 0;
            var property = so.GetIterator();
            bool enterChildren = true;

            while (property.Next(enterChildren))
            {
                // Default to NOT descending; only a Generic property can open a subtree. That
                // alone stops the walk entering float/int/Vector3/Quaternion/Color/curve leaves.
                enterChildren = false;

                switch (property.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        // m_Script points at a MonoScript and must never be rewritten. A
                        // component's m_GameObject cannot match either: doomed bones carry no
                        // components, which is precisely why they were eliminated.
                        if (property.name == "m_Script") break;

                        var current = property.objectReferenceValue;
                        if (current == null) break;
                        if (!lookup.TryGetValue(current.GetInstanceID(), out var replacement)) break;

                        // Assigned IN PLACE — never reordered, inserted or removed. Magica's baked
                        // transformArray is indexed in parallel with its pose arrays, so the array
                        // layout has to survive untouched.
                        property.objectReferenceValue = replacement;
                        changed++;

                        if (verbose)
                            Debug.Log(
                                $"[MA-CVR] {passLabel}: {owner.GetType().Name} on '{owner.name}' " +
                                $"{property.propertyPath}: '{current.name}' → '{replacement.name}'",
                                owner);
                        break;

                    case SerializedPropertyType.Generic:
                        // Structs, classes, arrays and lists — the only things worth opening.
                        enterChildren = !property.isArray || ArrayMayHoldReferences(property);
                        break;
                }
            }

            return changed;
        }

        /// <summary>
        /// Whether an array's elements could contain an object reference. Magica bakes very large
        /// float3/quaternion arrays that would otherwise dominate the walk, so unfamiliar element
        /// types are probed once and the answer cached. Every uncertainty resolves to "descend",
        /// so the probe can cost time but never correctness.
        /// </summary>
        private static bool ArrayMayHoldReferences(SerializedProperty array)
        {
            var elementType = array.arrayElementType;
            if (string.IsNullOrEmpty(elementType)) return true;   // unknown — look inside
            if (elementType.StartsWith("PPtr<")) return true;     // exactly what we are hunting
            if (array.arraySize == 0) return false;

            if (ElementTypeHoldsRefs.TryGetValue(elementType, out var known)) return known;

            bool holdsRefs = SubtreeHasObjectReference(array.GetArrayElementAtIndex(0));
            ElementTypeHoldsRefs[elementType] = holdsRefs;
            return holdsRefs;
        }

        /// <summary>Walks one array element, bounded by its end property, looking for a reference.</summary>
        private static bool SubtreeHasObjectReference(SerializedProperty element)
        {
            var end = element.GetEndProperty();
            var probe = element.Copy();
            bool enterChildren = true;

            while (probe.Next(enterChildren) && !SerializedProperty.EqualContents(probe, end))
            {
                enterChildren = false;

                if (probe.propertyType == SerializedPropertyType.ObjectReference) return true;
                if (probe.propertyType != SerializedPropertyType.Generic) continue;

                if (!probe.isArray)
                {
                    enterChildren = true;
                    continue;
                }

                var elementType = probe.arrayElementType;
                if (string.IsNullOrEmpty(elementType) || elementType.StartsWith("PPtr<")) return true;
                enterChildren = probe.arraySize > 0;
            }

            return false;
        }
    }
}
#endif
