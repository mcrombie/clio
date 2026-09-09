# Unity 6 / HDRP presentation starter

This folder contains a source integration starter, not an already configured Unity project or a validated Unity build. The Unity Editor is absent from the development environment used to create it. The simulation has executable tests; Unity compilation, rendering, input and standalone builds still require verification in an installed Editor.

The starter builds the simulation's actual 2,562-cell globe as one mesh with ten biome submeshes and shared `HDRP/Lit` materials. Shared corner heights keep the terrain watertight. It applies an oblate Y scale of `0.996647`, adds modest elevation relief, and shows the selected place and the player band's position. It includes globe orbit, zoom, selection and a simple action panel connected directly to `Clio.Simulation.Game`.

## Import into a real HDRP project

1. Install Unity 6 with Windows build support through Unity Hub. Create a project from the **High Definition 3D / HDRP** template. Open it once and allow its package imports and HDRP setup to finish. This repository does not install the Editor or choose a package version on your behalf.
2. From the Clio repository root, run the import script with the path to that existing project:

   ```powershell
   .\prepare-unity.ps1 -UnityProject 'C:\Projects\ClioUnity'
   ```

   The script copies the authoritative simulation `.cs` sources into `Assets/Clio/Simulation`, writes its engine-independent `Clio.Simulation.asmdef`, and copies this presentation folder to `Assets/Clio/Presentation`. It refuses to overwrite an existing `Assets/Clio` directory. Treat copied simulation files as imports; make rule changes in the repository's `src/Clio.Simulation` and deliberately refresh your Unity copy.
3. In Unity, wait for compilation. Confirm that HDRP is assigned in the project's Graphics and active Quality settings. `Clio.Presentation` references the simulation, Core RP and HDRP runtime assemblies. Its Editor assembly is restricted to the Editor platform.
4. Set **Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling** to **Both**, then restart the Editor if prompted. The starter intentionally uses legacy mouse polling to minimize integration setup. New production input should use Input System actions. If legacy input is disabled, the panel explains this; its buttons still provide basic command access.
5. Open a scene with your desired HDRP exposure/volume setup. Use **Clio > Create globe scene**. This adds one root containing `ClioGlobeView`, a camera and two directional lights. It preserves existing cameras, lights, volumes, pipeline settings and unsaved scene content. The action supports Undo; selecting the menu again selects the existing globe. It does not create or assign an HDRP pipeline asset.
6. Save the scene, select the new root and set the seed, band name, ancestry, language style and optional four-band start in the Inspector. Keep the root's scale at `(1,1,1)`; use its Radius setting to resize the globe. Enter **Play**. The world is generated on entering Play, so it does not appear as a baked mesh in Edit mode.
7. Before a standalone build, create a material using shader **HDRP/Lit** and assign it to **Terrain Material Template** on `ClioGlobeView`. This retains a referenced material/shader in the scene; a runtime `Shader.Find` alone can be stripped from builds. Leave the material opaque with no textures, displacement, alpha clipping or unusual render settings. The runtime clones it for biome colors and markers.

If importing manually, copy all three simulation source files and create an assembly definition named exactly `Clio.Simulation` with `noEngineReferences: true`. Copy this entire Presentation folder, including both assembly definitions and its Editor subfolder. Do not import the Windows native desktop presentation source into Unity.

## Controls and responsibilities

| Input | Behavior |
| --- | --- |
| Right mouse drag | Orbit continuously through either pole |
| Mouse wheel | Zoom between globe and regional distances |
| Left click | Select the nearest cell on the inverse-transformed ellipsoid |
| F / Focus band | Center the current band |
| Move | Call `Game.Move` for the selected adjacent land cell |
| Forage / Hunt / Befriend / Camp | Call the core action at the band's current location |
| End turn | Call `Game.EndTurn`, then refresh the display |

The left panel reports turn, season, actions, population, provisions, upkeep, selected terrain and recent chronicle entries. Selecting a remote place does not relocate foraging or animal interactions. The globe currently previews all geography; the panel distinguishes explored places. This is a map integration view, not the final fog-of-war design.

No movement, survival, domestication, population growth or linguistic rules are reimplemented in a `MonoBehaviour`. `ClioGlobeView.State` exposes the simulation state for future UI binding. `PickCell(Ray)` and `FocusBand()` are available for an Input System integration. Save/load and restart interfaces are not part of this small bridge; exit and re-enter Play to generate another story.

The component allocates one terrain GameObject, two selection line renderers and one player marker. It does not create a GameObject per cell. Animal and rival-band markers, instanced vegetation, animated water, culture/language overlays, atmospheric scattering, Shader Graph terrain blending and production UI remain to be implemented. The detailed art direction is in `docs/MAP.md`.

## First Editor validation

- Confirm the Console contains no compile errors and the intended HDRP asset is active. The script reports a clear error if HDRP or `HDRP/Lit` is unavailable; it does not silently substitute another rendering pipeline.
- Check the globe is visible, biome materials are opaque, relief has no holes, and both poles can be crossed by camera orbit. Adjust the scene's HDRP exposure if the template's existing volumes overexpose or darken the terrain. The starter lights follow the new camera; pre-existing lights remain active.
- Select adjacent land, move, forage and end a turn. Compare displayed population, food and chronicle changes with the core commands. Verify UI clicks do not also select cells behind the panel.
- Inspect selection close to coastlines and mountainous silhouettes. Current picking intersects the base ellipsoid, then chooses the nearest cell center; it is approximate around relief and polygon edges. Replace it with a mesh raycast or precise spherical-polygon containment when this becomes a production interaction.
- Use Frame Debugger and Profiler to inspect draw calls, material setup, generated mesh memory and camera/input costs. Verify selection rings render correctly with the retained HDRP/Lit material.
- Build a Windows development player with the retained template material assigned, then test shader inclusion, controls and scene loading outside the Editor.

These checks are pending. Documentation/API review and passing simulation tests do not establish Unity visual correctness or player-build support.

## Documentation references

The integration follows Unity's [mesh submesh API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetTriangles.html), [HDRP Lit material model](https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Lit-Shader.html), and [HDRP camera data](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/api/UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.html). Unity 6 moved light intensity and unit settings onto `Light`; the scene utility uses those properties rather than the deprecated HDRP setters described in [HDAdditionalLightData](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/api/UnityEngine.Rendering.HighDefinition.HDAdditionalLightData.html). The [Unity input documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/Input.html) recommends the Input System for new production input.
