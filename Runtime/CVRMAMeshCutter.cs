using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModularAvatarCVR
{
    public enum CVRMAMeshCutterMode
    {
        /// <summary>A triangle is selected when it matches ANY filter (union).</summary>
        VertexUnion,
        /// <summary>A triangle is selected only when it matches ALL filters (intersection).</summary>
        VertexIntersection
    }

    public enum CVRMAMeshCutterAction
    {
        /// <summary>Permanently remove the selected portion from the mesh at build time.</summary>
        Delete,
        /// <summary>Split the selected portion into a child renderer with an AAS toggle.</summary>
        Toggle
    }

    public enum CVRMAVertexFilterType
    {
        /// <summary>Vertices weighted to a bone (optionally including its children).</summary>
        ByBone,
        /// <summary>Vertices moved by any of the listed blendshapes.</summary>
        ByBlendshape,
        /// <summary>Vertices on the positive side of a plane (mesh-local space).</summary>
        ByAxis,
        /// <summary>Vertices whose UV samples a mask texture region.</summary>
        ByMask,
        /// <summary>Vertices inside a UDIM-style UV tile.</summary>
        ByUVTile
    }

    /// <summary>How a triangle qualifies from its vertices' matches (mirrors VRC MA).</summary>
    public enum CVRMATriangleSelectMode
    {
        /// <summary>Triangle matches when ANY of its vertices match.</summary>
        AnyVertex,
        /// <summary>Triangle matches only when ALL of its vertices match.</summary>
        AllVertices,
        /// <summary>Triangle matches when most of its vertices match (≈ MA's Centroid mode).</summary>
        Centroid
    }

    public enum CVRMAMaskSelectMode
    {
        /// <summary>Select vertices where the mask is black (value &lt; 0.5). Matches MA's DeleteBlack.</summary>
        SelectBlack,
        /// <summary>Select vertices where the mask is white (value ≥ 0.5). Matches MA's DeleteWhite.</summary>
        SelectWhite
    }

    [Serializable]
    public class CVRMAVertexFilter
    {
        public CVRMAVertexFilterType type = CVRMAVertexFilterType.ByBone;

        [Tooltip("Select the vertices NOT matched by this filter instead.")]
        public bool invert;

        [Tooltip("How a triangle qualifies: any vertex matching, all vertices matching, or the majority (centroid).")]
        public CVRMATriangleSelectMode selectionMode = CVRMATriangleSelectMode.AnyVertex;

        // ByBone
        [Tooltip("Bone whose weighted vertices are selected. Must be one of the target mesh's bones (or a parent of them when Include Child Bones is on).")]
        public Transform bone;
        [Tooltip("Also select vertices weighted to child bones of the bone above.")]
        public bool includeChildBones = true;
        [Tooltip("Minimum total skin weight (0–1) a vertex must have on the matched bones.")]
        [Range(0f, 1f)] public float minBoneWeight = 0.01f;

        // ByBlendshape
        [Tooltip("Blendshapes whose moved vertices are selected (a vertex matches if ANY listed shape moves it).")]
        public List<string> blendshapeNames = new List<string>();
        [Tooltip("Minimum vertex delta (in meters, at full weight) for a vertex to count as moved.")]
        public float minShapeDelta = 0.001f;

        // ByAxis
        [Tooltip("A point on the cutting plane, in the target mesh's local space.")]
        public Vector3 planeOrigin = Vector3.zero;
        [Tooltip("Plane normal — vertices on this side of the plane are selected.")]
        public Vector3 planeNormal = Vector3.up;

        // ByMask / ByUVTile
        [Tooltip("Mask texture sampled at each vertex's UV.")]
        public Texture2D maskTexture;
        [Tooltip("Whether black or white areas of the mask select vertices.")]
        public CVRMAMaskSelectMode maskMode = CVRMAMaskSelectMode.SelectBlack;
        [Tooltip("Restrict the mask to one material slot (submesh). -1 applies to the whole mesh.")]
        public int materialSlot = -1;
        [Tooltip("UV channel (0–3) used for mask sampling / tile lookup.")]
        [Range(0, 3)] public int uvChannel = 0;
        [Tooltip("UDIM tile column — vertices with floor(u) == this are selected.")]
        public int tileX;
        [Tooltip("UDIM tile row — vertices with floor(v) == this are selected.")]
        public int tileY;
    }

    /// <summary>
    /// Cuts a portion of a mesh, selected by vertex filters, at build time.
    /// Mirrors VRC MA's Mesh Cutter (1.14+), including per-filter triangle
    /// selection modes and union/intersection filter combining.
    ///
    /// Delete: selected triangles are removed from the mesh (cloned — the original
    /// asset is never touched).
    ///
    /// Toggle: the selected portion is split into a child renderer and wired to a
    /// CVR AAS toggle via a generated CVRMAObjectToggle.
    /// </summary>
    [AddComponentMenu("Modular Avatar CVR/MA Mesh Cutter")]
    public class CVRMAMeshCutter : CVRMAComponent
    {
        [Tooltip("The mesh whose vertices will be cut. Defaults to the SkinnedMeshRenderer on this GameObject.")]
        public SkinnedMeshRenderer targetMesh;

        public CVRMAMeshCutterMode mode = CVRMAMeshCutterMode.VertexUnion;

        public CVRMAMeshCutterAction action = CVRMAMeshCutterAction.Delete;

        public List<CVRMAVertexFilter> filters = new List<CVRMAVertexFilter>();

        // Toggle mode only
        [Tooltip("Display name shown in the CVR Quick Menu. Defaults to the GameObject name.")]
        public string label = "";
        [Tooltip("Animator parameter name. If empty, inherits the nearest parent MA Menu Item's parameter, else the GameObject name.")]
        public string parameter = "";
        [Tooltip("Default state of the toggle parameter when the avatar loads.")]
        public bool defaultValue = false;
        [Tooltip("When ON, hide the cut portion (true) or show it (false).")]
        public bool hiddenWhenOn = true;

        public SkinnedMeshRenderer GetEffectiveTarget() =>
            targetMesh != null ? targetMesh : GetComponent<SkinnedMeshRenderer>();

        public string GetEffectiveLabel()     => string.IsNullOrEmpty(label) ? gameObject.name : label;
        public string GetEffectiveParameter() => ResolveReactiveParameter(parameter);
    }
}
