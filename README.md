# Stadium-3D Football

A four-surface CAVE demo: the front wall follows the ball, the side walls isolate each team,
the floor carries the tactical top-down. A scripted touchdown drive runs on a loop.

Forked from the Sit-Rep CAVE project, keeping the MiddleVR CAVE rig, the region/panel layout
system, and the wand + gamepad operator input layers. See
[`Docs/football_viewing_room_plan.md`](Docs/football_viewing_room_plan.md) for the full plan.

---

## Requirements

- **Unity 2022.3.9f1** (URP 14.0.11)
- **MiddleVR 2**, installed to `C:\Program Files\MiddleVR2`

> `Packages/manifest.json` pins MiddleVR by **absolute local path**:
> ```
> "com.middlevr":     "file:C:/Program Files/MiddleVR2/unity_packages/com.middlevr.tgz",
> "com.middlevr.urp": "file:C:/Program Files/MiddleVR2/unity_packages/com.middlevr.urp.tgz"
> ```
> The project will not resolve on a machine without MiddleVR 2 at that exact path. This is
> inherited from the source project and is the main obstacle to a second workstation or CI.

## CAVE geometry

From `Assets/_Project/Config/MVR.vrx` — the calibration source of truth, now **in-repo**:

| Surface | Physical | Aspect | Viewport |
| --- | --- | --- | --- |
| Front (`ScreenCenter`) | 3.556 × 2.2225 m | 1.60000 | 2560 × 1600 |
| Floor (`ScreenFloor`) | 3.556 × 2.2225 m | 1.60000 | 2560 × 1600 |
| Left (`ScreenLeft`) | 2.3876 × 2.2225 m | 1.07429 | 1719 × 1600 |
| Right (`ScreenRight`) | 2.3876 × 2.2225 m | 1.07429 | 1719 × 1600 |

All four `CameraStereo` nodes are parented to `HeadNode` at local origin — four off-axis frusta
onto one shared world. Per-wall *views* are therefore RenderTexture panels, not repositioned
cameras. `cullingMask` is the only per-wall lever MiddleVR does not overwrite each frame.

## Layout

```
Assets/_Project/
  Config/          MVR.vrx (CAVE calibration), CaveRegionMap.asset (4 base regions)
  Prefabs/
    Environment/   CAVE_Room.prefab
    Rig/           MVRManager Variant, MVR_URP Variant
  Scenes/CAVE/
    Boot/          BootMiddleVR2.unity
  Scripts/
    CaveBox/       Region map, panel placement, wand pointer/buttons, focus navigation,
                   MiddleVR display-camera access, the front-wall portal
    Football/      The sim: field math, route baking, ball solver, game state, drive
                   transport, stats, and the scripted drive content
    Operators/     IOperatorInput, registry, wand + gamepad backends, edge sampling
    Telemetry/     InteractionLog
    Vr/            MiddleVRWand — the wand read every input path sits on
    Editor/        StadiumTestRunner, StadiumRoomBuilder, FrontWallGroupBuilder,
                   GamepadProbeBuilder
  Tests/EditMode/  271 assertions across 21 files
```

Assemblies: `Stadium3D.Runtime`, `Stadium3D.Football`, `Stadium3D.Editor`, `Stadium3D.Tests`.
Namespaces: `Stadium3D.CaveBox`, `Stadium3D.Football`, `Stadium3D.Operators`, `Stadium3D.Telemetry`,
`Stadium3D.Vr`, `Stadium3D.EditorTools`.

`Stadium3D.Football` references **no** MiddleVR, XR or Input System assembly. That is deliberate: it
makes "the sim runs off the Unity run loop" a structural property rather than a convention, so it
cannot accidentally grow a dependency on the render or input path.

## Layers

| # | Layer | Purpose |
| --- | --- | --- |
| 8 | `PitchMiniature` | Reserved for M8 — 3D miniature field, the one thing display cameras *may* see |
| 11 | `WandUI` | Wand-clickable chips (hard-coded in `WandPointer` / `WandButton`) |
| 14 | `PitchShared` | Turf, lines, goalposts — all four pitch cameras |
| 15 | `PitchPlayers` | Un-dimmed player markers — front + floor |
| 16 | `PitchRightWall` | Home-wall per-player dressing — right camera only |
| 17 | `PitchLeftWall` | Away-wall per-player dressing — left camera only |
| 18 | `PitchTactical` | Routes, coverage, down markers — floor camera only |
| 19 | `PitchStadium` | Bowl, crowd, floodlights — front + left + right |
| 20 | `CaveScreen` | The four RT panels and their wall-pick colliders — display cameras only |

## Open items from the fork

These are known and deliberate, not oversights:

0. **MiddleVR does not compile on this Unity version without a patch, and the patch is not in the
   repo.** `Library/PackageCache/com.middlevr@…/Runtime/MiddleVR.Unity.Scripts/Internal/MVRManagerScript.cs`
   guards on `UNITY_2019_4 … UNITY_2022_1` and `#error`s on anything else — including 2022.3.9f1.
   The three guard lines are commented out locally so the project builds. **`Library/` is gitignored
   and Unity regenerates it from the `.tgz`, so this patch is lost on any clean clone or package
   reimport.** The source Sit-Rep CAVE project has the identical hand-edit and the identical exposure.
   `UNITY_2022_1` appears nowhere else in the package, so the guard is a pure version gate with no
   code behind it. Fix properly by getting a MiddleVR build that supports 2022.3, or by adding an
   Editor script that re-applies the patch on import.

1. **`Boot/BootMiddleVR2.unity` carries two dangling references** — `Target_Sphere.cs` and
   `Gun_MiddleVR.prefab`, both dropped with the shooting-gallery code. They appear as missing
   objects on first open and should be deleted in-editor. The rest of the scene (CAVE_Room,
   MVRManager, MVR_URP) is intact and is the base to build the football scene from.
2. **The football scene does not exist yet.** M2 builds it.
3. **Side-screen orientation in `MVR.vrx` is unresolved.** `1.11125 − 2.3876/2 = −0.08255`
   exactly, and the declared Z-rotation cannot produce a side wall. Run a labelled test card on
   all four surfaces before M3 — no desk test catches this, because panels are placed from
   `CaveRegionMap.asset` and never from the `.vrx`.
4. **`Assets/Settings/Project Configuration/Performance URP Config.asset`** still has
   `m_ShadowDistance: 2.5` and a 4096 main-light shadowmap, tuned for a 3.5 m room. M2 turns
   main-light shadows off entirely and uses blob shadows.
5. **`CaveRegionMap.asset` holds only the four base regions.** The `Football*` regions land in
   M3, after the hardware check in item 3.

## Tests

Run from the Unity Test Runner (EditMode), or `Stadium3D ▸ Tests ▸ Run EditMode Tests`.
