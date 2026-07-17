#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    [CustomEditor(typeof(CVRMABoneProxy))]
    internal class CVRMABoneProxyEditor : UnityEditor.Editor
    {
        private SerializedProperty _attachmentMode;
        private SerializedProperty _matchScale;
        private SerializedProperty _boneReference;
        private SerializedProperty _subPath;

        private bool _advancedOpen;

        private void OnEnable()
        {
            _attachmentMode = serializedObject.FindProperty("attachmentMode");
            _matchScale     = serializedObject.FindProperty("matchScale");
            _boneReference  = serializedObject.FindProperty("boneReference");
            _subPath        = serializedObject.FindProperty("subPath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("MA Bone Proxy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reparents this object onto a humanoid bone at build time. " +
                "The target is identified by a bone reference, so the same setup works " +
                "on any humanoid avatar regardless of bone naming.",
                MessageType.None);

            EditorGUILayout.Space(4);

            // Convenience: drop a bone Transform to auto-fill bone reference + sub path.
            var proxy = (CVRMABoneProxy)target;
            var animator = proxy.FindAvatarAnimator();

            EditorGUI.BeginChangeCheck();
            var dropped = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Target (drag bone)",
                    "Drag any bone here to auto-fill Bone Reference and Sub Path. Not stored — just a helper."),
                proxy.ResolveTarget(animator), typeof(Transform), true);
            if (EditorGUI.EndChangeCheck() && dropped != null)
                AutoFill(proxy, animator, dropped);

            EditorGUILayout.PropertyField(_attachmentMode, new GUIContent("Attachment Mode"));
            EditorGUILayout.PropertyField(_matchScale,     new GUIContent("Match Scale"));

            EditorGUILayout.Space(2);
            _advancedOpen = EditorGUILayout.Foldout(_advancedOpen, "Advanced", true);
            if (_advancedOpen)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_boneReference, new GUIContent("Bone Reference"));
                EditorGUILayout.PropertyField(_subPath,       new GUIContent("Sub Path"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            // Validation feedback
            if (animator == null || !animator.isHuman)
            {
                if ((HumanBodyBones)_boneReference.enumValueIndex != HumanBodyBones.LastBone)
                    EditorGUILayout.HelpBox(
                        "No humanoid Animator found above this object. Bone references require a humanoid avatar.",
                        MessageType.Warning);
            }
            else if (proxy.ResolveTarget(animator) == null)
            {
                EditorGUILayout.HelpBox(
                    "Target could not be resolved. Check the bone reference and sub path.",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// Given a dropped Transform, finds the nearest humanoid-bone ancestor and stores
        /// it as the bone reference plus the descending sub path. If none is found, stores
        /// LastBone + a path from the avatar root.
        /// </summary>
        private void AutoFill(CVRMABoneProxy proxy, Animator animator, Transform dropped)
        {
            Undo.RecordObject(proxy, "Set Bone Proxy target");

            if (animator != null && animator.isHuman)
            {
                var boneMap = BuildBoneMap(animator);

                var parts = new List<string>();
                var t = dropped;
                while (t != null)
                {
                    if (boneMap.TryGetValue(t, out var bone))
                    {
                        parts.Reverse();
                        proxy.boneReference = bone;
                        proxy.subPath = string.Join("/", parts);
                        EditorUtility.SetDirty(proxy);
                        serializedObject.Update();
                        return;
                    }
                    parts.Add(t.name);
                    if (t == animator.transform) break;
                    t = t.parent;
                }

                // Fall through: no humanoid ancestor — use avatar-root-relative path.
                proxy.boneReference = HumanBodyBones.LastBone;
                proxy.subPath = GetPath(animator.transform, dropped);
            }
            else
            {
                proxy.boneReference = HumanBodyBones.LastBone;
                proxy.subPath = dropped.name;
            }

            EditorUtility.SetDirty(proxy);
            serializedObject.Update();
        }

        private static Dictionary<Transform, HumanBodyBones> BuildBoneMap(Animator animator)
        {
            var map = new Dictionary<Transform, HumanBodyBones>();
            for (HumanBodyBones b = 0; b < HumanBodyBones.LastBone; b++)
            {
                var t = animator.GetBoneTransform(b);
                if (t != null && !map.ContainsKey(t)) map[t] = b;
            }
            return map;
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return current == null ? target.name : string.Join("/", parts);
        }
    }
}
#endif
