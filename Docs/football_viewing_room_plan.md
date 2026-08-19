# Stadium-3D — Football Viewing Room

*A CAVE demo built on the Sit-Rep CAVE infrastructure · plan / spec*

*Four surfaces, four views of one football play: the front wall follows the ball, the side walls isolate each team, the floor is the tactical top-down. A scripted touchdown drive runs on a loop; the wand claims a wall and shifts focus between players.*

---

## 0. The constraint that shapes everything

**MiddleVR renders one world from one head position.**

`Assets/_Project/Config/MVR.vrx` declares four stereo cameras, and every one is parented to `HeadNode` at `PositionLocal="0,0,0"`:

| Camera | Screen | Physical size | Aspect | Projector viewport |
| --- | --- | --- | --- | --- |
| `CameraCenter` | `ScreenCenter` | 3.556 × 2.2225 m | 1.60000 | 2560 × 1600 (1.60000) |
| `CameraFloor` | `ScreenFloor` | 3.556 × 2.2225 m | 1.60000 | 2560 × 1600 (1.60000) |
| `CameraLeft` | `ScreenLeft` | 2.3876 × 2.2225 m | 1.07429 | 1719 × 1600 (1.07438) |
| `CameraRight` | `ScreenRight` | 2.3876 × 2.2225 m | 1.07429 | 1719 × 1600 (1.07438) |

Four off-axis frusta onto **one** shared scene, and MiddleVR actively defends that:

| Per frame, MiddleVR rewrites | Where |
| --- | --- |
| `localPosition` / `localRotation` / `localScale` of every mapped node | `MVRNodesMapper.UpdateChildrenNodesMiddleVRToUnity()` |
| `nearClipPlane` / `farClipPlane` / `projectionMatrix` | `MVRTools.UpdateCameraProperties()` |
| **`cullingMask` — never** | — |

So "left wall = a camera on team 2's sideline" is **not achievable by moving a MiddleVR camera**. Park it and it snaps back the same frame; override its projection and the frustum no longer matches the physical screen.

`cullingMask` is the one durable lever, and this project already exploits it twice (`FrontWallPortal`, `DetectionXRayManager`). A one-shot flip persists precisely because `UpdateCameraProperties` never touches it.

**Therefore:** the four views are **flat RenderTexture panels hung as world geometry**, fed by four ordinary cameras out at the pitch, with the display cameras culling the pitch entirely.

**Say the cost out loud:** the football on those panels is **monoscopic**. Head tracking still gives correct parallax on the *panel* (a real object in the room), but the play itself has no stereo depth. That follows directly from the shared `HeadNode` and there is no way around it while the walls show different views. Stereo returns only in **M8**, where the miniature field is real geometry the display cameras *are* allowed to see.

---

## 1. Three amendments — all accepted

Each is arithmetic, not taste. All three were accepted and are now part of the spec,
not proposals.

### 1.1 The floor cannot carry jersey identity — and it should be reoriented

Fitting the full field (109.7 × 48.8 m) into the 3.556 × 2.2225 m floor gives **1:30.9**:

| Element | Real | On the floor | Angular size at 1.9 m |
| --- | --- | --- | --- |
| Player marker disc | 1 m | 3.2 cm | — |
| Seven linemen across | ~8 m | 26 cm total, 3.7 cm centres | one smear at every snap |
| Jersey number | 20 cm | 0.65 cm | **0.20°** (need 0.4–0.5° to read two digits) |

Numbers are **2.5× too small**, and no achievable scale fixes it. Two consequences:

1. **The floor carries position, formation, motion and line markers — never identity.** That is exactly what you asked it for ("highlight the ball, players, markers for downs/lines"), so nothing is lost. It just means no jersey numbers on the floor.
2. **Reorient and crop.** Run field *length* along the room's 3.556 m axis and field *width* across the 2.2225 m axis at **1:22** — full field width, ~85 of 120 yards visible, the window sliding with the ball. Markers become 2.3 cm at 5.5 cm spacing: **linemen separate and formation is legible.**

Also: you stand on the floor, so your body occludes the middle and (if projected) casts a shadow. Bias the ball's neighbourhood toward the front half.

### 1.2 The side walls should frame the player at *true scale*

You chose a broadcast iso cam, and the mechanism stays exactly that. What changes is the framing rule.

> A 1.88 m person at 1.778 m viewing distance subtends **55.8° vertically**. The side walls offer **64°**.

Solve each iso camera's zoom so the focused player renders **1:1 life-size on the wall plane**. Turn your head and a professional athlete is standing right there at true scale — an image no television can produce, and the one thing in this demo that is unambiguously *because* it's a CAVE.

This also resolves the aspect worry. The side walls are 1.074 — essentially square — which is wrong for a wide sideline shot but exactly right for a standing human. Same rig, same milestone, better payoff.

### 1.3 Add a beat schedule and slow motion

A football play is a 4-second event, and during those 4 seconds the viewer's head is locked forward. Nobody turns 90° mid-play to check an iso cam, because turning means missing the play. Left as four always-live channels, three walls are expensive wallpaper.

The fix is cheap — one `timeScale` on the sim you're already building, plus a per-wall emphasis schedule. **Ball-in-air is the only football event long enough for a person to use four surfaces**, so stretch the hero play's throw to ~12 s at 0.3×:

| Beat | Front | Right (home) | Left (away) | Floor |
| --- | --- | --- | --- | --- |
| Huddle 3 s | down & distance, large | dim | dim | formation forming |
| Snap → live 4 s | **sole focus** | dim | dim | dim |
| Ball in air, 0.3× ≈ 12 s | wide, slow | **receiver, life-size** | **defender, life-size** | routes drawing |
| Catch 2 s | freeze | hold | hold | hold |
| Result 4 s | yards, chains move | player card | player card | field resets |

The walls stay live throughout — this is emphasis, not blanking — but the room now tells you where to look. It also mitigates the framing risk that American football reads to a newcomer as "people stand around, 4 seconds of chaos, people stand around." A ball hanging in the air with two life-size men running under it needs no rules explained.

---

## 2. M0 — the repo split

### 2.1 A git hazard to handle first

`C:/Users/Exx/KostageProjects/Stadium-3D` is **not a new project**. It is a copy of the Sit-Rep-CAVE repo with the same history (HEAD `da614b1`) and — verified — the same remote:

```
origin  https://github.com/Keko787/CARE-Sit-Rep-Cave.git  (fetch & push)
```

`git worktree list` from here reports worktrees **inside the sibling Sit-Rep-CAVE tree**:

```
C:/Users/Exx/KostageProjects/Stadium-3D                                            da614b1 [secondary-operator-gamepad]
C:/Users/Exx/KostageProjects/_sitrep-testwt                                        987c998 (detached HEAD)
C:/Users/Exx/KostageProjects/Sit-Rep-CAVE/.claude/worktrees/hopeful-clarke-e32552  1dccfa0
C:/Users/Exx/KostageProjects/Sit-Rep-CAVE/.claude/worktrees/nifty-hertz-1f4b85     1dccfa0
```

The two repos share git plumbing. `git worktree prune`, `git gc`, and some `checkout`/`branch -D` paths run from Stadium-3D can reach into Sit-Rep-CAVE's files, and any `git push` here goes to the old remote.

> **Rule for M0: build the new repo by file copy into a brand-new directory. Do not `git init` in place, do not reuse the existing `.git`, and run no mutating git command in `Stadium-3D`.**
>
> Copy from the **working tree**, not `HEAD` — the tree is mid-refactor on `secondary-operator-gamepad` with three scenes showing deleted and their replacements untracked.

### 2.2 M0 is a milestone, not a chore

The keep set is ~430 files of 1,853 tracked. But the interesting number is what *doesn't* survive:

| | Before | After | Note |
| --- | --- | --- | --- |
| Editor builder scripts | 20 files, 3,655 lines | 4 files, ~285 lines | 16 reference dropped namespaces |
| EditMode tests | 325 assertions, 50 files | ~109, 13 files | plus 2 files needing surgery |
| Runtime namespaces | 12 | 3 | `CaveBox`, `Operators`, `VRShootingGallery` (wand only) |

