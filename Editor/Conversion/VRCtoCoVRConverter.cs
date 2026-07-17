#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ModularAvatarCVR.Editor
{
    /// <summary>
    /// Editor window that converts VRC Modular Avatar components to their
    /// CVR Modular Avatar equivalents on a selected avatar.
    ///
    /// Open via: Tools → Modular Avatar CVR → Convert VRC MA to CVR MA
    /// </summary>
    public class VRCtoCoVRConverter : EditorWindow
    {
        private GameObject _targetAvatar;
        private Vector2 _scrollPos;
        private ConversionReport _lastReport;

        [MenuItem("Tools/Modular Avatar CVR/Convert VRC MA to CVR MA")]
        public static void ShowWindow()
        {
            var win = GetWindow<VRCtoCoVRConverter>("VRC MA → CVR MA");
            win.minSize = new Vector2(420, 540);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("VRC Modular Avatar → CVR Modular Avatar", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Replaces VRC MA components with their CVR MA equivalents.\n\n" +
                "Removed (no CVR equivalent):\n" +
                "  • MA World Fixed Object  (use CVR Props instead)\n" +
                "  • MA World Scale Object  (use CVR Props instead)\n" +
                "  • MA PhysBone Blocker    (use Magica Cloth 2 instead)\n\n" +
                "Converted:\n" +
                "  • MA Merge Armature      →  CVR MA Merge Armature\n" +
                "  • MA Merge Animator      →  CVR MA Merge Animator\n" +
                "  • MA Bone Proxy          →  CVR MA Bone Proxy\n" +
                "  • MA Blendshape Sync     →  CVR MA Blendshape Sync\n" +
                "  • MA Mesh Settings       →  CVR MA Mesh Settings\n" +
                "  • MA Shape Changer       →  CVR MA Shape Changer (AAS Toggle + anim clips)\n" +
                "  • MA Parameters          →  CVR MA Parameters\n" +
                "  • MA Merge Blend Tree    →  CVR MA Merge Blend Tree\n" +
                "  • MA Menu Item           →  CVR MA Menu Item\n" +
                "  • MA Menu Group          →  CVR MA Menu Group\n" +
                "  • MA Menu Installer      →  CVR MA Menu Installer\n" +
                "  • MA Menu Install Target →  CVR MA Menu Install Target\n" +
                "  • MA Object Toggle       →  CVR MA Object Toggle\n" +
                "  • MA Material Swap       →  CVR MA Material Swap\n" +
                "  • MA Material Setter     →  CVR MA Material Setter\n" +
                "  • MA Mesh Cutter              →  CVR MA Mesh Cutter (reference only — no CVR equivalent)\n" +
                "  • MA Visible Head Accessory   →  CVR MA Visible Head Accessory (FPRExclusion)\n\n" +
                "Avatar dynamics:\n" +
                "  • VRC Constraints        →  Unity built-in constraints (Position/Rotation/\n" +
                "                              Scale/Parent/Aim/LookAt — supported natively by CVR)\n" +
                "  • VRC Contact Sender     →  NAK Contact Sender      (needs NAK.Contacts)\n" +
                "  • VRC Contact Receiver   →  NAK Contact Receiver + ContactAnimator",
                MessageType.Info);

            EditorGUILayout.Space(8);

            if (!IsVRCMAPresent())
                EditorGUILayout.HelpBox(
                    "VRC Modular Avatar is not installed. This window is only needed for conversion — " +
                    "CVR MA components work standalone without it.",
                    MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                _targetAvatar = (GameObject)EditorGUILayout.ObjectField(
                    "Avatar Root", _targetAvatar, typeof(GameObject), true);
                if (GUILayout.Button("Use Selection", GUILayout.Width(100)))
                    _targetAvatar = Selection.activeGameObject;
            }

            // Auto-fill from selection if empty
            if (_targetAvatar == null && Selection.activeGameObject != null)
                _targetAvatar = Selection.activeGameObject;

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_targetAvatar == null))
            {
                if (GUILayout.Button("Scan for VRC MA Components", GUILayout.Height(28)))
                    _lastReport = Scan(_targetAvatar);
            }

            if (_lastReport != null)
            {
                EditorGUILayout.Space(8);
                DrawReport(_lastReport);

                EditorGUILayout.Space(8);
                using (new EditorGUI.DisabledScope(!_lastReport.HasConvertible))
                {
                    if (GUILayout.Button("Convert Now", GUILayout.Height(32)))
                    {
                        Undo.IncrementCurrentGroup();
                        Undo.SetCurrentGroupName("Convert VRC MA to CVR MA");
                        int group = Undo.GetCurrentGroup();

                        Convert(_targetAvatar, _lastReport);

                        Undo.CollapseUndoOperations(group);
                        _lastReport = Scan(_targetAvatar);
                        Debug.Log("[MA-CVR] Conversion complete.");
                    }
                }
            }
        }

        private void DrawReport(ConversionReport report)
        {
            EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(240));

            foreach (var item in report.Items)
            {
                var color = item.Action switch
                {
                    ConversionAction.Convert => new Color(0.4f, 1f, 0.4f),
                    ConversionAction.Remove  => new Color(1f, 0.55f, 0.2f),
                    ConversionAction.Manual  => new Color(1f, 0.95f, 0.3f),
                    _                        => Color.white
                };
                var prev = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(
                    $"[{item.Action}] {item.ComponentType}  on  '{item.ObjectName}'",
                    EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }

            if (report.Items.Count == 0)
                EditorGUILayout.LabelField("No VRC MA components found.", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                $"Convert: {report.ConvertCount}    Remove: {report.RemoveCount}    Manual: {report.ManualCount}",
                EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ Scan

        private static ConversionReport Scan(GameObject root)
        {
            var report = new ConversionReport();
            if (root == null) return report;

            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;

                switch (typeName)
                {
                    case "ModularAvatarMergeArmature":
                    case "ModularAvatarMergeAnimator":
                    case "ModularAvatarBoneProxy":
                    case "ModularAvatarBlendshapeSync":
                    case "ModularAvatarMeshSettings":
                    case "ModularAvatarShapeChanger":
                    case "ModularAvatarParameters":
                    case "ModularAvatarMergeBlendTree":
                    case "ModularAvatarMenuItem":
                    case "ModularAvatarMenuGroup":
                    case "ModularAvatarMenuInstaller":
                    case "ModularAvatarMenuInstallTarget":
                    case "ModularAvatarObjectToggle":
                    case "ModularAvatarMaterialSwap":
                    case "ModularAvatarMaterialSetter":
                    case "ModularAvatarMeshCutter":
                    case "ModularAvatarVisibleHeadAccessory":
                    case "ModularAvatarScaleAdjuster":
                    case "ModularAvatarReplaceObject":
                    case "ModularAvatarRemoveVertexColor":
                    case "ModularAvatarFloorAdjuster":
                        report.Add(typeName, comp.gameObject.name, ConversionAction.Convert); break;

                    case "ModularAvatarWorldFixedObject":
                    case "ModularAvatarWorldScaleObject":
                    case "ModularAvatarPBBlocker":
                        report.Add(typeName, comp.gameObject.name, ConversionAction.Remove); break;

                    case "ModularAvatarConvertConstraints":
                    case "ModularAvatarGlobalCollider":
                    case "ModularAvatarMaterialChanger":
                    case "ModularAvatarReactionDebugger":
                        report.Add(typeName, comp.gameObject.name, ConversionAction.Manual); break;

                    default:
                        // VRC constraints → Unity built-in constraints (always available).
                        if (VRCConstraintToUnity.IsVRCConstraint(typeName))
                        {
                            report.Add(typeName, comp.gameObject.name, ConversionAction.Convert);
                        }
                        // VRC contacts → NAK contacts (only if NAK.Contacts is installed).
                        else if (VRCContactToNAK.IsVRCContact(typeName))
                        {
                            report.Add(typeName, comp.gameObject.name,
                                VRCContactToNAK.NAKAvailable
                                    ? ConversionAction.Convert
                                    : ConversionAction.Manual);
                        }
                        break;
                }
            }

            return report;
        }

        // ------------------------------------------------------------------ Convert

        private static void Convert(GameObject root, ConversionReport report)
        {
            _aorFailures = 0;
            int converted = 0;

            // Sanity check: warn if the user picked a child of the actual avatar root.
            if (root.transform.parent != null)
            {
                bool parentIsAvatar = root.transform.parent.GetComponentInParent<ABI.CCK.Components.CVRAvatar>() != null
                                   || HasComponentByName(root.transform.parent.gameObject, "VRCAvatarDescriptor");
                if (parentIsAvatar)
                    Debug.LogWarning(
                        $"[MA-CVR] Converter: '{root.name}' has a parent that looks like an avatar root. " +
                        $"AvatarObjectReference paths are stored relative to the avatar root, so converting " +
                        $"a child object may cause every mesh/transform reference to come up empty. " +
                        $"Drag the avatar's TOP transform into the Avatar Root field.");
            }

            // Snapshot all components upfront — we'll destroy the originals as we go
            var allComponents = root.GetComponentsInChildren<Component>(true).ToArray();

            foreach (var comp in allComponents)
            {
                if (comp == null) continue;

                switch (comp.GetType().Name)
                {
                    case "ModularAvatarMergeArmature":    ConvertMergeArmature(comp, root); break;
                    case "ModularAvatarMergeAnimator":    ConvertMergeAnimator(comp); break;
                    case "ModularAvatarBoneProxy":        ConvertBoneProxy(comp, root); break;
                    case "ModularAvatarBlendshapeSync":   ConvertBlendshapeSync(comp, root); break;
                    case "ModularAvatarMeshSettings":     ConvertMeshSettings(comp, root); break;
                    case "ModularAvatarShapeChanger":     ConvertShapeChanger(comp, root); break;
                    case "ModularAvatarParameters":       ConvertParameters(comp); break;
                    case "ModularAvatarMergeBlendTree":   ConvertMergeBlendTree(comp, root); break;
                    case "ModularAvatarMenuItem":         ConvertMenuItem(comp, root); break;
                    case "ModularAvatarMenuGroup":        SimpleConvert<CVRMAMenuGroup>(comp); break;
                    case "ModularAvatarMenuInstaller":    SimpleConvert<CVRMAMenuInstaller>(comp); break;
                    case "ModularAvatarMenuInstallTarget":SimpleConvert<CVRMAMenuInstallTarget>(comp); break;
                    case "ModularAvatarObjectToggle":     ConvertObjectToggle(comp, root); break;
                    case "ModularAvatarMaterialSwap":     ConvertMaterialSwap(comp, root); break;
                    case "ModularAvatarMaterialSetter":   ConvertMaterialSetter(comp, root); break;
                    case "ModularAvatarMeshCutter":              ConvertMeshCutter(comp, root); break;
                    case "ModularAvatarVisibleHeadAccessory":    SimpleConvert<CVRMAVisibleHeadAccessory>(comp); break;
                    case "ModularAvatarScaleAdjuster":           ConvertScaleAdjuster(comp); break;
                    case "ModularAvatarReplaceObject":           ConvertReplaceObject(comp, root); break;
                    case "ModularAvatarRemoveVertexColor":       ConvertRemoveVertexColor(comp); break;
                    case "ModularAvatarFloorAdjuster":           SimpleConvert<CVRMAFloorAdjuster>(comp); break;
                    case "ModularAvatarWorldFixedObject":
                    case "ModularAvatarWorldScaleObject":
                    case "ModularAvatarPBBlocker":
                        Undo.DestroyObjectImmediate(comp); break;
                    default:
                        // VRC constraints → Unity constraints; VRC contacts → NAK contacts.
                        if (VRCConstraintToUnity.TryConvert(comp)) break;
                        if (VRCContactToNAK.TryConvert(comp, root)) break;
                        continue;
                }
                converted++;
            }

            if (_aorFailures > 0)
                Debug.LogWarning(
                    $"[MA-CVR] Converter: finished with {_aorFailures} unresolved AvatarObjectReference path(s). " +
                    $"See the warnings above to identify which bindings need manual cleanup.");
            else
                Debug.Log($"[MA-CVR] Converter: {converted} component(s) converted with no resolution warnings.");
        }

        private static bool HasComponentByName(GameObject go, string typeName)
        {
            foreach (var c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == typeName) return true;
            return false;
        }

        // ------------------------------------------------------------------ Per-type converters

        private static void ConvertMergeArmature(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMergeArmature");
            var dst = Undo.AddComponent<CVRMAMergeArmature>(src.gameObject);

            var so = new SerializedObject(src);
            dst.prefix = so.FindProperty("prefix")?.stringValue ?? "";
            dst.suffix = so.FindProperty("suffix")?.stringValue ?? "";
            dst.mangleNames = so.FindProperty("mangleNames")?.boolValue ?? true;

            // Resolve AvatarObjectReference → Transform
            var targetGO = ResolveAOR(so.FindProperty("mergeTarget"), avatarRoot.transform,
                $"MergeArmature on '{src.gameObject.name}'.mergeTarget");
            if (targetGO != null)
                dst.mergeTarget = targetGO.transform;
            else
                Debug.LogWarning($"[MA-CVR] MergeArmature on '{src.gameObject.name}': mergeTarget was empty in the source.");

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMergeAnimator(Component src)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMergeAnimator");
            var dst = Undo.AddComponent<CVRMAMergeAnimator>(src.gameObject);

            var so = new SerializedObject(src);
            dst.animator = so.FindProperty("animator")?.objectReferenceValue as RuntimeAnimatorController;
            dst.deleteAttachedAnimator = so.FindProperty("deleteAttachedAnimator")?.boolValue ?? true;
            dst.matchAvatarWriteDefaults = so.FindProperty("matchAvatarWriteDefaults")?.boolValue ?? true;
            dst.layerPriority = so.FindProperty("layerPriority")?.intValue ?? 0;

            // pathMode: 0 = Relative, 1 = Absolute in MA
            var pathModeVal = so.FindProperty("pathMode")?.enumValueIndex ?? 0;
            dst.pathMode = pathModeVal == 0 ? CVRMAPathMode.Relative : CVRMAPathMode.Absolute;

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertBoneProxy(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMABoneProxy");
            var dst = Undo.AddComponent<CVRMABoneProxy>(src.gameObject);

            var so = new SerializedObject(src);

            // CVR MA now uses the SAME model as VRC MA — HumanBodyBones reference + subPath —
            // so this is a direct field copy. HumanBodyBones int values are identical
            // (both UnityEngine.HumanBodyBones), and LastBone means "resolve from avatar root".
            dst.subPath       = so.FindProperty("subPath")?.stringValue ?? "";
            dst.matchScale    = so.FindProperty("matchScale")?.boolValue ?? false;
            dst.boneReference = (HumanBodyBones)(so.FindProperty("boneReference")?.intValue
                                                 ?? (int)HumanBodyBones.LastBone);

            // attachmentMode enum: Unset=0, AsChildAtRoot=1, AsChildKeepWorldPose=2,
            //   AsChildKeepRotation=3, AsChildKeepPosition=4
            var attachVal = so.FindProperty("attachmentMode")?.enumValueIndex ?? 0;
            dst.attachmentMode = attachVal switch
            {
                2 => CVRMABoneProxyAttachment.AsChildKeepWorldPose,
                3 => CVRMABoneProxyAttachment.AsChildKeepRotation,
                4 => CVRMABoneProxyAttachment.AsChildKeepPosition,
                _ => CVRMABoneProxyAttachment.AsChildAtRoot
            };

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertBlendshapeSync(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMABlendshapeSync");
            var dst = Undo.AddComponent<CVRMABlendshapeSync>(src.gameObject);

            // The local mesh is this component's own SkinnedMeshRenderer
            var localSMR = src.GetComponent<SkinnedMeshRenderer>();

            var so = new SerializedObject(src);
            var bindingsProp = so.FindProperty("Bindings");
            if (bindingsProp != null && bindingsProp.isArray)
            {
                for (int i = 0; i < bindingsProp.arraySize; i++)
                {
                    var elem = bindingsProp.GetArrayElementAtIndex(i);

                    // ReferenceMesh is an AvatarObjectReference — the REMOTE mesh
                    var refMeshGO = ResolveAOR(elem.FindPropertyRelative("ReferenceMesh"), avatarRoot.transform,
                        $"BlendshapeSync on '{src.gameObject.name}'.Bindings[{i}].ReferenceMesh");
                    var remoteSMR = refMeshGO?.GetComponent<SkinnedMeshRenderer>();
                    if (refMeshGO != null && remoteSMR == null)
                        Debug.LogWarning(
                            $"[MA-CVR] BlendshapeSync on '{src.gameObject.name}' binding {i}: " +
                            $"resolved target '{refMeshGO.name}' has no SkinnedMeshRenderer.");

                    var remoteBlendshape = elem.FindPropertyRelative("Blendshape")?.stringValue ?? "";
                    var localBlendshape  = elem.FindPropertyRelative("LocalBlendshape")?.stringValue ?? "";
                    if (string.IsNullOrEmpty(localBlendshape)) localBlendshape = remoteBlendshape;

                    dst.blendshapes.Add(new CVRMABlendshapeSyncEntry
                    {
                        localMesh        = localSMR,
                        localBlendshape  = localBlendshape,
                        targetMesh       = remoteSMR,
                        targetBlendshape = remoteBlendshape
                    });
                }
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMeshSettings(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMeshSettings");
            var dst = Undo.AddComponent<CVRMAMeshSettings>(src.gameObject);

            var so = new SerializedObject(src);

            // InheritProbeAnchor enum maps directly (Inherit=0, Set=1, DontSet=2, SetOrInherit=3)
            dst.inheritProbeAnchor = (CVRMAInheritMode)(so.FindProperty("InheritProbeAnchor")?.enumValueIndex ?? 0);
            dst.inheritBounds      = (CVRMAInheritMode)(so.FindProperty("InheritBounds")?.enumValueIndex ?? 0);

            var probeAnchorGO = ResolveAOR(so.FindProperty("ProbeAnchor"), avatarRoot.transform,
                $"MeshSettings on '{src.gameObject.name}'.ProbeAnchor");
            if (probeAnchorGO != null) dst.probeAnchor = probeAnchorGO.transform;

            var rootBoneGO = ResolveAOR(so.FindProperty("RootBone"), avatarRoot.transform,
                $"MeshSettings on '{src.gameObject.name}'.RootBone");
            if (rootBoneGO != null) dst.rootBone = rootBoneGO.transform;

            var boundsProp = so.FindProperty("Bounds");
            if (boundsProp != null)
            {
                dst.localBounds = new Bounds(
                    boundsProp.FindPropertyRelative("m_Center").vector3Value,
                    boundsProp.FindPropertyRelative("m_Extent").vector3Value * 2f
                );
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMenuItem(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMenuItem");
            var dst = Undo.AddComponent<CVRMAMenuItem>(src.gameObject);

            var so = new SerializedObject(src);
            dst.label    = so.FindProperty("label")?.stringValue ?? "";
            dst.isSynced = so.FindProperty("isSynced")?.boolValue ?? true;
            dst.isSaved  = so.FindProperty("isSaved")?.boolValue ?? true;

            // PortableMenuControl data lives either on the VRC Control or portable fields
            // In non-VRC builds, the fields are directly on the component under Control
            ReadMenuItemControl(so, dst);

            // MA toggles can drive bool, float, or int parameters. The declared type
            // lives in the avatar's VRC expression parameters when available.
            if (dst.controlType == CVRMAControlType.Toggle || dst.controlType == CVRMAControlType.Button)
                dst.parameterType = InferParameterType(avatarRoot, dst.parameter, dst.value);

            Undo.DestroyObjectImmediate(src);
        }

        /// <summary>
        /// Determines a toggle's parameter type from the VRC avatar's expression
        /// parameters (reflection-free, works without the VRC SDK). Falls back to a
        /// heuristic: writing a value other than 0/1 implies an Int radio group.
        /// </summary>
        private static CVRMAMenuParameterType InferParameterType(GameObject avatarRoot, string paramName, float value)
        {
            if (!string.IsNullOrEmpty(paramName))
            {
                foreach (var comp in avatarRoot.GetComponents<Component>())
                {
                    if (comp == null || comp.GetType().Name != "VRCAvatarDescriptor") continue;

                    var expressionParams = new SerializedObject(comp)
                        .FindProperty("expressionParameters")?.objectReferenceValue;
                    if (expressionParams == null) break;

                    var listProp = new SerializedObject(expressionParams).FindProperty("parameters");
                    if (listProp == null || !listProp.isArray) break;

                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        var p = listProp.GetArrayElementAtIndex(i);
                        if (p.FindPropertyRelative("name")?.stringValue != paramName) continue;

                        // VRCExpressionParameters.ValueType: Int=0, Float=1, Bool=2
                        switch (p.FindPropertyRelative("valueType")?.enumValueIndex ?? 2)
                        {
                            case 0:  return CVRMAMenuParameterType.Int;
                            case 1:  return CVRMAMenuParameterType.Float;
                            default: return CVRMAMenuParameterType.Bool;
                        }
                    }
                    break;
                }
            }

            return Mathf.Approximately(value, 1f) || Mathf.Approximately(value, 0f)
                ? CVRMAMenuParameterType.Bool
                : CVRMAMenuParameterType.Int;
        }

        private static void ReadMenuItemControl(SerializedObject so, CVRMAMenuItem dst)
        {
            // Non-VRC (portable) path: fields directly serialized on the component
            var controlProp = so.FindProperty("Control");
            if (controlProp == null) return;

            // PortableControlType enum values: Button=101, Toggle=102, SubMenu=103,
            //   TwoAxisPuppet=201, FourAxisPuppet=202, RadialPuppet=203
            var typeProp = controlProp.FindPropertyRelative("type");
            if (typeProp == null)
            {
                // VRC SDK present: Control is VRCExpressionsMenu.Control, type is also "type"
                typeProp = controlProp.FindPropertyRelative("m_Type");
            }

            if (typeProp != null)
            {
                int typeVal = typeProp.intValue;
                dst.controlType = typeVal switch
                {
                    101 => CVRMAControlType.Button,
                    102 => CVRMAControlType.Toggle,
                    103 => CVRMAControlType.SubMenu,
                    201 => CVRMAControlType.TwoAxisPuppet,
                    202 => CVRMAControlType.FourAxisPuppet,
                    203 => CVRMAControlType.RadialPuppet,
                    // VRC SDK enum ints: Toggle=1, Button=2 (VRCExpressionsMenu.Control.ControlType)
                    1   => CVRMAControlType.Toggle,
                    2   => CVRMAControlType.Button,
                    3   => CVRMAControlType.SubMenu,
                    4   => CVRMAControlType.RadialPuppet,
                    5   => CVRMAControlType.TwoAxisPuppet,
                    6   => CVRMAControlType.FourAxisPuppet,
                    _   => CVRMAControlType.Toggle
                };
            }

            var paramProp = controlProp.FindPropertyRelative("parameter")
                         ?? controlProp.FindPropertyRelative("m_Parameter");
            if (paramProp != null)
                dst.parameter = paramProp.FindPropertyRelative("name")?.stringValue ?? "";

            var valueProp = controlProp.FindPropertyRelative("value")
                          ?? controlProp.FindPropertyRelative("m_Value");
            if (valueProp != null) dst.value = valueProp.floatValue;

            // SubParameters → joystickBaseName (use first sub-param, strip -x/-y suffix)
            var subParamsProp = controlProp.FindPropertyRelative("subParameters")
                              ?? controlProp.FindPropertyRelative("m_SubParameters");
            if (subParamsProp != null && subParamsProp.isArray && subParamsProp.arraySize > 0)
            {
                var firstName = subParamsProp.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("name")?.stringValue ?? "";
                // Strip common -x/-y/-h/-v suffixes to get base name
                foreach (var suffix in new[] { "-x", "-y", "-h", "-v", "_x", "_y" })
                {
                    if (firstName.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        firstName = firstName.Substring(0, firstName.Length - suffix.Length);
                        break;
                    }
                }
                dst.joystickBaseName = firstName;
            }
        }

        private static void ConvertShapeChanger(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAShapeChanger");
            var dst = Undo.AddComponent<CVRMAShapeChanger>(src.gameObject);

            var so = new SerializedObject(src);
            dst.threshold = so.FindProperty("m_threshold")?.floatValue ?? 0.5f;

            // ReactiveComponent condition: read parameter from parent context if available
            // MA stores the "condition" on the parent's ObjectToggle or as a direct field
            // Best-effort: look for a parameter name on the parent MenuItem or ObjectToggle
            dst.parameter = TryFindReactiveParameter(src.gameObject) ?? "";
            if (string.IsNullOrEmpty(dst.parameter))
                Debug.LogWarning(
                    $"[MA-CVR] ShapeChanger on '{src.gameObject.name}': no parameter found. " +
                    $"In VRC MA, Shape Changer is reactive — its trigger comes from a parent " +
                    $"Menu Item or Object Toggle. CVR MA needs an explicit Parameter; fill it " +
                    $"in manually to match the driving parameter.");

            var shapesProp = so.FindProperty("m_shapes") ?? so.FindProperty("Shapes");
            if (shapesProp != null && shapesProp.isArray)
            {
                for (int i = 0; i < shapesProp.arraySize; i++)
                {
                    var elem = shapesProp.GetArrayElementAtIndex(i);
                    var targetGO = ResolveAOR(elem.FindPropertyRelative("Object"), avatarRoot.transform,
                        $"ShapeChanger on '{src.gameObject.name}'.Shapes[{i}].Object");
                    var smr = targetGO?.GetComponent<SkinnedMeshRenderer>();
                    if (targetGO != null && smr == null)
                        Debug.LogWarning(
                            $"[MA-CVR] ShapeChanger on '{src.gameObject.name}' shape {i}: " +
                            $"resolved target '{targetGO.name}' has no SkinnedMeshRenderer.");

                    var changeTypeVal = elem.FindPropertyRelative("ChangeType")?.enumValueIndex ?? 1;
                    dst.shapes.Add(new CVRMAChangedShape
                    {
                        targetMesh  = smr,
                        shapeName   = elem.FindPropertyRelative("ShapeName")?.stringValue ?? "",
                        changeType  = changeTypeVal == 0
                            ? CVRMAShapeChangeType.Delete
                            : CVRMAShapeChangeType.Set,
                        value       = elem.FindPropertyRelative("Value")?.floatValue ?? 100f
                    });
                }
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertParameters(Component src)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAParameters");
            var dst = Undo.AddComponent<CVRMAParameters>(src.gameObject);

            var so = new SerializedObject(src);
            var paramsProp = so.FindProperty("parameters");
            if (paramsProp != null && paramsProp.isArray)
            {
                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    var elem = paramsProp.GetArrayElementAtIndex(i);
                    // ParameterSyncType: NotSynced=0, Int=1, Float=2, Bool=3
                    var syncVal = elem.FindPropertyRelative("syncType")?.enumValueIndex ?? 0;
                    dst.parameters.Add(new CVRMAParameterConfig
                    {
                        nameOrPrefix  = elem.FindPropertyRelative("nameOrPrefix")?.stringValue ?? "",
                        remapTo       = elem.FindPropertyRelative("remapTo")?.stringValue ?? "",
                        syncType      = (CVRMAParameterSyncType)syncVal,
                        defaultValue  = elem.FindPropertyRelative("defaultValue")?.floatValue ?? 0f,
                        saved         = elem.FindPropertyRelative("saved")?.boolValue ?? true,
                        localOnly     = elem.FindPropertyRelative("localOnly")?.boolValue ?? false,
                        isPrefix      = elem.FindPropertyRelative("isPrefix")?.boolValue ?? false
                    });
                }
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMergeBlendTree(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMergeBlendTree");
            var dst = Undo.AddComponent<CVRMAMergeBlendTree>(src.gameObject);

            var so = new SerializedObject(src);
            // MA stores it as Object BlendTree (obsolete) but also as Motion via interface
            dst.motion = so.FindProperty("BlendTree")?.objectReferenceValue as Motion;

            var pathModeVal = so.FindProperty("PathMode")?.enumValueIndex ?? 0;
            dst.pathMode = pathModeVal == 0 ? CVRMAPathMode.Relative : CVRMAPathMode.Absolute;

            dst.layerName = src.gameObject.name;

            Undo.DestroyObjectImmediate(src);
        }

        /// <summary>
        /// Walks up the hierarchy to find a MA MenuItem or ObjectToggle that drives
        /// a parameter, which is used as the default parameter for ShapeChanger.
        /// </summary>
        private static string TryFindReactiveParameter(GameObject go)
        {
            var t = go.transform.parent;
            while (t != null)
            {
                foreach (var comp in t.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName == "ModularAvatarMenuItem" || typeName == "CVRMAMenuItem")
                    {
                        var so = new SerializedObject(comp);
                        // try portable parameter path
                        var p = so.FindProperty("Control")?.FindPropertyRelative("parameter")
                                  ?.FindPropertyRelative("name")?.stringValue;
                        if (!string.IsNullOrEmpty(p)) return p;

                        p = so.FindProperty("parameter")?.stringValue;
                        if (!string.IsNullOrEmpty(p)) return p;
                    }
                }
                t = t.parent;
            }
            return null;
        }

        private static void ConvertObjectToggle(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAObjectToggle");
            var dst = Undo.AddComponent<CVRMAObjectToggle>(src.gameObject);

            var so = new SerializedObject(src);
            dst.label        = so.FindProperty("label")?.stringValue ?? "";
            dst.parameter    = so.FindProperty("parameter")?.stringValue ?? "";
            dst.defaultValue = so.FindProperty("defaultValue")?.boolValue ?? false;

            var objectsProp = so.FindProperty("Objects") ?? so.FindProperty("objects");
            if (objectsProp != null && objectsProp.isArray)
            {
                for (int i = 0; i < objectsProp.arraySize; i++)
                {
                    var elem = objectsProp.GetArrayElementAtIndex(i);
                    var targetGO = ResolveAOR(elem.FindPropertyRelative("Object"), avatarRoot.transform,
                        $"ObjectToggle on '{src.gameObject.name}'.Objects[{i}].Object");
                    if (targetGO == null) continue;
                    dst.objects.Add(new CVRMAToggledObject
                    {
                        target      = targetGO.transform,
                        activeWhenOn = elem.FindPropertyRelative("Active")?.boolValue ?? true
                    });
                }
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMaterialSwap(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMaterialSwap");
            var dst = Undo.AddComponent<CVRMAMaterialSwap>(src.gameObject);

            var so = new SerializedObject(src);
            dst.label        = so.FindProperty("label")?.stringValue ?? "";
            dst.parameter    = so.FindProperty("parameter")?.stringValue ?? "";
            dst.defaultValue = so.FindProperty("defaultValue")?.boolValue ?? false;

            var swapRootGO = ResolveAOR(so.FindProperty("swapRoot"), avatarRoot.transform,
                $"MaterialSwap on '{src.gameObject.name}'.swapRoot");
            if (swapRootGO != null) dst.swapRoot = swapRootGO.transform;

            var swapsProp = so.FindProperty("m_swaps") ?? so.FindProperty("swaps");
            if (swapsProp != null && swapsProp.isArray)
            {
                for (int i = 0; i < swapsProp.arraySize; i++)
                {
                    var elem = swapsProp.GetArrayElementAtIndex(i);
                    dst.swaps.Add(new CVRMAMatSwap
                    {
                        from = elem.FindPropertyRelative("From")?.objectReferenceValue as Material
                              ?? elem.FindPropertyRelative("from")?.objectReferenceValue as Material,
                        to   = elem.FindPropertyRelative("To")?.objectReferenceValue as Material
                              ?? elem.FindPropertyRelative("to")?.objectReferenceValue as Material
                    });
                }
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMaterialSetter(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMaterialSetter");
            var dst = Undo.AddComponent<CVRMAMaterialSetter>(src.gameObject);

            var so = new SerializedObject(src);
            dst.label        = so.FindProperty("label")?.stringValue ?? "";
            dst.parameter    = so.FindProperty("parameter")?.stringValue ?? "";
            dst.defaultValue = so.FindProperty("defaultValue")?.boolValue ?? false;

            var entriesProp = so.FindProperty("m_entries") ?? so.FindProperty("entries");
            if (entriesProp != null && entriesProp.isArray)
            {
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem = entriesProp.GetArrayElementAtIndex(i);
                    var rendererGO = ResolveAOR(elem.FindPropertyRelative("Object"), avatarRoot.transform,
                        $"MaterialSetter on '{src.gameObject.name}'.entries[{i}].Object");
                    var renderer = rendererGO?.GetComponent<Renderer>();
                    if (rendererGO != null && renderer == null)
                        Debug.LogWarning(
                            $"[MA-CVR] MaterialSetter on '{src.gameObject.name}' entry {i}: " +
                            $"resolved target '{rendererGO.name}' has no Renderer.");

                    dst.entries.Add(new CVRMAMaterialSetEntry
                    {
                        targetRenderer  = renderer,
                        materialIndex   = elem.FindPropertyRelative("Slot")?.intValue ?? 0,
                        material        = elem.FindPropertyRelative("Material")?.objectReferenceValue as Material
                    });
                }
            }

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertMeshCutter(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAMeshCutter");
            var dst = Undo.AddComponent<CVRMAMeshCutter>(src.gameObject);

            var so = new SerializedObject(src);

            // MA stores the target as an AvatarObjectReference in m_object.
            var targetGO = ResolveAOR(so.FindProperty("m_object"), avatarRoot.transform,
                $"MeshCutter on '{src.gameObject.name}'.Object");
            dst.targetMesh = targetGO != null
                ? targetGO.GetComponent<SkinnedMeshRenderer>()
                : src.GetComponent<SkinnedMeshRenderer>();
            if (dst.targetMesh == null)
                Debug.LogWarning($"[MA-CVR] MeshCutter on '{src.gameObject.name}': target mesh unresolved — assign it manually.");

            // Same member order as MA: VertexUnion=0, VertexIntersection=1.
            var modeProp = so.FindProperty("m_multiMode");
            if (modeProp != null)
                dst.mode = modeProp.enumValueIndex == 1
                    ? CVRMAMeshCutterMode.VertexIntersection
                    : CVRMAMeshCutterMode.VertexUnion;

            // MA Mesh Cutter is reactive: under a Menu Item it toggles instead of deleting.
            if (HasMenuItemAbove(src.transform))
                dst.action = CVRMAMeshCutterAction.Toggle;

            ConvertVertexFilters(src, avatarRoot, dst);

            Undo.DestroyObjectImmediate(src);
        }

        private static bool HasMenuItemAbove(Transform t)
        {
            for (var current = t; current != null; current = current.parent)
            {
                if (current.GetComponent<CVRMAMenuItem>() != null) return true;
                foreach (var c in current.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "ModularAvatarMenuItem") return true;
            }
            return false;
        }

        /// <summary>
        /// Converts MA's component-based vertex filters (VertexFilterBy*Component,
        /// attached next to the Mesh Cutter) onto CVRMAVertexFilter entries.
        /// Unmappable filters are reported so the user can rebuild them manually.
        /// </summary>
        private static void ConvertVertexFilters(Component src, GameObject avatarRoot, CVRMAMeshCutter dst)
        {
            foreach (var sibling in src.GetComponents<Component>())
            {
                if (sibling == null || sibling == src) continue;
                var typeName = sibling.GetType().Name;
                if (!typeName.StartsWith("VertexFilter")) continue;

                var so = new SerializedObject(sibling);
                var filter = new CVRMAVertexFilter();

                // MA: AnyVertex=0, AllVertices=1, Centroid=2 — same order as ours.
                var selProp = so.FindProperty("m_selectionMode");
                if (selProp != null)
                    filter.selectionMode = (CVRMATriangleSelectMode)Mathf.Clamp(selProp.enumValueIndex, 0, 2);

                bool mapped = true;
                if (typeName.Contains("ByBone"))
                {
                    filter.type = CVRMAVertexFilterType.ByBone;
                    var boneGO = ResolveAOR(so.FindProperty("m_bone"), avatarRoot.transform,
                        $"MeshCutter filter on '{src.gameObject.name}'.bone");
                    filter.bone = boneGO != null ? boneGO.transform : null;
                    filter.includeChildBones = false; // MA matches only the named bone
                    var thresholdProp = so.FindProperty("m_threshold");
                    if (thresholdProp != null) filter.minBoneWeight = thresholdProp.floatValue;
                    if (filter.bone == null)
                        Debug.LogWarning($"[MA-CVR] MeshCutter on '{src.gameObject.name}': ByBone filter bone unresolved — assign it manually.");
                }
                else if (typeName.Contains("ByShape"))
                {
                    filter.type = CVRMAVertexFilterType.ByBlendshape;
                    var shapesProp = so.FindProperty("m_shapes");
                    if (shapesProp != null && shapesProp.isArray)
                        for (int i = 0; i < shapesProp.arraySize; i++)
                            filter.blendshapeNames.Add(shapesProp.GetArrayElementAtIndex(i).stringValue);
                    var thresholdProp = so.FindProperty("m_threshold");
                    if (thresholdProp != null) filter.minShapeDelta = thresholdProp.floatValue;
                }
                else if (typeName.Contains("ByAxis"))
                {
                    filter.type = CVRMAVertexFilterType.ByAxis;
                    var centerProp = so.FindProperty("m_center");
                    if (centerProp != null) filter.planeOrigin = centerProp.vector3Value;
                    var axisProp = so.FindProperty("m_axis");
                    if (axisProp != null) filter.planeNormal = axisProp.vector3Value;
                }
                else if (typeName.Contains("ByMask"))
                {
                    filter.type = CVRMAVertexFilterType.ByMask;
                    filter.maskTexture = so.FindProperty("m_maskTexture")?.objectReferenceValue as Texture2D;
                    // MA ByMaskMode: DeleteBlack=0 → SelectBlack, DeleteWhite=1 → SelectWhite.
                    var deleteModeProp = so.FindProperty("m_deleteMode");
                    filter.maskMode = deleteModeProp != null && deleteModeProp.enumValueIndex == 1
                        ? CVRMAMaskSelectMode.SelectWhite
                        : CVRMAMaskSelectMode.SelectBlack;
                    var slotProp = so.FindProperty("m_materialIndex");
                    if (slotProp != null) filter.materialSlot = slotProp.intValue;
                    mapped &= TryMapUVChannel(so, src, filter);
                }
                else if (typeName.Contains("ByUVTile"))
                {
                    filter.type = CVRMAVertexFilterType.ByUVTile;
                    var invertProp = so.FindProperty("m_invert");
                    if (invertProp != null) filter.invert = invertProp.boolValue;
                    mapped &= TryMapUVChannel(so, src, filter);
                    mapped &= TryMapUVTileRange(so, src, filter);
                }
                else
                {
                    mapped = false;
                }

                if (mapped)
                {
                    dst.filters.Add(filter);
                    Undo.DestroyObjectImmediate(sibling);
                }
                else
                {
                    Debug.LogWarning(
                        $"[MA-CVR] MeshCutter on '{src.gameObject.name}': vertex filter '{typeName}' could not " +
                        "be converted automatically — rebuild it on the CVRMAMeshCutter manually.");
                }
            }

            if (dst.filters.Count == 0)
                Debug.LogWarning(
                    $"[MA-CVR] MeshCutter on '{src.gameObject.name}': no vertex filters converted — " +
                    "configure filters on the CVRMAMeshCutter before building.");
        }

        private static bool TryMapUVChannel(SerializedObject so, Component src, CVRMAVertexFilter filter)
        {
            var chProp = so.FindProperty("m_uvChannel");
            int channel = chProp != null ? chProp.intValue : 0;
            if (channel > 3)
            {
                Debug.LogWarning(
                    $"[MA-CVR] MeshCutter on '{src.gameObject.name}': filter uses UV channel {channel}, " +
                    "but CVRMA supports 0-3 only.");
                return false;
            }
            filter.uvChannel = channel;
            return true;
        }

        /// <summary>
        /// MA serializes By UV Tile as a UV range (min/max per axis). We support single
        /// UDIM tiles, so only ranges spanning exactly one unit tile convert cleanly.
        /// </summary>
        private static bool TryMapUVTileRange(SerializedObject so, Component src, CVRMAVertexFilter filter)
        {
            bool useUMin = so.FindProperty("m_useUMin")?.boolValue ?? false;
            bool useUMax = so.FindProperty("m_useUMax")?.boolValue ?? false;
            bool useVMin = so.FindProperty("m_useVMin")?.boolValue ?? false;
            bool useVMax = so.FindProperty("m_useVMax")?.boolValue ?? false;
            float uMin = so.FindProperty("m_uMin")?.floatValue ?? 0f;
            float uMax = so.FindProperty("m_uMax")?.floatValue ?? 1f;
            float vMin = so.FindProperty("m_vMin")?.floatValue ?? 0f;
            float vMax = so.FindProperty("m_vMax")?.floatValue ?? 1f;

            bool unitTile = useUMin && useUMax && useVMin && useVMax &&
                            Mathf.Approximately(uMax - uMin, 1f) &&
                            Mathf.Approximately(vMax - vMin, 1f) &&
                            Mathf.Approximately(uMin, Mathf.Round(uMin)) &&
                            Mathf.Approximately(vMin, Mathf.Round(vMin));
            if (!unitTile)
            {
                Debug.LogWarning(
                    $"[MA-CVR] MeshCutter on '{src.gameObject.name}': By UV Tile filter uses a UV range " +
                    "that is not a single UDIM tile — CVRMA only supports whole tiles.");
                return false;
            }

            filter.tileX = Mathf.RoundToInt(uMin);
            filter.tileY = Mathf.RoundToInt(vMin);
            return true;
        }

        private static void ConvertScaleAdjuster(Component src)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAScaleAdjuster");
            var dst = Undo.AddComponent<CVRMAScaleAdjuster>(src.gameObject);

            var so = new SerializedObject(src);
            var scaleProp = so.FindProperty("m_Scale");
            dst.scale = scaleProp != null ? scaleProp.vector3Value : Vector3.one;

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertReplaceObject(Component src, GameObject avatarRoot)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMAReplaceObject");
            var dst = Undo.AddComponent<CVRMAReplaceObject>(src.gameObject);

            var so = new SerializedObject(src);
            dst.targetObject = ResolveAOR(so.FindProperty("targetObject"), avatarRoot.transform,
                $"ReplaceObject on '{src.gameObject.name}'.targetObject");
            if (dst.targetObject == null)
                Debug.LogWarning($"[MA-CVR] ReplaceObject on '{src.gameObject.name}': target was empty or unresolved.");

            Undo.DestroyObjectImmediate(src);
        }

        private static void ConvertRemoveVertexColor(Component src)
        {
            Undo.RecordObject(src.gameObject, "Add CVRMARemoveVertexColors");
            var dst = Undo.AddComponent<CVRMARemoveVertexColors>(src.gameObject);

            // MA RemoveMode: Remove=0, DontRemove=1 — same order as ours.
            var so = new SerializedObject(src);
            var modeProp = so.FindProperty("Mode") ?? so.FindProperty("m_Mode");
            dst.mode = (modeProp?.enumValueIndex ?? 0) == 1
                ? CVRMARemoveVertexColorMode.DontRemove
                : CVRMARemoveVertexColorMode.Remove;

            Undo.DestroyObjectImmediate(src);
        }

        private static void SimpleConvert<T>(Component src) where T : CVRMAComponent
        {
            Undo.RecordObject(src.gameObject, $"Add {typeof(T).Name}");
            Undo.AddComponent<T>(src.gameObject);
            Undo.DestroyObjectImmediate(src);
        }

        // ------------------------------------------------------------------ AvatarObjectReference resolver

        /// <summary>
        /// Resolves an AvatarObjectReference SerializedProperty to a scene GameObject.
        /// Tries the direct targetObject reference first, then falls back to referencePath.
        /// Emits a warning to the console (and increments _aorFailures) when a non-empty
        /// AOR fails to resolve — so users can see WHICH binding the converter couldn't translate.
        /// </summary>
        private static GameObject ResolveAOR(SerializedProperty aorProp, Transform avatarRoot,
            string context = null)
        {
            if (aorProp == null) return null;

            // Direct object reference (fastest, most reliable)
            var targetObjProp = aorProp.FindPropertyRelative("targetObject");
            if (targetObjProp?.objectReferenceValue is GameObject directGO && directGO != null)
                return directGO;

            // Path-based resolution from avatar root
            var refPathProp = aorProp.FindPropertyRelative("referencePath");
            var refPath = refPathProp?.stringValue ?? "";
            if (string.IsNullOrEmpty(refPath))
                return null; // genuinely unset — no warning needed

            // Support both the modern MA constant ($$$AVATAR_ROOT$$$) and the legacy form.
            if (refPath == "$$$AVATAR_ROOT$$$" || refPath == "$$AVATAR_ROOT$$" || refPath == "$$ROOT$$")
                return avatarRoot.gameObject;

            var found = avatarRoot.Find(refPath);
            if (found == null)
            {
                _aorFailures++;
                Debug.LogWarning(
                    $"[MA-CVR] Converter: could not resolve AvatarObjectReference path " +
                    $"'{refPath}' under avatar '{avatarRoot.name}'" +
                    (context != null ? $" ({context})" : "") +
                    ". Is the right avatar root selected, and does the object still exist?");
                return null;
            }

            return found.gameObject;
        }

        private static int _aorFailures;

        private static bool IsVRCMAPresent()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetType("nadena.dev.modular_avatar.core.ModularAvatarMergeArmature") != null)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------ Report types

        private class ConversionReport
        {
            public readonly List<ReportItem> Items = new List<ReportItem>();
            public int ConvertCount => Items.Count(i => i.Action == ConversionAction.Convert);
            public int RemoveCount  => Items.Count(i => i.Action == ConversionAction.Remove);
            public int ManualCount  => Items.Count(i => i.Action == ConversionAction.Manual);
            public bool HasConvertible => ConvertCount + RemoveCount > 0;

            public void Add(string type, string objName, ConversionAction action) =>
                Items.Add(new ReportItem { ComponentType = type, ObjectName = objName, Action = action });
        }

        private class ReportItem
        {
            public string ComponentType;
            public string ObjectName;
            public ConversionAction Action;
        }

        private enum ConversionAction { Convert, Remove, Manual }
    }
}
#endif
