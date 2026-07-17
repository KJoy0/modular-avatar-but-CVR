# Modular Avatar CVR

A non-destructive, drag-and-drop avatar assembly toolkit for **ChilloutVR**, mirroring
[Modular Avatar](https://modular-avatar.nadena.dev/) (VRChat) — plus a one-click converter
that migrates VRC MA setups to their CVR equivalents.

Everything is applied **at build time** by a CCK build processor: your scene stays clean,
your original meshes and animator assets are never modified, and removing a component
removes its effect. Drop an outfit prefab on your avatar, add a Merge Armature, and upload.

- **Namespace:** `ModularAvatarCVR` (runtime) / `ModularAvatarCVR.Editor` (editor)
- **Requires:** ChilloutVR CCK, Unity 2022.3
- **Hook point:** `CCKBuildProcessor` — discovered automatically by the CCK via reflection;
  no manual setup needed.

---

## Components

Add via **Add Component → Modular Avatar CVR**, or the
**GameObject → Modular Avatar CVR** menu (Setup Outfit, Create Toggle, Create Toggle for Selection).

### Outfit & hierarchy

| Component | What it does |
|---|---|
| **MA Merge Armature** | Merges an outfit's armature into the avatar's at build time. Auto-infers bone prefix/suffix when unset, and re-skins meshes onto the base bones (no duplicate bone chains, no deformation). |
| **MA Bone Proxy** | Reparents an object to a humanoid bone (by `HumanBodyBones`, portable across avatars) with live editor preview. Attachment modes for keeping world pose / rotation / position, plus match-scale. |
| **MA Replace Object** | Swaps an object into another object's place in the hierarchy at build. |
| **MA Scale Adjuster** | Scales a bone with child compensation (children keep their world size). |
| **MA Floor Adjuster** | Marker object at the bottom of your shoes; the build raises the avatar (Hips) so that height becomes the floor — CVR view and voice positions are shifted to match. One per avatar. |
| **MA Visible Head Accessory** | Makes a head-attached accessory visible in first person (adds `FPRExclusion`). |
| **MA Mesh Settings** | Applies probe anchor / bounds overrides to all renderers in its subtree. |
| **MA Remove Vertex Colors** | Strips vertex colors from meshes in its subtree (on cloned meshes). A nested *Don't Remove* excludes its subtree. |

### Mesh Cutter

**MA Mesh Cutter** deletes or toggles a portion of a mesh, selected by **vertex filters**
(parity with VRC MA 1.14+):

- Filters: **By Bone** (skin weight), **By Blendshape** (moved vertices), **By Axis** (plane),
  **By Mask** (texture, per material slot), **By UV Tile** (UDIM)
- Per-filter triangle mode (Any Vertex / All Vertices / Centroid) and invert;
  filters combine by union or intersection
- **Delete** rebuilds the mesh without the selected triangles (blendshapes, skin weights,
  UVs, and material slots all preserved; original mesh asset untouched)
- **Toggle** splits the selection into a child renderer wired to an AAS toggle
- Inspector **Preview Selection** button shows cut triangle counts and paints the affected
  vertices in the scene view

### Menus (→ CVR Advanced Avatar Settings)

| Component | What it does |
|---|---|
| **MA Menu Item** | One AAS control. Toggle/Button (Bool, Int, or Float parameter), RadialPuppet → Slider, TwoAxisPuppet → Joystick2D, FourAxisPuppet → Joystick3D, SubMenu → children flattened. |
| **MA Menu Group** | Logical grouping; children are flattened into the AAS list. |
| **MA Menu Installer** | Marks a subtree of menu items for installation on the avatar. |
| **MA Menu Install Target** | Named slot controlling where installed entries are inserted. |
| **MA Parameters** | Registers animator-driven parameters (Bool/Int/Float) in AAS for network sync without a visible control. |
| **MA Modular Settings** (+ Installer) | Reusable asset containing AAS entries, installable onto any avatar. |

**Toggle parameter types** (mirrors VRC MA):

- **Bool** → AAS Toggle
- **Int** → all Int toggles sharing a parameter merge into **one AAS Dropdown** — exclusive
  selection, the MA radio-toggle pattern (outfit pickers etc.). Each item's Value is its
  option index; its label is the option name.
- **Float** → AAS Toggle writing 0/1 as float

### Reactive components

React to a parameter — explicit, inherited from the nearest parent MA Menu Item, or
defaulting to the GameObject name. Int/Float menu items produce correctly typed animator
conditions (`Equals value` for Int, threshold for Float), so reactive components work
inside dropdown selections.

| Component | What it does |
|---|---|
| **MA Object Toggle** | Shows/hides GameObjects (each entry can invert). |
| **MA Shape Changer** | Sets or deletes blendshapes while active. |
| **MA Material Swap** | Swaps materials (from → to) on all renderers under a root. |
| **MA Material Setter** | Sets a specific material slot on a specific renderer. |

> **Why animator layers instead of AAS clips:** CVR only regenerates the AAS animator when
> you click *Create Controller* in the CVRAvatar inspector — never during a build. Each
> reactive component therefore builds its own animator layer (merged via Merge Animator)
> and registers a slim AAS entry for the menu parameter.

### Animators

| Component | What it does |
|---|---|
| **MA Merge Animator** | Merges an AnimatorController into the avatar's override controller at build (absolute or relative path mode). |
| **MA Merge Blend Tree** | Wraps a blend tree as an always-on animator layer (VRC MA "Merge Motion"). |
| **MA Blendshape Sync** | Outfit blendshapes follow base-avatar blendshapes: live mirror in editor; at build every animation clip touching the source blendshape is retargeted to also drive the follower. Optional **curve remap** (source weight → follower weight) per entry. |

---

## VRC → CVR converter

**Tools → Modular Avatar CVR → Convert VRC MA to CVR MA**

Reflection/SerializedObject-based — works with or without the VRC SDK installed. Produces a
report (Convert / Remove / Manual) before touching anything.

- Converts every VRC MA component above to its CVRMA equivalent, including Mesh Cutter
  vertex-filter components, menu items (parameter type inferred from the avatar's VRC
  expression parameters), and Floor Adjuster
- VRC constraints → Unity constraints, VRC contacts → NAK equivalents
- Removed as N/A: World Fixed/Scale Object (CVR has Props), PhysBone Blocker & Global
  Collider (CVR uses Magica Cloth 2 / NAK), Platform Filter, Sync Parameter Sequence,
  VRChat Settings, MMD Layer Control

---

## Build pipeline

`CVRMABuildProcessor` runs all passes in `OnPreProcessAvatar`:

```
ReplaceObject → VisibleHeadAccessory → BoneProxy → MergeArmature → ScaleAdjuster
→ FloorAdjuster → MeshSettings → RemoveVertexColors → MeshCutter → ShapeChanger
→ Parameters → ObjectToggle → MaterialSwap → MaterialSetter → ModularSettings
→ MenuToAAS → MergeBlendTree → MergeAnimator → BlendshapeSync
```

Generated assets (cloned meshes, controllers, clips) go to `Assets/MA_CVR_Temp/` and are
cleaned up after the build. Source assets are never modified.

---

## Intentionally not implemented

| VRC MA feature | Reason |
|---|---|
| World Fixed / World Scale Object | CVR has native Props |
| PhysBone Blocker, Global Collider, Rename Collision Tags | VRC PhysBones/Contacts; CVR uses Magica Cloth 2 / NAK |
| Platform Filter | CVR is PC-only |
| Sync Parameter Sequence, VRChat Settings, MMD Layer Control | VRC platform/world conventions |
| Extract Menu | CVR AAS is a flat list — nothing to extract |

Not yet built (candidates): Move Independently, Manual Bake Avatar, Reactive Object Debugger.
and yes some or most is ai sloppa