**Only three hard compile breaks exist.** Every other mention of a dropped type across the keep set is inside a doc comment.

| # | File | Break | Fix |
| --- | --- | --- | --- |
| 1 | `Scripts/CaveBox/FrontWallPortal.cs` | `using Care.ControlCenter` (line 3), `InteractionLog m_Log` (55), `m_Log?.Log(…)` (~211) | Delete all three lines, or keep `InteractionLog.cs` |
| 2 | `Scripts/Core/MatchService.cs` | `using VRShootingGallery.Targets`, aggregates `TargetBase.Scored` | **Drop `Core/` entirely** — it is the shooting-gallery match loop, not infrastructure. `UI/HUDController.cs` dies with it |
| 3 | `Tests/EditMode/CaveRegionMapTests.cs` | `using Care.DroneFeeds` (line 2), 5 assertions calling `FeedWindowSpawner.AssignRegion` | Delete the import and the two affected `[Test]` methods; the other 8 tests survive |

`Scripts/Rig/StationaryRigGuard.cs` also drops — it guards an XRI **XR Origin**, and there is no XR Origin under MiddleVR. It is the only reason `_Project.Runtime.asmdef` still drags `Unity.XR.Interaction.Toolkit` and `Unity.XR.OpenXR`.

### 2.3 Things the naive keep list misses

Copying only the obvious paths leaves a project that will not open:

- `Assets/_Project/Art/Materials/Wall.mat`, `Wall 1.mat`, `Wall 2.mat`, `Wall 3.mat` — the **only** materials `CAVE_Room.prefab` references. Without them the room is magenta.
- `Assets/TextMesh Pro/` — every world-space label in M4/M5.
- `Assets/DefaultVolumeProfile.asset` — URP global settings.
- `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` — referenced by `EditorBuildSettings.m_configObjects`.
- `Assets/Settings/Project Configuration/` — bound by `GraphicsSettings.m_CustomRenderPipeline`.

### 2.4 The two Editor tools worth porting rather than deleting

- **`Editor/CaveLayoutPreview.cs`** (351 lines, blocked only by `using Care.DroneFeeds`) — per its own doc comment, the **only** round-trip that writes hand-dragged panel poses back into `CaveRegionMap.asset`. Without it, region calibration in M3/M7 means typing numbers into YAML by hand. This is the most valuable desk-side tool in the repo.
- **`Editor/TopDownMapCameraBuilder.cs`** and **`Editor/SimEnvironmentBuilder.cs`** — the patterns the new field and floor-camera builders copy. Extract the geometry and orthographic-camera code *before* deleting; neither survives as-is.

### 2.5 Correction on the MiddleVR config path

An earlier read of mine was wrong. There is no dead path — there are **three live paths, none of them the in-repo `.vrx`**:

| Asset | `EditorConfigFile` | On disk |
| --- | --- | --- |
| `Prefabs/Rig/MVRManager Variant.prefab:73` | `C:\Users\Exx\Desktop\visbox\config\MiddleVR\MVR.vrx` | exists |
| `Scenes/CAVE/Stadium-3D-Football.unity:2436` (override) | `C:\Users\Exx\KostageProjects\Sit-Rep-CAVE\…\MVR.vrx` | exists |
| `Scenes/CAVE/Boot/BootMiddleVR2.unity:1687` (build-enabled) | `C:\Users\Exx\KostageProjects\VR-Demo\…\MVR.vrx` | exists |

`Assets/_Project/Config/MVR.vrx` is **decorative — nothing loads it.** The real defect is that the calibration source lives in two project trees the split is meant to sever. M0 must repoint every one of these at the in-repo copy, or the new repo's geometry silently depends on a folder on the Desktop.

### 2.6 Other M0 facts

- **`Stadium-3D-Football.unity` is a decoy.** Byte-identical to `SituationRoom.unity` (both md5 `fd91fea6…`), 38 MonoBehaviour references of which 35 are on the drop list, still instantiating `Gun_MiddleVR`. Build the football scene fresh from `CAVE_Room` + `MVRManager Variant` + `MVR_URP Variant`.
- **LFS is trivial here.** 138 tracked objects, **zero under `Assets/_Project/`** — the keep set carries exactly one (the TMP font). The new repo starts at ~3–5 MB against the current 138 MB `.git`. Run `git lfs install --local` before the first `git add`.
- **`Packages/manifest.json` pins MiddleVR by absolute local path** (`file:C:/Program Files/MiddleVR2/…`). The repo is machine-bound; document it in the README rather than discovering it on a second workstation.
- **Layers free up.** After the drop, 6, 8, 9, 10, 12 and 13 join 14–31 — 23 free. Keep 11 `WandUI` (hard-coded in `WandPointer`/`WandButton`).
- **Rename the assemblies.** `Project.Runtime` → `Stadium.Runtime`; `Care.FeedAdapter.Tests` → `Stadium.Tests` (it holds all 50 test files, not feed-adapter ones, and its `Newtonsoft.Json.dll` precompiled reference becomes dead).
- `ProjectSettings.asset` still says `productName: VR Demo`.

---

## 3. Room geometry — and one thing that must be settled on hardware

`CaveRegionMap` is a ScriptableObject of named regions extended non-destructively via `EnsureRegion`. The football demo adds its own rather than resizing the Sit-Rep ones:

| Region | Center (local m) | Euler | Size (m) | Physical |
| --- | --- | --- | --- | --- |
| `FootballFront` | `(0, 1.111, 1.10)` | `(0, 0, 0)` | `3.50 × 2.19` | 3.556 × 2.2225 |
| `FootballLeft` | `(-1.75, ?, 0)` | `(0, -90, 0)` | `2.35 × 2.19` | 2.3876 × 2.2225 |
| `FootballRight` | `(1.75, ?, 0)` | `(0, 90, 0)` | `2.35 × 2.19` | 2.3876 × 2.2225 |
| `FootballFloor` | `(0, 0.02, 0)` | `(90, 0, 0)` | `3.50 × 2.19` | 3.556 × 2.2225 |

### 3.1 The side-screen convention is unresolved — check it before M3

Two independent reviews flagged the same thing, from different directions.

```
ScreenLeft   OrientationLocal = "0, 0,  0.707107, 0.707107"   → +90° about Z
ScreenRight  OrientationLocal = "0, 0, -0.707107, 0.707107"   → −90° about Z
```

A rotation about Z **cannot change a +Z screen normal**. As literally written, both side screens are vertical panels still facing *forward*, rolled 90° in-plane. A left wall needs a yaw about Y. And the `PositionLocal` y-coordinate is not noise:

```
1.11125  −  2.3876/2  =  1.11125 − 1.19380  =  −0.08255      (exact, to five decimals)
```

The y-centre is **derived from the side screen's `Width`, not its `Height`** — which suggests the exporter treats the 2.3876 axis as *vertical* on the side screens, i.e. they are stored rotated 90° in-plane relative to the table above.

Both `MVR.vrx` and `MVR_Experimental.vrx` carry identical values, so this is deployed config, not a typo — there may be a MiddleVR `Screen` convention (note the unexplained `OriginX` / `OriginZ`) that reconciles it. What is *not* in doubt: the dimensions. Viewport aspects (2560×1600 = 1.60000, 1719×1600 = 1.07438) match the screen aspects to four decimals, so whoever authored this knew the room.

> **Action, ~20 minutes on the hardware, before M3 exists.** Put a labelled test card — large arrow plus per-wall text — on all four surfaces and walk the corners. If the side walls render rotated or the seams misalign, you have found it before building a milestone on top. Panels are placed from `CaveRegionMap.asset`, never from the `.vrx`, so **no desk test catches this.**

Get this wrong and both team walls show a 90°-rotated, wrong-aspect image with a 1.19 m vertical error.

### 3.2 Two smaller geometry consequences

