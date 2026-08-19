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

*No millisecond figures appear here on purpose.* The pixel counts are arithmetic on the `.vrx` viewport rects; anything in ms would be fabricated without knowing the GPU. **Measure at M2 with a stress scene**, not at M7 when the art is locked. Dropped frames in a stereo CAVE cause real discomfort, so this is a comfort issue, not just a perf one.

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
| **M3.5** | **One life-size player, real geometry, correct stereo, standing in the room** | Go / no-go on the whole concept — before stats, before overlays |
| **M4** | Overlays — LOS, first-down line, down markers, route trails, focus rings, per-wall dim/ghost. Side walls solved for life-size framing | Floor is legible; markers track game state |
| **M5** | Stat panels, coplanar with the video, toggleable | Values match M1's expected box score |
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
| **Side-screen orientation in `MVR.vrx`** | Unresolved. `1.11125 − 2.3876/2 = −0.08255` exactly, and a Z-rotation cannot produce a side wall. Blocks M3. **Test card on hardware, 20 minutes.** No desk test catches it |
| **Frame budget** | 41.1 MPix and 12 render loops per frame is arithmetic. Cost in ms is unknown and deliberately unestimated. Measure at M2 |
| **M1–M7 are flat** | A skeptic will ask why this isn't four televisions, and until M3.5 they are right. That is why M3.5 exists |
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
