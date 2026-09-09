# The living atlas

Clio's native desktop presentation now treats the map as an illustrated natural-history atlas: warm paper-colored lettering and engraved gold details surround a landscape of jade woodland, ochre grass, blue-green water, and pale stone. The intent is to make exploration and ordinary play feel inviting while keeping the simulation legible during development.

## Civ IV reference pass

The supplied reference informed stronger terrain depth, visible people under standards and a clear distinction between the commanded unit and inspected land. Regional bands now have original miniature travellers with garments, staffs and packs, a shaded cloth standard and grounded shadows. The active band retains a gold ring when a different hex is inspected; its name and remaining-action diamonds appear in the existing dock. Right-click movement and a local destination preview make the control relationship explicit.

Forests use fewer, larger trees with trunks, asymmetric lit crowns and open glades. Mountains have broken ridges, rock faces and scree; hills use their own rounded silhouettes. Ground textures vary by biome, wetlands include pools and reeds, and beaches sit within several shallow-water washes. A modest slope-lighting wash reads only known neighboring elevations, preserving fog privacy. Place-name labels are optional and initially shown through hover, leaving the landscape clear.

These are original procedural illustrations and miniature figures within the native renderer. They do not use Civilization artwork. This pass improves composition and depth cues; animated 3D figures, elevation meshes, a freely tilted camera, water shaders and dynamic lighting require additional renderer work. The simulation and save format are unchanged.

## Landscape

The physical world still uses the same spherical cell graph. The presentation softens the board-like appearance through shared-corner color blending, subtle mottled ground textures, irregular tree clusters, illustrated mountain faces, shallow-water washes, and shoreline highlights. Desert dunes, wetland pools, grasses, and scattered groves give terrain types their own surface character. Forests include conifers and broadleaf trees; autumn changes the warmer woodland palette and cold winter terrain receives a pale wash.

The trees, mountains, textures, mist, and cartographic symbols are original procedural drawings in the desktop source. They require no downloaded art or external image service. Stable presentation seeds place details consistently as the camera moves, independently of simulation randomness. Terrain is cached between relevant state or camera changes. During dragging or rapid zooming, a lighter drawing pass keeps the camera responsive; the complete illustration returns when the gesture settles.

Terrain decoration does not change elevation, movement, resources, climate, or world generation. Coastline strokes describe existing land-water boundaries. Wetland water marks are illustrative pools, not a new river or drainage simulation. Relief drawings are screen-space illustrations over the globe rather than an elevation-displaced 3D mesh.

## Discovery

**Known land is the default** when opening the game, creating a story, or loading a story. The camera starts close enough to read the first familiar places. Unexplored space uses a misted chart background. A blurred discovery mask combines explored cells and softens the remembered world's edge, avoiding a separate haze outline around each hex.

The **Known land / Atlas view** button remains available for development and inspection. Switching it does not discover cells or modify the story. Atlas view reveals the existing world for examination; returning to Known land reapplies the exploration mask.

Known-land rendering filters terrain, shoreline neighbors, region labels, wildlife, and bands. Speech and polity overlays use bands in explored cells as their sources. Inspecting an unexplored cell shows the unknown-place panel rather than its resources or inhabitants.

The current simulation records places that have been explored, not a separate live-visibility or intelligence history. Entities in remembered cells therefore remain visible; this pass does not add stale sightings or remembered enemy positions.

## Signs of belonging

Every living band has a color and emblem determined by its persistent band ID. The vocabulary uses sun, crescent, stag, twin peaks, leaf, three rivers, star, and hearth marks, with circular seals and shield frames. The first sixteen IDs have distinct color/emblem/frame combinations; the visual vocabulary repeats after that. Names remain the identifying labels.

The same identity appears on map banners, the player panel, selected-place details, and the Polities key. Camp and wandering-band drawings distinguish ways of living. When multiple bands share a place, their banners spread apart and their names stack beneath them. Because identity is independent of language, a daughter band is distinguishable immediately while it still speaks the parent tongue; language divergence does not recolor its banner.

The **Polities** overlay shows local presence around bands, with a legend for the peoples visible under the chosen discovery mode. It does not assign ownership to natural regions or add political borders to the simulation. The **Regions** and **Speech** layers remain separate readings of the same landscape.

Animal silhouettes replace the letter badges. The selected-place panel supplies species names, numbers, and contact information. Wildlife marks recede at globe scale so they do not cover the geography.

## Interface

The shell uses a restrained palette, finer borders, a laurel wordmark, a season medallion, illustrated action icons, and consistent band emblems. The selected navigation tab has a gold underline; controls brighten on hover. Two small chapter marks indicate available actions. Population and food retain their prominent position, and the chronicle remains visible beneath the active page.

Grid lines are optional. Movement possibilities and the selected cell receive local outlines so action boundaries remain available without drawing a heavy seam around every place. Place names are spaced around band labels and use scale-sensitive typography.

## The library type palette

The text interface draws on book title pages, running heads, and handwritten marginal notes. EB Garamond supplies the display lettering, numerals, and italic annotations; its semibold face gives headings and spaced capitals enough weight against the dark panels. Constantia handles explanatory text and statistics, while Libre Baskerville gives buttons a clear, substantial reading face. Small keyboard hints retain Segoe UI. The hierarchy carries the historical character without requiring players to decipher script during play.

Labels, controls, and reading text have separate type roles and larger sizes than the original interface. Measured fitting uses the same face as the rendered text. Long names can wrap or shorten within their panels, and counters scale within their allotted width. The chronicle and secondary pages use italic notes and restrained folio details to continue the book aesthetic around the map.

`Typography.cs` resolves installed font families once and caches the fonts used by the renderer. This machine has EB Garamond and Libre Baskerville installed; they are not bundled or downloaded by the game. Other Windows machines fall back to Palatino Linotype/Georgia for display and Constantia/Georgia for controls, with Cambria/Georgia reading fallbacks. The presentation requires no font installation to run.

## Review artifacts and implementation

The desktop smoke command renders the actual drawing code into the `artifacts` directory, including the known-land start, an atlas overview, closer terrain, the other interface pages, and a polity presentation fixture. `clio-four-polity-fixture.png` explicitly labels its four bands' illustrative positions. That image uses a separate temporary game; these positions never enter a player's command history or a saved story.

Focused visual checks compare full rendered pixel hashes before and after changing undiscovered geography and inhabitants. They exercise terrain, speech, and polity views with a hidden band immediately outside discovery, verify that Atlas exposes those changes, and check that toggling views leaves simulation state and random streams untouched. A real fission command supplies the daughter-band identity fixture. Draw timings are reported as local observations rather than a production performance guarantee.

The presentation lives in:

- [`MapRenderer.cs`](../src/Clio.Desktop/MapRenderer.cs): projection, discovery, terrain blending, overlays, labels, and map composition.
- [`TerrainArt.cs`](../src/Clio.Desktop/TerrainArt.cs): ground textures, trees, relief, and mist.
- [`IdentityArt.cs`](../src/Clio.Desktop/IdentityArt.cs): band emblems, banners, and animal silhouettes.
- [`GameForm.cs`](../src/Clio.Desktop/GameForm.cs): interface layout, input, inspection, and render fixtures.
- [`Typography.cs`](../src/Clio.Desktop/Typography.cs): semantic font roles, fallback selection, measured lines, and spaced capitals.

Simulation and world-generation behavior are unchanged. Existing `CLIO-STORY-1` saves continue to reconstruct their stories from the original settings and command sequence. This is a native Windows presentation pass; Unity/HDRP integration remains separate. Current check results and broader limitations belong in [`VALIDATION.md`](VALIDATION.md).