- **Seams.** 3.50 vs 3.556 is a 28 mm inset per side; 2.35 vs 2.3876 is 18.8 mm. On a demo whose premise is a wraparound stadium, a 2–6 cm dark band at every wall junction is the first thing anyone sees. Either go true full-bleed or accept a deliberate bezel.
- **Standoffs must match.** The draft had the front panel 11.2 mm proud of its screen and the sides 28.0 mm. Different standoff per wall means three different apparent depths and corners that will not converge. Pick one number for all four.

### 3.3 Two existing behaviours to know about

- **`RegionPanel` does not size anything.** `RegionPlacement.WorldSlot()` returns a `Pose` — position and rotation only. **Nothing reads `CaveRegion.Size`** except `GetLocalSlot`'s grid maths. "Correct-aspect wall panels" is entirely new code; the region system gives you *where*, never *how big*.
- `RegionPanel.m_HasTarget` never clears, so every panel calls `SetPositionAndRotation` every frame forever. Harmless, worth fixing while you are in there.
- **Wire the region root explicitly.** Every `RegionPanel` in the current scene has `m_RegionRoot: {fileID: 0}` and silently falls back to `transform.parent`.

### 3.4 Stat panels sit *at* the wall plane

The draft put stat canvases ~2 cm proud of the video. In stereo at 1–2 m viewing distance, text at 20 mm disparity over a zero-disparity background is a known comfort problem. Put the stat canvases **at zero disparity, coplanar with the video panel**, and separate them by contrast and a drop shadow instead of depth.

---

## 4. Render architecture

### 4.1 Where the pitch lives

One root `PitchWorld` at world `(0, −1500, 0)`, everything under it authored in **local** coordinates with the 50-yard line at local origin: +X = long axis (goal lines at ±45.72 m, back of end zones ±54.864), +Z = across (sidelines ±24.384), turf at Y = 0. Local authoring keeps every camera-framing formula unit-testable with no offset in it.

The large negative Y is deliberate: it puts the pitch beyond the display cameras' `Far="1000"` as a second, independent guarantee behind the culling mask.

**Isolation is belt-and-braces:**
- *Pitch cameras* use an explicit **allow-list** of `Pitch*` bits only. Nothing in the room is in any pitch camera's mask — not the CAVE shell, not the stat canvases, not the wand reticle, and **not the four RT panels**, which structurally prevents both picture-in-picture of neighbouring walls and the infinite-mirror feedback loop of a camera seeing its own output.
- *Display cameras* get `cam.cullingMask &= ~PitchMask` from a new `PitchIsolation` component modelled line-for-line on `FrontWallPortal` — including its coroutine `Start` that polls up to 10 s, because `MVRManager` builds the display cameras in its own `Awake` and ordering is not guaranteed.

Note `TopDownMapCamera` currently does `cullingMask = ~LayerMask.GetMask(m_ExcludeLayers)` — *everything except*. That inversion is exactly wrong here; the four new cameras need allow-lists.

### 4.2 Layers

| # | Layer | Contents | Seen by |
| --- | --- | --- | --- |
| 14 | `PitchShared` | Turf with lines/numbers/hashes baked into the texture, goalposts, LOS and first-down lines | all 4 pitch cameras |
| 15 | `PitchPlayers` | Full-colour un-dimmed markers, both teams | Front + Floor |
| 16 | `PitchRightWall` | Per-player "dressing" for the home wall: home players own-material + focus ring, away players ghosted | Right only |
| 17 | `PitchLeftWall` | Mirror of 16 for the away wall | Left only |
| 18 | `PitchTactical` | Route polylines, coverage shading, down markers — the broadcast cams must never show these | Floor only |
| 19 | `PitchStadium` | Bowl shell, crowd cards, floodlights, sky dome | Front + Left + Right |
| 20 | `CaveScreen` | The four RT panels, bezels, and their wall-pick colliders | all 8 display cameras |

The 15/16/17 split is the compression that matters: each player root carries exactly **three** renderers (broadcast, left-wall, right-wall dressing), and "dim my opponents" becomes a culling decision rather than per-frame material swapping.

### 4.3 Cameras and RenderTextures

Match the RTs to the projector viewports exactly — the `.vrx` gives both physical size and viewport, and they already agree:

| Wall | RT | Camera |
| --- | --- | --- |
| Front | 2560 × 1600 | Broadcast: sideline boom, tracks the ball with lookahead + damping, frames LOS and carrier |
| Left | 1719 × 1600 | Away iso: sideline on that team's side, zoom solved for **life-size** focused player |
| Right | 1719 × 1600 | Home iso: mirror of left |
| Floor | 2560 × 1600 | Orthographic, **1:22, field width across the 2.2225 m axis**, window sliding with the ball |

1719 is odd and non-power-of-two; keep it. RenderTextures need no POT, and matching the viewport means the panel samples 1:1 with no resample. The front wall subtends ~89° at 2560 px = 28.8 px/deg — already well under acuity, so anything less is visible softening on yard lines. Expose one `m_ResolutionScale` knob (default 1.0) for the perf fallback.

Descriptor: `R8G8B8A8_SRGB` (the URP config has `m_SupportsHDR: 0`), `D24_UNorm_S8_UInt`, `msaaSamples = 4`, no mips, bilinear, clamp.

**The floor camera needs a two-axis extent.** `TopDownMapCamera` has a single `m_ExtentMeters` and a square RT — it cannot express a 1.600 frame. That is a port, not a reuse.

**Avoid the stretch bug.** `DroneFeedWindow.prefab` shows a 1.333:1 RT in a 2.256:1 rect with no `AspectRatioFitter` — a 1.69× horizontal stretch. With players on a pitch that would be unmissable. Drive panel dimensions from the RT aspect in code.

### 4.4 Perf — arithmetic, not estimates

| | px/frame |
| --- | --- |
| Display cameras, mono (2×2560×1600 + 2×1719×1600) | 13,692,800 |
| × 2 eyes (`Stereo="1"` on all four viewports) | **27,385,600** |
| Four pitch RTs at 1:1 | +13,692,800 |
| **Total** | **~41.1 MPix** |

That is 4.95× a 4K frame every frame, **12 full URP render loops** (8 display + 4 pitch), at `m_MSAA: 4` — roughly 164 M raster samples per frame. All four viewports are `ClusterNode="0"`: **one PC**.

*No millisecond figures appeared here for three milestones on purpose.* The pixel counts are arithmetic on the `.vrx` viewport rects; anything in ms would have been fabricated without a GPU. **Measure with a stress scene**, not at M7 when the art is locked. Dropped frames in a stereo CAVE cause real discomfort, so this is a comfort issue, not just a perf one.

#### Measured at M4 — `FrameBudgetProbe`

`Stadium3D ▸ Measure ▸ Frame Budget`, or `-executeMethod Stadium3D.EditorTools.FrameBudgetProbe.MeasureBatch`.
300 timed frames of play 6 — four verticals, everything on the field moving — after 30 discarded, on an
**NVIDIA Quadro RTX 4000** at the shipped 4× MSAA.

| per frame | median | min | p95 |
| --- | ---: | ---: | ---: |
| CPU — sim, markers, cameras, trails, numbers | **0.20 ms** | 0.19 | 0.23 |
| GPU — 12 loops, 41.1 MPix, one sync | **8.82 ms** | 8.45 | 9.49 |
| **total** | **9.02 ms** | | |

**Against 16.67 ms it fits with 7.65 ms spare — 54% used.** 60 Hz is the right line: both eyes of all four
surfaces are already inside that 8.82, so a 120 Hz active-stereo wall delivering 60 Hz stereo pairs is
what this number answers. Confirm the projector's actual mode on hardware alongside the test card.

*What the probe measures and what it does not.* The four pitch cameras are the real ones out of the built
scene at their shipped resolutions — that quarter of the frame is measured. The eight display loops cannot
be: MiddleVR builds those cameras at runtime, so they are stood in for by a camera in the room carrying
the mask `PitchIsolation` leaves, at the `.vrx` viewport sizes, twice per surface for two eyes. **Desk GPU,
not the CAVE PC**, and no MiddleVR in the process. Read the display half as a floor on the cost.

Two things in the breakdown are worth keeping:

