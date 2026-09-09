# Clio: a world that can accumulate history

## Map decision

Use a mostly hexagonal globe for movement and ecology, with persistent natural regions above it and overlapping human claims above those. A cell is a landscape catchment; a region is a larger named geographic area. Neither is automatically a sovereign province. This makes migration, shared hunting grounds, competing grazing rights, settlement growth and shifting frontiers possible without requiring one owner per territory.

The implemented foundation is the dual of a subdivided icosahedron. It has exactly twelve pentagons; every other cell is a hexagon. These twelve exceptions are mathematically necessary for this construction and should look like ordinary terrain. All cells are connected, including both poles. There is no longitude seam, invisible map edge or disconnected polar cap.

| Layout | Useful qualities | Cost for Clio |
| --- | --- | --- |
| Square or latitude/longitude grid | Easy textures and coordinate lookup | Directional movement bias; polar distortion; awkward seams |
| Flat hex map with east/west wrap | Familiar tactics and readable movement | Implies a cylinder; poles need artificial rules |
| Territory-only map | Large political changes read clearly | A fifty-person band loses meaningful local movement and animal pursuit |
| Dual icosphere plus geographic regions | Nearly uniform movement, globe continuity, local and regional histories | Twelve pentagons; more demanding picking and camera work |

The final option best fits a game that starts with one wandering band and eventually contains many interacting polities. Unity should display the same graph the simulation uses, rather than maintain a second hidden movement grid.

## What exists in the prototype

`src/Clio.Simulation/World.cs` generates seeded geography independently of Unity, using only the C# base class library. The default subdivision level is four: 2,562 cells, 7,680 shared edges, 5,120 shared corners. Vertex and edge identities are stable for a given resolution; changing the seed changes the world, not the topology.

The terrain model provides ocean, shallow coastal water, grassland, forest, hills, mountains, desert, tundra, ice and wetland. Coast is water and `Cell.IsLand` returns false there. Current temperature, moisture and productivity values are normalized ecological indices, not degrees, millimeters or calorie counts. Elevation is normalized and signed around sea level. The simulation should apply seasonal snow, depletion and species preferences separately from these baseline values.

Continents use coherent three-dimensional noise with domain warping and a broad lowland continental basin. The noise is evaluated on the sphere, so geography is continuous through longitude zero and across the poles. Sea level is chosen from a height percentile, giving approximately 42% land across seeds. A second field introduces narrow mountain ridges independently of continental shape. Latitude, altitude and regional moisture set the biomes. The initial basin favors a substantial productive mainland, while starting-position selection should still score forage, winter exposure and connected land area for the chosen species.

Natural regions grow from dispersed seeds over connected land or water. Growth costs rise across elevation and biome changes. Each region is contiguous and respects the shoreline. The current target is about 48 cells per region, with smaller regions for islands. These are deterministic geographic groupings, not a river drainage simulation: explicit rivers, watersheds, currents and rain shadows remain later work.

`Cell.Center` and `Cell.Corners` are unit vectors with north along +Y. Corners wind counterclockwise when viewed from outside. Shared edges use exactly the same two corner vectors. `Cell.Neighbors` also follows angular order. `World.SphericalArea(id)` returns each polygon's area in steradians; multiply by a chosen planet radius squared to obtain physical area.

## Scale without losing the small band

The prototype globe is a strategic abstraction. A fifty-person band does not occupy every hectare of a tile, and collecting a tile's forage does not imply clearing an entire province. Local camp position, access to water, seasonal routes and encounter locations should live within the cell as simulation attributes and selected scene details.

For an Earth-sized sphere of radius 6,371 km, the scale implications are substantial:

| Subdivisions | Cells | Mean cell area, km² | Equivalent circular diameter, km | Intended use |
| ---: | ---: | ---: | ---: | --- |
| 3 | 642 | 794,493 | 1,006 | Fast tests and coarse world previews |
| 4 | 2,562 | 199,089 | 503 | Current playable globe prototype |
| 5 | 10,242 | 49,801 | 252 | Broader strategic campaign experiment |
| 6 | 40,962 | 12,452 | 126 | Possible shipping resolution after profiling |
| 7 | 163,842 | 3,113 | 63 | High resolution experiment; costly region generation |

Diameters describe equal-area circles, not exact hex edge lengths. Actual tile areas vary modestly around the sphere. The twelve pentagons and projection differences make geometric uniformity approximate rather than perfect.

Even subdivision seven cannot literally resolve a small hunting party's daily movements. Increasing tile count indefinitely is the wrong solution to that gap. Maintain a stable strategic cell graph, model camps and encounters inside cells, and vary time represented by a turn. A later regional view can show local landmarks and routes without replacing the authoritative global cell identifiers. Do not make season duration or historical population growth depend on an assumed fixed number of years per turn.

Foraging capacity should use accessible productive area and regeneration, not merely tile count. Likewise, movement should eventually consume distance and terrain cost; pentagons must not become cheaper shortcuts because they have five edges. Transport networks, navigation and aircraft can then change effective distance without editing the graph.

## Nature, language and sovereignty are separate maps

Keep these layers distinct in the save model:

1. **Physical geography:** cell geometry, elevation, soils, water and long-lived ecological regions.
2. **Ecology:** seasonal biomass, herds, predators, disease, forest change and domestic populations.
3. **Habitual use:** camps, migrations, hunting grounds, pasture rights and paths used by multiple groups.
4. **Settlement and authority:** villages, towns, jurisdiction, tribute, contested border influence and military control.
5. **Culture and language:** weighted populations, practices, dialect families, contact zones and inherited place names.

Natural regions give exploration a readable vocabulary before states exist. A place may acquire a first proto-language name, later an inherited dialect form, and then an official imperial name. Preserve that history rather than renaming the physical region destructively. Language and culture borders should appear as blended distributions and contact zones; political ownership should use a clearer boundary. Allow a town's language, its ruler's culture and its pasture rights to differ.

An early band needs a range or set of familiar places, not a painted national border. The shift from remembered routes to exclusive farms and later administrative provinces can therefore emerge from play.

## Poles and oblate rendering

Retain unit-sphere coordinates for topology, pathfinding and generation. For presentation, transform a point `p` to `(R*p.X, R*(1-f)*p.Y, R*p.Z)`, where `f` is flattening. A small visual flattening around 0.025 can make the oblate shape readable at game scale; Earth-like flattening is approximately 1/298 and would be subtle. Treat exaggerated flattening as an art choice rather than a climate model.

For lighting, transform normals by the inverse transpose, or derive the ellipsoid normal from `(x/a², y/b², z/a²)`. Transforming a sphere normal as if it were a position produces incorrect polar shading. Picking should invert the visual transform before testing spherical cells.

Polar routes remain legal graph routes. Extreme cold, low forage, storms and exposure should make them nearly impossible for the starting band. Later cold-weather equipment, provisioning and advanced aircraft change those constraints. A pole should never be a hidden impassable tile simply to simplify the camera. The camera must orbit smoothly through polar views without longitude singularities; use vectors or quaternions, not latitude clamping.

The current generator represents ocean ice thermally; it does not yet simulate seasonal sea-ice connectivity. That needs an explicit traversal rule, because frozen water is not automatically permanent land.

## Unity 6 / HDRP visual specification

The production target is a readable, painterly natural-history globe with restrained metallic UI, parchment-toned typography panels and terrain that remains legible without permanently drawing every cell border. Let the landscapes carry the atmosphere and keep decorative UI from competing with small units.

**Camera and geometry.** Use an orbit camera with a stable horizon and a smooth zoom into a regional view. Keep the selected band on screen while panning. Render cell tops from the shared corner geometry, with a small inset only when emphasizing tiles. Use subtle bevels or land extrusion to catch light, but avoid deep gaps that turn continents into disconnected board pieces. Coastlines, silhouettes and hill ridges should remain readable from the initial camera distance. Decouple collision/picking meshes from decorative props.