- **The floor is the expensive surface** — 1.40 ms against 1.23–1.31 for the walls — and it earns it: 78
  renderers in frustum against 14–25, because top-down sees all 22 players, every marking, both trails and
  the state lines at once. The display proxies see **2** renderers each, a wall and its panel, so that half
  of the frame is pure fill rate exactly as the arithmetic predicted.
- **The CPU side is 0.20 ms with a p95 of 0.23**, for the whole per-frame chain. That is the bake rather
  than integrate decision showing up as a number: nothing in that path scales with how long the play has
  been running, so the figure is a constant rather than a curve, and M5's panels inherit the headroom.

*The probe found a settings split, which is the part worth acting on.* The project carries two URP assets
and the quality levels divide between them: **Low → `Performance URP Config`** (HDR off, main-light and
soft shadows off) is `m_CurrentQuality`, so it is what the editor, Play mode and the run above use — while
**Standalone defaults to Ultra → `Quality URP Config`** (HDR on, main-light shadows on at 4096², soft
shadows on). A built player would therefore not be the thing that was measured, which is the same shape as
the Android-renderer bug: correct in the config you are looking at, wrong in the one that ships.

Measured rather than assumed, the cost of that is **+0.77 ms — 9.79 ms total, still 59% of a 60 Hz frame**,
and the reason it is that small is a defensive line written in M2: `StadiumCaveBuilder` sets
`light.shadows = LightShadows.None` at the source and every pitch renderer has casting off, so a pipeline
that supports shadows still finds nothing to cast one. Both URP assets also point at **Standalone Preset**
now, so the Native RenderPass smearing cannot return in a build either. **Pin the level for builds anyway** —
not because 0.77 ms matters, but because a demo that ships on a different pipeline from the one that was
measured has an unmeasured budget again the moment anything changes.

**The one certain win:** `m_ShadowDistance: 2.5` with `m_MainLightShadowmapResolution: 4096`. The pitch cameras stand 15–45 m from their subjects and receive **zero** shadowed pixels, yet URP still runs `MainLightShadowCasterPass` per camera and still rasterises a 4096² map. Turn `m_MainLightShadowsSupported` off entirely, reclaim the shadowmap, and use blob-shadow quads under the markers — which read better top-down anyway.

---

## 5. The simulation

Pure C#, unit-testable off the Unity run loop, matching the project's existing `Tick(double dt)` + MonoBehaviour-driver split.

### 5.1 The core idea: bake, don't integrate

`SimulatedFeedSource.Advance()` — the closest existing analogue — has four defects, and every one is caused by integrating state forward per frame:

| Defect | Consequence | Replacement |
| --- | --- | --- |
| `travel = Speed * dt` from frame one | bodies at full sprint on the first frame after the snap | forward/backward trapezoidal speed solve with `v[0] = 0` |
| `Quaternion.LookRotation(dir)` every frame | facing snaps in one frame on a cut | turn-rate-limited heading baked per sample, seeded from the authored **pre-snap** facing — a lineman in his stance faces the defense, not his first step |
| `Seg = (Seg + 1) % Loop.Length` | infinite patrol, no terminal state | `MotionTrack.Duration`; `Sample(t ≥ Duration)` returns the last sample forever. The loop lives in `DriveDirector` as an explicit transport decision |
| `if (dist <= 1e-4f) { Seg++ }` | a body standing **on** its first waypoint silently loses it — **exactly the pre-snap case for all 22 players** | `RouteBaker` merges co-located waypoints into a dwell; `BakedPlay.PoseAtSnap(id)` is contractually bit-exact with the authored alignment. This gets its own named regression test |

Routes bake once at load into fixed-rate `MotionTrack` arrays of `MotionSample` (position, heading, speed, cumulative distance, running peak — **baked fields, not accumulators**). Everything downstream *samples by time*.

That single decision buys determinism, replay, loop, and slow motion for free: rewinding a play is assigning `t = 0`, and `timeScale` is a multiplier on `dt`. No state to unwind.

### 5.2 Type sketch

```
FieldGeometry (static)      yard ↔ metre, hashes, spots — the unit boundary; the sim runs in metres
RouteWaypoint               what a designer authors: yard line + lateral yards, so it reads like a play diagram
MotionLimits                top speed, accel, decel, lateral grip, max turn rate
RouteBaker (static)         waypoints + limits → MotionTrack. Replaces Advance() entirely
MotionTrack / MotionSample  immutable fixed-rate track; Sample(t) clamps, never wraps
BallEvent / BallState       authored custody changes; BallSolver derives state as a pure function
                            (held → carrier pose + hand offset; in flight → closed-form parabola)
PlayScript → BakedPlay      formation, 22 PlayerScripts, ordered BallEvents, authored PlayOutcome
PlayValidator (static)      monotonic events, every Throw has a Catch/Incompletion, outcomes land on
                            the intended down & distance — stops a designer silently breaking the demo
GameSituation               immutable value: down, distance, LOS, possession, score, quarter, clocks
GameStateProjector          pure fold (situation, outcome) → next situation
DriveScript / DriveLedger   the drive; ledger precomputes the situation at every snap by folding once
DriveDirector               the transport — owns five scalars, never touches a pose or a stat.
                            Huddle blend, loop, pause, replay, timeScale
BoxScore / TelemetryBuilder read models for the stat panels
FootballSim                 the façade; the only thing the Unity layer talks to
PlayerMarker : IPlayerMarkerView   the swap seam — stylized now, humanoid later
```

Authored content lives in `PlayAsset` / `DriveAsset` ScriptableObjects a designer edits in the Inspector; the tick logic never touches Unity.

---

## 6. The drive

**Bayside Ironhawks (home, offense) vs Northgate Sentinels (away, defense).** Q2, Ironhawks trail 10–14, ball on their own 35 after a punt return. Six snaps, five official plays, 65 yards, 2:58 elapsed. Fictional teams and players throughout.

| # | Situation | Call | Result |
| --- | --- | --- | --- |
| 1 | 1st & 10, BAY 35 | Gun Split Right, inside zone right to RB #26 Pruitt vs nickel over front, Cover 3 sky | **+6** |
| 2 | 2nd & 4, BAY 41 | Gun Trey Right, play-action **dagger** — #83 clears the deep third, #17 digs behind the vacated hook | **+14, FIRST DOWN** |
| 3 | 1st & 10, NOR 45 | Gun Trips Right, 5-man slide vs nickel cross-dog; DE #91 Grant beats LT #74 around the edge | **SACK, −7** |
| 4 | 2nd & 17, BAY 48 | RB slip screen right, nominally +9 — RG #68 flagged **offensive holding** behind the LOS | **−10, down replayed** |
| 5 | 2nd & 27, BAY 38 | Deep over to #17 at 20 yards | **INCOMPLETE**, broken up by FS #31. Clock stops |
| 6 | 3rd & 27, BAY 38 | Gun Trips Right, **four verticals** vs 2-Man Under — trips seams widen the boundary safety, backside X #11 Colston stacks CB #24 on a go | **62-yard TOUCHDOWN** |

Final: BAY 17 – NOR 14.

Every play is authored waypoint data with per-player alignment in yards relative to the ball, assignment, top speed, and a beat timeline in seconds from snap (snap 0.0, drop depth, throw, catch, tackle). Play 6 — 2.4 s of hang time, catch at the NOR 16, 16 YAC — is the hero play the §1.3 slow-motion beat is built around.

The expected box score is authored alongside, so **the stat panels have a verifiable target** rather than whatever the code happens to produce.

> **Author 2–3 drive variants** (or randomise the non-critical beats). Booth staff will watch this 200 times a day; the same holding penalty every 100 seconds becomes a running joke by noon.

---

## 7. Wand and interaction

Dropping the mission content frees **all six buttons** — the Sit-Rep scene had exactly one free index, with `ControlCenterManager` and `MiddleVRWandMatchRestart` colliding on button 4.

| Control | Binding |
| --- | --- |
| `Button0` (trigger) | Claim the pointed-at wall; also clicks any `WandButton` chip |
| `Button1` / `Button2` | Previous / next player on the claimed wall's team |
| `Button3` | Toggle stat panels |
| `Button4` | Pause / resume |
| `Button5` | Replay last play (slow motion) |
| Stick horizontal | Alternative prev/next, auto-repeating via `StickDirectionLatch` |
| Stick vertical | Cycle the claimed wall without pointing |

### 7.1 Three notes that change the implementation

**Wall picking is a new subsystem, not wiring.** `WandPointer.Pick()` only ever returns `m_Hits[i].collider.GetComponentInParent<WandButton>()` — pointing at a *wall* returns null. Wall selection needs its own ray, its own `CaveScreen` mask, and selection-outline rendering across four surfaces. `WandPointer.m_Mask` (layer 11, `WandUI`) must stay untouched so stat chips keep working.

**Render the ray always.** Today the reticle is only parked when a `WandButton` is hit, so a visitor waving the wand at a wall gets **zero feedback** until they accidentally land on a registered collider. A visible beam plus a wall-plane cursor, always on, or point-and-trigger does not exist as a discoverable affordance.

**`HeadNode` is tracked with yaw.** You already know which wall the viewer is facing, so *default* wall focus to head yaw and let the trigger override it. Pointing at a wall you are already looking at is redundant work for a first-time user.

### 7.2 M6 is more desk-verifiable than it looks

`UnityGamepadOperatorInput` implements `IOperatorInput`, and `OperatorInputRegistry.Register(OperatorRole.Primary, …)` takes precedence over `WandInput.Shared` in `WandPointer.Input`. **Registering a gamepad as Primary makes most of M6 testable without the CAVE** — only the ray geometry genuinely needs the wand.

---

## 8. Stat panels

Front wall subtends **116°** horizontally at 1.11 m. Comfortable single-glance vision is ~60° — the central **1.28 m of 3.556 m**. **64% of the front wall is peripheral**, so anything in the corners will not be read.

| Surface | Content |
| --- | --- |
| **Front, central 1.28 m** | Down & distance, yards to go, game clock. Large. That is all |
| **Front, periphery** | Score, quarter, possession, ball-on — present but not load-bearing |
| **Team walls, result beat** | Focused player card: number, name, position, ht/wt, position-appropriate stats |
| **Team walls, live** | **Speed mph, top speed this play, distance covered, separation from nearest defender, route run** |

Team comparison (total yards, pass/rush split, first downs, 3rd-down conversions, TOP, turnovers, penalties) is authored in the ledger and available, but it is broadcast furniture for a fan who already knows the sport. Show it during the huddle beat, not during live action.

**Promote the live telemetry.** "Separation 1.2 m" beside a life-size receiver is the genuinely novel thing this system can show, and it was buried at the bottom of the list. It is the answer to "why not just watch TV."

---

## 9. Milestones

| # | Milestone | Verifiable when |
| --- | --- | --- |
| **M0** ✅ | Repo split — fresh directory by file copy, 3 compile fixes, `.vrx` paths repointed, assemblies renamed | **Done** — `Stadium-3D-Football`, commit `33417a3`, 297 files, 106 assertions. First Unity open still to be run |
| **M1** ✅ | Sim core, pure C# — field math, bake pipeline, game state, drive content, `timeScale` first-class | **Done** — 271/271 EditMode assertions green in 2022.3.9f1, including the drive acceptance suite |
| **M2** ✅ | Pitch — field from an Editor builder, 22 markers + ball, layers, blob shadows, main-light shadows off | **Done** — 283/283 green; field and drive verified by headless renders from the broadcast angle |
| **M3** ✅ | Four cameras → four correct-aspect panels; `PitchIsolation`; floor at 1:22 two-axis ortho | **Done** — 303/303 green; all four surfaces rendered and inspected. Side-wall regions still unverified against hardware |
| **M3.5** ✅ | **One life-size player, real geometry, correct stereo, standing in the room** | **Done, and switched off.** It answered the go/no-go; the figure itself was a placeholder, not content. Getting there took four defects no test in the suite could see — see *M2–M3.5 as built* |
| **M4** ⏳ | Overlays — LOS, first-down line, route trails, focus rings, per-wall dim/ghost, stadium bowl. Side walls solved for life-size framing | **Built, not signed off** — 334/334 green, all four surfaces captured and inspected; `Players` is out of both side masks now that the dressing renderers exist. Held open by the side-screen test card on hardware |
| **M5** ⏳ | Stat panels, coplanar with the video, toggleable | **Built and checked** — 361 green, including an acceptance suite that reads the strings off the built labels and compares them to `ScriptedDrive.Expected`. Front wall and both team cards done; the result-beat/live split and any panel the wand needs are what remain |
| **M6** | Wand — wall picking subsystem, always-on ray, head-yaw default focus, transport | Gamepad-as-Primary at the desk; wand on hardware |
| **M6.5** | **Attract / idle** — stale-tracker fallback, docent reset, drive variants, seamless loop join | Runs unattended for an hour without degrading |
| **M7** | Polish — field lighting, region calibration on hardware, perf pass, pacing | Runs at target frame rate in the CAVE |
| **M8** | 3D miniature floor — the stereo payoff | Depth reads correctly standing in the room |

### 9.1 Ordering corrections folded in

- **M2 cannot be verified in the CAVE scene.** `SimDrone.cs:84` records it verbatim: *"MiddleVR disables every scene camera whose targetTexture is null at startup."* With `MVRManager` present there is no Game-view camera at all. M2 therefore needs a **separate MVR-free scene** — which is good practice anyway and is now an explicit deliverable.
- **The layer set is designed in M2 from M3's constraints**, not discovered in M3. The rule that fixes it — every pitch camera must exclude all four wall panels including its own — belongs in the layer table before any camera exists.
- **Perf is M2, not M7.** Undersizing RTs later to make the budget fit reintroduces exactly the softness class of bug §4.3 swears off.
- **M8 inverts M3's isolation** (the miniature must be visible to the display cameras that M3 culls). Reserve the stereo-pitch layer in M3 even though nothing uses it, or M8 costs a re-layer.

### 9.2 M6.5 exists because demos die in the idle state

A grep across `Assets/_Project/Scripts` finds **no attract, idle, or reset logic anywhere**. This is the state the booth spends most of its day in.

- **Nobody is wearing the head tracker.** There is a single `ART.BodyTracker0`. Set down on a table at 1 m pointing sideways, MiddleVR keeps solving off-axis projections from that pose and **all four walls go visibly skewed** to everyone walking past. Fix: if the tracker is stale > 2 s, clamp the head to a nominal standing pose at room centre (≈ 0, 1.6, 0) and ease back when it returns. ~30 lines protecting most of the booth's uptime.
- **Stereo with no glasses.** All four viewports are `Stereo="1"`; passers-by see doubled images. Attract should drop to mono or near-zero parallax.
- **State left dirty** — panels in whatever toggle state the last visitor left, focus on some random lineman. Attract resets to known-good.
- **The loop seam.** TD → reset must be invisible, or every 100 seconds the room announces that it's a video.

One thing already handled: `ProjectSettings.asset` has `runInBackground: 1`.

---

## 10. Open risks