**Land materials.** Start with biome palettes that share a coherent hue range: muted ochres for grasslands, deep desaturated jade for forests, warm gray stone, chalk-and-blue snow, pale sand and blue-green wetlands. Add elevation, slope, moisture and macro noise through Shader Graph. Reserve handwritten HLSL for measured needs such as spherical triplanar blending, instancing data or optimized shoreline sampling. Use world-space macro textures so repeated hex-shaped stamps do not reveal the grid.

**Water.** Distinguish deep ocean, shallow coast and shore foam. HDRP water or an appropriately simple custom material can provide slow broad highlights; small tactical pieces must not disappear in reflections. Climate-sensitive water tint and modest shoreline depth contribute more readability than constant high-frequency waves. Fog and atmosphere should separate the globe silhouette from the background without obscuring land in the center.

**Landmarks and vegetation.** Build original low-poly assets in Blender: families of conifers and broadleaf trees, clustered mountain ridges, rocky outcrops, reeds, snow caps and recognizable animal silhouettes. Randomize rotation, scale and density with stable per-cell seeds. Mountains should form visual ranges across neighboring cells; forest clusters should taper at ecological boundaries. Instanced meshes and level-of-detail transitions are essential at globe scale. A dragon must be readable as a rare moving creature, not just an oversized terrain prop.

**Lighting and post-processing.** Use one warm key light, cool ambient fill, contact shadows and restrained bloom. Keep north-facing slopes and the far side of the globe readable, even if the lighting is not physically literal. ACES-like tone mapping, subtle atmospheric rim light and limited depth of field can add scale. Avoid heavy film grain, chromatic aberration or strong vignette around map controls.

**Information overlays.** Offer ecology, elevation, winter risk, natural regions, culture, language and authority as deliberate views. Show neighbor movement options and route cost on selection. Default borders should emphasize the selected band and actionable tiles; provide a grid toggle for players who prefer it. Use icons plus labels or patterned accents so selection, danger and territorial disputes do not rely solely on red/green distinctions.

**Performance.** Share materials, instance vegetation and fauna, batch static terrain into chunks, and rebuild only changed meshes. Prefer a small mesh/material budget that can scale to 10,242 cells before committing to subdivision six. GPU picking or an inverse ellipsoid ray plus nearest graph search can avoid thousands of per-tile colliders. Do not allocate one Unity GameObject per ecological population or simulate every individual animal.

These are production art and renderer requirements. The simulation library supplies geometry and environmental data; it does not itself implement HDRP shaders, licensed art, a Unity camera or atmospheric scattering.

## Quantitative acceptance checks

The executable checks in `tests/WorldChecks.cs` exercise five resolutions and six climate seeds, including negative and maximum signed seeds. They verify:

- `10*4^n + 2` cells, exactly twelve pentagons and `30*4^n` edges.
- Reciprocal distinct adjacency, outward winding, normalized centers/corners, and exactly two shared corners for every adjacent pair.
- Euler characteristic two and total spherical area within `1e-9` steradians of `4π`.
- Whole-globe reachability from ordinary, northernmost and southernmost cells.
- Deterministic terrain, elevation, climate, productivity and region IDs for repeated seeds.
- Approximately 42% land, at least seven biomes, mountain terrain, valid shallow coasts and many productive temperate starting cells on substantial connected land.
- Contiguous geographic regions that do not mix water and land.
- Meaningful geographic variation between different seeds and rejection of invalid resolution inputs.

Before raising campaign resolution, record generation time, memory use, mesh upload cost, frame time with vegetation, picking latency and end-turn cost with a representative population of bands and animals. Set hardware targets after measuring the first Unity build. Do not infer production performance from topology tests alone.