| Risk | Standing |
| --- | --- |
| **Side-screen orientation in `MVR.vrx`** | Unresolved. `1.11125 − 2.3876/2 = −0.08255` exactly, and a Z-rotation cannot produce a side wall. M3 shipped at the desk with the side regions as authored guesses, so this now gates **M4 sign-off** rather than M3 start. **Test card on hardware, 20 minutes.** No desk test catches it |
| **Frame budget** | **Measured at M4 — 9.02 ms of a 16.67 ms frame, 54% used** (§4.4). M2's stress test had run before the render chain worked end to end, so it measured less than it appeared to; `FrameBudgetProbe` now measures all twelve loops on real hardware and is re-runnable per milestone. Two caveats stand: it is the desk GPU rather than the CAVE PC, and the eight display loops are a proxy with no MiddleVR in the process |
| **A build would not be what was measured** | **Closed.** Standalone defaulted to quality level Ultra → `Quality URP Config` while the editor sits on Low → `Performance URP Config`; costed at **+0.77 ms**, so never a cliff — the scene turns shadows off at the source regardless — but `m_PerPlatformDefaultQuality: Standalone` is now `1`, so a build ships on the pipeline that was measured. The level is still *named* "Low", which reads oddly in a quality dropdown and is the only thing left to tidy |
| **M1–M7 are flat** | **Still standing.** M3.5 proved the room can do better than four televisions, but with a placeholder that is now switched off. The four walls stay flat pictures by construction, and the answer does not actually ship until M8 |
| **Build-time state that does not serialize** | **The dominant defect class so far — three instances in one milestone.** A builder sets something in memory, an offscreen render or an EditMode test confirms it, the scene is saved, and the state is gone. Neither kind of test touches a save/load round trip. Rule: anything a builder sets must be checked *after* a scene reload |
| **MiddleVR clones a template camera** | `GrabTemplateCamera()` takes `Camera.allCameras[0]` and `Object.Instantiate`s that whole GameObject — components included — once per display camera. Any camera added to a CAVE scene is a candidate. `MVRTemplateCamera` at depth −1000 now claims the role; do not remove it |
| **Pitch masks are subsets of `PitchMask`** | Structural, and it turns one mistake into a black room: `PitchIsolation`'s `&= ~PitchMask` takes any camera carrying a pitch mask to exactly `0x00000000`. Guarded, not eliminated |
| **`.vrx` lives outside the repo** | Three live paths into two foreign project trees. M0 must repoint them all |
| **MiddleVR pinned by absolute path** | `Packages/manifest.json` binds the repo to one machine. Document it |
| **Stop-start pacing** | Football reads to a newcomer as standing around punctuated by chaos. The slow-motion hero beat is the mitigation |
| **MiddleVR does not compile on 2022.3 unpatched** | `MVRManagerScript.cs` `#error`s on any Unity outside 2019.4–2022.1. The guard is commented out in `Library/PackageCache`, which is gitignored and regenerated — so the fix is lost on a clean clone. **The source Sit-Rep CAVE repo has the same hand-edit and the same exposure.** Needs a supported MiddleVR build or a self-healing Editor script |
| **M2 depends on an MVR-free scene** | Now explicit, was implicit |

---

## 11. Decisions taken

| Decision | Choice |
| --- | --- |
| The three amendments in §1 | **All accepted** — floor reoriented to 1:22 with no jersey identity, side walls solved for life-size framing, beat schedule with slow motion |
| Namespace | **`Stadium3D.*`** — swept in M0; `Care.*` and `VRShootingGallery` are gone |
| Repo | **Local only**, no remote. `C:\Users\Exx\KostageProjects\Stadium-3D-Football` |
| M3.5 room figure | **Placeholder — off by default.** `IncludeRoomFigure` in `StadiumCaveBuilder`. The component, its menu item and its tests stay; M8 is where room-scale geometry actually lands |

### M0 as built

Fresh directory by file copy — no `git init` in place, no mutating git command in the source
tree, so `Stadium-3D` and the git plumbing it shares with `Sit-Rep-CAVE` were never touched.

| | |
| --- | --- |
| Commit | `33417a3` on `main`, 297 files, 2.2 MB `.git` |
| Remotes | none |
| LFS | 1 object (the TMP font), as predicted |
| C# | 46 files, 106 test assertions |
| Assemblies | `Stadium3D.Runtime`, `Stadium3D.Editor`, `Stadium3D.Tests` |
| Namespaces | `Stadium3D.{CaveBox, Operators, Telemetry, Vr, EditorTools}` |

The three compile edges were cut as specified: `FrontWallPortal`'s telemetry import (with
`InteractionLog` kept, moved to `Stadium3D.Telemetry`), `MatchService` → `TargetBase` (`Core/`
and `HUDController` dropped entirely), and the two `CaveRegionMap` tests that exercised
`FeedWindowSpawner`. `Scripts/Rig/StationaryRigGuard.cs` dropped with the XRI package references
it was the only reason for.

`MVR.vrx` is now genuinely the calibration source — the `MVRManager` prefab and the boot scene
both pointed at copies in `Desktop\visbox` and `VR-Demo`. Layers are re-cut per §4.2, with
`PitchMiniature` reserved at 8 and `FrontWallPortal`'s reveal layer following `SimAirspace`
there so the component stays alive and meaningful.

`CaveRegionMap.asset` is trimmed to the four base surfaces; the `Football*` regions land in M3,
after the hardware check below.

### Next

1. **Open the project in Unity once.** Expect two missing objects in `BootMiddleVR2.unity`
   (`Target_Sphere`, `Gun_MiddleVR`) — delete them.
2. **Run the side-screen test card on the CAVE** (§3.1). It gates M3, and nothing at the desk
   substitutes for it.
3. **M2** — the pitch. Field builder, 22 stylized markers driven by the sim, the layer set designed
   from M3's constraints, main-light shadows off, and the frame-budget stress test.

### M1 as built

The sim is 18 files in its own `Stadium3D.Football` assembly with **no MiddleVR, XR or Input System
reference**, which makes "runs off the Unity run loop" structural rather than conventional.

| | |
| --- | --- |
| Tests | **271 / 271** passing, run headless in Unity 2022.3.9f1 |
| | 106 inherited CAVE assertions (unchanged by the M0 namespace sweep) |
| | 150 new component assertions across 8 fixtures |
| | 15 drive acceptance assertions |
| Content | 6 plays, 22 players, ~9k baked samples per play |

**The one idea it rests on:** bake the dynamics once, evaluate purely thereafter. Routes solve into
fixed-rate `MotionTrack`s at load; the ball is derived from its surrounding pair of `BallEvent`s as a
closed-form arc; the situation is a single fold `DriveLedger` performs at bind time. Per frame there
is no integration anywhere — so nothing accumulates, nothing drifts, and seeking backwards is exact.
Replay, looping and slow motion then cost nothing rather than each needing their own machinery, and
three tests pin exactly that: a seek must not depend on how the sim got there, a replay must
reproduce frame-for-frame, and lap two of the loop must equal lap one.

All four defects the design identified in the old `SimulatedFeedSource.Advance()` are now
structurally impossible, including the one that mattered most — a body standing *on* its first
waypoint used to silently lose it, which is the pre-snap case for all 22 players.

**A bug the acceptance suite caught that the component tests could not.** Play 4 baked to 19.8 s
against an authored 5.4. The slip screen has linemen turning back through a near-180° reversal; the
spline reports an almost-zero corner radius there, `sqrt(grip / curvature)` collapses toward zero,
and the time integration — floored at only 0.05 m/s — spent a full second on each 5 cm arc step.
Fixed by clamping the corner radius the curvature caps can see to 0.5 m, flooring the
curvature-derived speed cap at 18% of top speed (applied *before* the authored constraints, so
`Dwell` and `Stop` still genuinely stop), and raising the integration floor to 0.35 m/s. A
`BakeReport` warning now compares a route's duration against what its path length can justify, so the
next instance surfaces at authoring time. Every component test was green throughout — only running
the shipped content through the baker exposed it.

**Two decisions worth knowing about.** `BakedPlay.Duration` is derived, never authored, which kills
the "designer typed 3 s for a 40-yard route" bug class but means a play cannot be pinned to an exact
length. And `GameStateProjector` is deliberately not a rules engine — it handles the ordinary cases
and yields to an authored override, which is right for a scripted drive verified against an expected
box score and a liability the moment anyone wants free play.

Note that field position gained (65 yd) and box-score total yards (75 yd) legitimately differ,
because a penalty moves the ball without being charged as offensive yardage. Both are pinned, and a
test asserts they *do not* match so the gap does not get "fixed" later.

### M2–M3.5 as built

M2 built the field and 22 markers behind an Editor builder; M3 hung four cameras and four panels on
the CAVE walls; M3.5 put one life-size figure in the room. Every milestone was green at the desk —
283/283, then 303/303 — and every one of them shipped a defect the suite could not have caught.

| | |
| --- | --- |
| Scene | `StadiumCave.unity`, 220 GameObjects, pitch parked at −1500 m |
| Cameras | 4 pitch rigs → 4 RenderTextures; front and floor 2560×1600, sides 1719×1600 |
| Masks | Broadcast `0x0008C000`, Home `0x0029C000`, Away `0x002AC000`, Floor `0x0004C000` |
| Display | 8 MiddleVR eye cameras at `0xFFD03FFF` — everything except the pitch |

#### The defect class that cost the most: state that does not survive a save

Three separate bugs, one shape. A builder establishes something in memory, it is verified, the scene
is written to disk, and the state is silently dropped.

- **`PitchCameras.cs` declared four MonoBehaviours in one file.** Unity creates a `MonoScript` only
  for the class whose name matches the filename, and no class `PitchCameras` existed — so none of the
  four rigs could be serialized into a scene at all. `AddComponent<T>()` worked at build time, which
  is exactly why it survived review: `WallCapture` used those live objects and produced eight
  correct-looking wall renders from cameras that could never persist. The saved scene had four camera
  GameObjects carrying no components.
- **`PlayerMarker.m_Identity` was a plain field with no `[SerializeField]`.** Bound at build time,
  null after every reload. The driver registered none of the 22 markers, so 22 correctly built bodies
  stood on the field while nothing ever moved them.
- Both were invisible to the two things that were being run. An EditMode test and an offscreen
  capture each work with live in-memory objects; **only a save/load round trip exercises the failure.**

The fix pattern is worth keeping: serialize the *key*, resolve the object at load. `PlayerMarker` now
stores a roster id and `FootballSimDriver` re-binds against `ScriptedDrive` in `Awake`, which also
stops the scene carrying a stale duplicate of the roster.

#### MiddleVR duplicates a template camera, components and all

`MVRManagerScript.GrabTemplateCamera()` takes `Camera.allCameras[0]` and `MVRTools.CreateNodes`
calls `Object.Instantiate` on that entire GameObject once per display camera. The moment the pitch
rigs became the only cameras in the scene, all eight MiddleVR eye cameras came back as clones of a
`PitchCameraBase`. Each then did three separate things that draw nothing: allocated its own
2560×1600 render target, pointed itself at that instead of at its wall, and carried a pitch culling
mask.

That last one is the trap worth naming. **Every pitch mask is a subset of `PitchMask` by
construction**, because both are assembled from the same layer set — so `PitchIsolation`'s
`&= ~PitchMask` took each cloned display camera to exactly `0x00000000`. One clone, and the room went
black: walls, floor and the room's own geometry together, with no error anywhere.

The builder now creates a bare `MVRTemplateCamera` and assigns it through
`advancedProperties.TemplateCamera` — set by serialized property, so the builder keeps no
compile-time MiddleVR reference — at depth −1000 so even the fallback path prefers it.
`PitchIsolation` repairs a display camera that arrives carrying a rig instead of blinding it.

#### The desktop CAVE was running the mobile renderer

`Performance URP Config` pointed at **Android Preset**, which sets `m_UseNativeRenderPass: 1`.
Native RenderPass is a tile-GPU optimisation; on desktop D3D11 the pass fails to open, and **a pass
that never begins never runs its clear load action**. Every wall composited on top of every frame it
had already drawn. Because the floor camera pans with the ball, static yard lines painted a fresh
stripe each frame and the floor became a barcode — which reads as *objects are spawning*, not as a
missing clear. Inherited from Sit-Rep, and it would have hit any render-texture-heavy scene.

`Performance URP Config` now uses **Standalone Preset**, and the render targets take their sample
count from `QualitySettings.antiAliasing` rather than a hardcoded `4`, so target and pipeline cannot
drift apart again. An MSAA mismatch here is not a quality problem but a correctness one.

#### A wall that was plausible and wrong

`AwayIsoCamera` was left at the default `TeamSide.Home`: two cameras at the same `z = −16` showing
the same team, on the two walls whose entire purpose is to show different ones. Nothing about the
image said so. It was caught by reading `mask=0x0029C000` in the diagnostic and recognising it as
`RightWallMask`, which is the argument for printing masks as hex rather than trusting the picture.

#### What actually found these

Three wrong guesses preceded the first real fix. `PitchDiagnostics` ended that: it installs itself at
runtime, prints the whole chain once — layers, driver, every pitch camera with its mask and render
target, every panel with its source and bound texture, every display camera with whether it can see
the panel layer — and deletes itself. `pitch cameras: 0` and `pitch cameras: 12` were each
unambiguous the moment they appeared. It costs nothing, needs no scene edit, and should stay.

The standing lesson across M1, M2 and M3 is now consistent: **the component suites have been green
through every one of these.** What found the bugs was running the real content through the real
pipeline — a bake report on shipped routes, an offscreen render, a save/load cycle, a Play-mode dump.

One more instance surfaced the moment the suite was finally run at 309. `RoomHologramTests` was
written during M3.5 and never executed, and its proportion check measured the whole hologram root —
so the 0.9 m ground ring stood in for the shoulder span and reported a correctly proportioned figure
as a pillar. The assertion was right, the scope was wrong, and the first failure masked a second
assertion that never got to run. **A fixture that has never been run is not a fixture that passes**,
and it costs nothing to keep the number honest by running it.

### M4 — built, not signed off

**The floor now says what down it is.** `FieldStateLines` solves the line of scrimmage and the line to
gain from a `GameSituation` and nothing else; `FieldStateOverlay` draws them on `PitchTactical`, which
only the floor camera carries. Both strips are drawn at three times a painted stripe, because the floor
frames 48.9 m across a 2.2225 m surface and a real 4-inch line arrives 4.6 mm wide.

The sign convention is the reason the solve is a tested pure function rather than two lines in a
renderer. Home drives up the yard axis and away drives down it, and getting that backwards produces a
clean yellow line across a field on the wrong side of the ball — invisible at 1:22, and instantly
visible to anyone who follows football. Seven assertions pin it, including one that runs both teams
from the same spot with the same distance and requires the answers to be mirror images.

**A bug the overlay work surfaced, in code that had shipped two milestones earlier.** The ball's ground
tick is a child of the ball marker, the marker had already been moved to the ball, and the tick was
then given the ball's field position a second time — so it sat at double its offset from midfield.
Nothing about that looks broken: the tick still slid around the field near the play, and the error grew
smoothly with distance from the middle, so it read as a deliberate lead indicator. It took drawing two
*correct* lines next to it to make a 1.5 m yellow square 6 m from the ball look wrong. Fixed, and pinned
by a test that measures where the tick lands relative to the ball rather than trusting one number.

**Per-wall identity is complete.** Each player carries three bodies, three focus rings and one number,
and every team wall sees exactly its own set: own team lit, opposition ghosted to alpha 0.28, its own
eleven numbered, and a gold annulus under the player that wall is following. `Players` and
`PitchNumbers` are both out of the two side masks. The ball, the chevrons and the blob shadows moved
to `PitchShared`, which now means *what every pitch camera renders identically* rather than "scenery".

Three findings from doing it, all of the same family — a picture that looks deliberate and is wrong:

- **A lift proportional to depth separates nothing.** Screen height goes as `(worldY − eyeY) / depth`,
  so `k · depth` contributes a flat `k` to near and far alike. It has to be measured against a
  reference — and not the mean depth, and not the nearest player on the field, but the nearest player
  *in shot*. Each wrong reference produced numbers that had visibly moved and were no less stacked.
- **The per-camera number pass gated on the `Players` layer**, which the side walls stopped carrying
  when they were dressed — so it skipped precisely the two walls that draw numbers. The capture tool
  hid it by calling the marker directly; only Play mode would have been wrong. Both paths now go
  through `JerseyNumbers.Orient`.
- **The focus ring was a filled quad.** It had never been visible, because nothing had ever focused a
  player, so nobody had seen that "ring" meant a solid 2.1 m gold slab across the yard lines under the
  body it was meant to draw attention to. It is an annulus now.

**The void above the far sideline is gone.** Framing at 2.5 bought a five-player group and, with it, a
black band across the top third of both team walls: the side cameras sit 3.4 m off the turf looking
almost level, so everything above the far sideline was the camera's clear colour. `StadiumBowlBuilder`
sweeps a rounded rectangle — four corner arcs, with the straight sides falling out as the spans between
them, so there is no special case for them anywhere — through a stepped cross section: low wall, ten
raked rows, a concourse, eight upper rows, a rim. 1476 verts, 2880 triangles, three materials, no
textures: seating is two close tones alternating down the steps, which reads as rows at forty metres
and, unlike a generated texture, is not one more asset that has to survive a scene save. The floor
deliberately does not see it — `FloorMask` omits `PitchStadium`, because from directly overhead a bowl
is a ring of clutter around the one view that has to stay legible.

Two of its failures are worth keeping, both of them large and both invisible as *errors*:

- **It rendered near-black at any albedo**, and two rounds of brightening the materials went straight
  past the cause. The sweep was wound outward, so every seat was lit from behind — and with two-sided
  materials nothing looked broken; it just looked like the void it was built to replace. The fix takes
  the cross product of the first triangle, dots it against that path point's inward direction, and
  reverses every winding if the sign is wrong. **Measured rather than reasoned about**, since reasoning
  about it on paper is what produced the bug.
- **A single flat slab on top read as a dark curtain** whatever colour it was painted, and the 12 m gap
  between apron and bowl showed the clear colour as a black strip pinned to the horizon. Both are the
  original failure again in miniature: a large flat area with no internal detail. Upper seating rows,
  and a skirt running back under the turf edge.

**Route trails are sampled from the bake, never accumulated.** The obvious implementation appends the
player's position every frame, and it would be wrong here three ways at once: doubled up under slow
motion, thinned out under a frame-rate dip, and surviving a seek — so scrubbing back would leave a
trail through places the player has not been yet. M1 bakes every route into a fixed-rate `MotionTrack`
exactly so that position is a pure function of time, so `RouteTrail` asks it 48 times between the snap
and now. Constant cost per frame, resolution independent of how long the play has run, and replay,
looping, slow motion and seeking all need no special case — the same argument the sim itself rests on.
The test that matters seeks backwards and requires the line to *shorten*; an accumulated trail passes
every structural assertion and fails that one.

Four of them: both teams on the floor, because seeing a receiver's route against the coverage is the
whole reason to look down, and one per team wall carrying only its own side — tracing the route of
somebody that wall has deliberately ghosted says the opposite of what the ghosting says. Tapered from
0.35 to full width so the geometry itself says which end is *now*. The capture path has to settle both
wall cameras before it can draw any of them, since the trails are per team while the cameras choose
per wall, and the floor draws both.

### M5 — the readouts

**The stats are drawn into the picture, not in front of it.** §3.4 wants them at zero disparity, coplanar
with the video, and the obvious build — a canvas in the room parked at the wall plane — makes coplanarity a
number somebody has to keep correct and buys z-fighting with the panel quad as well. Each readout is instead
a child of its own pitch camera, drawn by that camera into that surface's render texture: the same pixels as
the picture, so there is no second plane to misalign and no way for a later edit to introduce one. It costs
no extra render loop.

That needed exactly one new layer. The team walls already have `PitchRightWall` and `PitchLeftWall` for
carrying content nobody else sees, but every layer in `BroadcastMask` is shared with the floor — and a
readout three metres in front of the broadcast camera falls inside the floor's 48.9 m ortho box, where it
draws as text lying across the middle of the field. Hence `PitchHud`, front wall only.

| Surface | Central | Peripheral |
| --- | --- | --- |
| **Front** | down & distance (largest), ball-on, clock | score with a possession mark, quarter |
| **Team walls** | lower third: `#85 BOONE RASMUSSEN` / `TE · 6-5 · 252 lb` / stat line / assignment | one live telemetry line at the top |

**The builder declares fractions; the HUD applies them.** Position and size both come from the camera's
frustum, and `TeamIsoPitchCamera` solves its own field of view in `Awake` from the framing scale — so
anything measured at build time is measured against a frustum that no longer exists by the time anyone
looks. Formatting lives in `StatLines`, in the sim assembly: "position-appropriate stats" is a decision — a
quarterback's line is completions and rating, a receiver's is catches and a long — and decisions belong
where a test can reach them without a camera.

Three defects, each invisible as one:

- **Score and quarter were simply absent from the front wall.** TextMeshPro aligns inside its
  `RectTransform` and a fresh one is 20 units wide, so at wall font sizes `Left` and `Right` alignment put
  them half a rect off the side of the frame. The labels existed, held the right strings and reported
  sensible positions. The three centred labels were unaffected, which is why it showed up as two missing
  items rather than as a layout that looked wrong. A degenerate rect makes the anchor the alignment edge.
- **The route label sat exactly where M4 floats the jersey numbers** — and lifts them further to separate
  them by depth — so `BULL RUSH` landed on top of `94`. The top strip belongs to the numbers; the route
  reads perfectly well as the card's last line.
- **The scoreboard was wired to the wrong source**, and this is the one worth keeping. It read
  `GameSituation`, which is the state *at the snap*, so it lagged a play permanently: the drive ends on a
  62-yard touchdown and the wall would have read `BAY 10` while the receiver stood in the end zone, and
  would never have reached 16 at all. **Mid-drive the two sources agree exactly**, so every capture looked
  right. It reads the box score now, which updates in `PostPlay` — when a real scoreboard changes.

**That last one is what the acceptance suite is for.** M1 already asserts the ledger produces 2-of-3 for 76
yards and a touchdown; M5 asserts a viewer *reads* those numbers, which is a different claim. Everything
between the ledger and the label is somewhere a value can survive intact and arrive wrong: the wrong stat
chosen for a position, a label bound to the neighbouring field, a rounding, a card filled for the wrong
player. Each leaves the box score perfect and the wall wrong, and none of them look like a defect. So
`StatPanelAcceptanceTests` plays the drive out, reads the strings back off the built labels, and compares
them against the answer authored beside the drive.

### Next

1. ~~Run the EditMode suite~~ — **334/334 green** in 2022.3.9f1, up from 309. One failure found and
   fixed on the way: see the note above on `RoomHologramTests`.
2. ~~Commit~~ — M3.5, its switch-off and the render-chain fixes landed, and M4 has gone in on top in
   pieces since.
3. **M4's build work is done**: floor state lines, per-wall dressing, per-wall jersey numbers with
   depth separation, per-wall focus rings, the stadium bowl and route trails are all in.
4. **Side-screen test card on the CAVE** (§3.1) — the one remaining M4 gate, and still the only thing
   at the desk that cannot be substituted for. Twenty minutes on hardware.
4b. **M5's readouts are built and checked** (§8, above). What is left of the milestone is the result-beat
   versus live split — the cards currently draw both at once — and whatever the wand needs a panel for.
5. ~~Re-measure the frame budget~~ — **9.02 ms of 16.67, 54% used**, on all twelve loops with the bowl,
   six renderers per player and four line renderers in place (§4.4). `FrameBudgetProbe` is in the repo,
   so every milestone from here can be compared against this one rather than against arithmetic.
6. **Pin the build's quality level** to the pipeline that was measured, per the risk table.

**The M3.5 figure is settled: placeholder, off by default.** It was a probe, it answered its
question, and `IncludeRoomFigure` in `StadiumCaveBuilder` keeps it out of the built scene. The
component, its builder menu item and its six tests stay — they are the groundwork M8 stands on, and a
known life-size reference standing in the room is genuinely useful when checking the side walls.
Rebuilding the CAVE scene drops the eight objects it left behind.

**Framing settled at 2.5.** Built at true life-size first, because that is the claim the side walls
exist to make, and seen on the wall it delivers exactly what it promises and is still the wrong
default: one torso fills the surface, no field behind it, the numbers above the frame, and the
per-wall dressing invisible because no second player is ever in shot. A wall that cannot show a
receiver and the man covering him is not showing a play. At 2.5 the rendered height is
`1.88 / 2.5 = 0.75 m` — the arithmetic is `subjectHeight / framingScale` and does not depend on
distance — which is unmistakably a person, and buys a five-player group.
