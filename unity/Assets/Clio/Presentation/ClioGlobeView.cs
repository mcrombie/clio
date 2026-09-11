using System;
using System.Collections.Generic;
using Clio.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using SimTerrain = Clio.Simulation.Terrain;

namespace Clio.Presentation
{
    /// <summary>
    /// Unity 6 / HDRP integration starter. All game rules live in Clio.Simulation.
    /// This component has not yet been compiled or visually validated in Unity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClioGlobeView : MonoBehaviour
    {
        public int Seed = 2026;
        public string BandName = "First Hearth";
        public Ancestry Ancestry = Ancestry.Human;
        public LanguageStyle LanguageStyle = LanguageStyle.Flowing;
        public bool FourStartingBands;
        public Camera ViewCamera;
        [Min(0.1f)] public float Radius = 5;
        [Range(0.97f, 1f)] public float PolarScale = 0.996647f;
        [Range(0, 0.1f)] public float Relief = 0.045f;
        public bool EnableLegacyInput = true;
        public bool ShowControls = true;
        [Tooltip("Assign a retained HDRP/Lit material to prevent shader stripping in a standalone build.")]
        public Material TerrainMaterialTemplate;

        public Game State { get; private set; }
        public int SelectedCellId { get; private set; }
        private Mesh globeMesh;
        private GameObject runtimeRoot;
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly Dictionary<Vec3, float> cornerRelief = new Dictionary<Vec3, float>();
        private LineRenderer selectedRing, bandRing;
        private Transform bandMarker;
        private Quaternion orbit;
        private float cameraDistance;
        private Vector3 lastPointer;
        private bool orbitDragging;
        private string status = "Select adjacent land to travel. Gather before winter.";
        private Vector2 panelScroll;
        private Rect panel = new Rect(16, 16, 310, 660);

        private static readonly Color[] TerrainColors = {
            new Color(0.025f,0.105f,0.18f), new Color(0.07f,0.32f,0.38f),
            new Color(0.53f,0.58f,0.29f), new Color(0.16f,0.36f,0.24f),
            new Color(0.46f,0.44f,0.30f), new Color(0.48f,0.47f,0.45f),
            new Color(0.76f,0.61f,0.35f), new Color(0.55f,0.59f,0.53f),
            new Color(0.85f,0.92f,0.91f), new Color(0.28f,0.45f,0.37f)
        };

        private void Start()
        {
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
            {
                status = "Clio requires an active HDRP pipeline asset. Open a Unity 6 HDRP project and complete its setup.";
                Debug.LogError(status, this); enabled = false; return;
            }
            Shader shader = TerrainMaterialTemplate != null ? TerrainMaterialTemplate.shader : Shader.Find("HDRP/Lit");
            if (shader == null || shader.name != "HDRP/Lit")
            {
                status = "HDRP/Lit is unavailable. Assign an HDRP/Lit material to Terrain Material Template and include it in the build.";
                Debug.LogError(status, this); enabled = false; return;
            }
            if (ViewCamera == null) ViewCamera = Camera.main;
            if (ViewCamera == null)
            {
                status = "Assign a camera or use Clio > Create globe scene.";
                Debug.LogError(status, this); enabled = false; return;
            }
            State = new Game(new GameSettings(Seed, LanguageStyle, Ancestry, FourStartingBands, BandName));
            SelectedCellId = State.Player.CellId;
            runtimeRoot = new GameObject("Generated globe");
            runtimeRoot.transform.SetParent(transform, false);
            BuildMesh(shader);
            Material gold = MakeMaterial(shader, new Color(0.95f, 0.72f, 0.31f), "Clio selection");
            gold.SetColor("_EmissiveColor", new Color(1.8f, 1.0f, 0.25f));
            Material ivory = MakeMaterial(shader, new Color(0.94f, 0.96f, 0.83f), "Clio band");
            ivory.SetColor("_EmissiveColor", new Color(1.2f, 1.2f, 0.8f));
            selectedRing = MakeRing("Selected place", gold, Radius * 0.0014f);
            bandRing = MakeRing("Band location", ivory, Radius * 0.0022f);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Player band"; marker.transform.SetParent(runtimeRoot.transform, false);
            marker.transform.localScale = Vector3.one * Radius * 0.014f;
            marker.GetComponent<Renderer>().sharedMaterial = ivory;
            Destroy(marker.GetComponent<Collider>()); bandMarker = marker.transform;
            cameraDistance = Radius * 2.7f;
            FocusBand(); RefreshMarkers();
        }

        private void BuildMesh(Shader shader)
        {
            Dictionary<Vec3, Vector2> totals = new Dictionary<Vec3, Vector2>();
            foreach (Cell cell in State.World.Cells)
                foreach (Vec3 corner in cell.Corners)
                {
                    Vector2 value; totals.TryGetValue(corner, out value);
                    totals[corner] = value + new Vector2(Mathf.Max(0, (float)cell.Elevation), 1);
                }
            foreach (KeyValuePair<Vec3, Vector2> pair in totals) cornerRelief[pair.Key] = pair.Value.x / pair.Value.y;

            List<Vector3> vertices = new List<Vector3>();
            List<int>[] triangles = new List<int>[TerrainColors.Length];
            Material[] materials = new Material[TerrainColors.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new List<int>();
                materials[i] = MakeMaterial(shader, TerrainColors[i], "Clio " + ((SimTerrain)i));
                materials[i].SetFloat("_Smoothness", i <= (int)SimTerrain.Coast ? 0.65f : 0.22f);
            }
            foreach (Cell cell in State.World.Cells)
            {
                int first = vertices.Count;
                vertices.Add(Project(cell.Center, Mathf.Max(0, (float)cell.Elevation)));
                foreach (Vec3 corner in cell.Corners) vertices.Add(Project(corner, cornerRelief[corner]));
                List<int> indices = triangles[(int)cell.Terrain];
                for (int i = 0; i < cell.Corners.Length; i++)
                {
                    indices.Add(first); indices.Add(first + 1 + i); indices.Add(first + 1 + (i + 1) % cell.Corners.Length);
                }
            }
            globeMesh = new Mesh { name = "Clio dual icosphere", indexFormat = IndexFormat.UInt32 };
            globeMesh.SetVertices(vertices); globeMesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) globeMesh.SetTriangles(triangles[i], i, false);
            globeMesh.RecalculateNormals(); globeMesh.RecalculateBounds();
            runtimeRoot.AddComponent<MeshFilter>().sharedMesh = globeMesh;
            MeshRenderer renderer = runtimeRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials; renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private Material MakeMaterial(Shader shader, Color color, string title)
        {
            Material material = TerrainMaterialTemplate != null ? new Material(TerrainMaterialTemplate) : new Material(shader);
            material.name = title; material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 0.25f);
            ownedMaterials.Add(material); return material;
        }

        private Vector3 Project(Vec3 p, float elevation)
        {
            float radius = Radius * (1 + Relief * elevation);
            return new Vector3((float)p.X * radius, (float)p.Y * radius * PolarScale, (float)p.Z * radius);
        }

        private LineRenderer MakeRing(string title, Material material, float width)
        {
            GameObject root = new GameObject(title); root.transform.SetParent(runtimeRoot.transform, false);
            LineRenderer line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.loop = true; line.useWorldSpace = false; line.widthMultiplier = width;
            line.generateLightingData = true; line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }

        private void SetRing(LineRenderer ring, int cellId)
        {
            Cell cell = State.World.Cells[cellId]; ring.positionCount = cell.Corners.Length;
            for (int i = 0; i < cell.Corners.Length; i++)
                ring.SetPosition(i, Project(cell.Corners[i], cornerRelief[cell.Corners[i]]) * 1.0025f);
        }

        private void RefreshMarkers()
        {
            if (State == null) return;
            SetRing(selectedRing, SelectedCellId); SetRing(bandRing, State.Player.CellId);
            Cell location = State.World.Cells[State.Player.CellId];
            bandMarker.localPosition = Project(location.Center, Mathf.Max(0, (float)location.Elevation)) * 1.009f;
        }

        public void FocusBand()
        {
            if (State == null) return;
            Vector3 outward = transform.TransformDirection(Project(State.World.Cells[State.Player.CellId].Center, 0).normalized);
            Vector3 up = Mathf.Abs(Vector3.Dot(outward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            orbit = Quaternion.LookRotation(-outward, up);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            ViewCamera.transform.rotation = orbit;
            ViewCamera.transform.position = transform.position - orbit * Vector3.forward * cameraDistance;
        }

        private void Update()
        {
            if (State == null || !EnableLegacyInput) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            Vector3 pointer = Input.mousePosition;
            bool overPanel = ShowControls && panel.Contains(new Vector2(pointer.x, Screen.height - pointer.y));
            if (Input.GetMouseButtonDown(1) && !overPanel) { orbitDragging = true; lastPointer = pointer; }
            if (Input.GetMouseButtonUp(1)) orbitDragging = false;
            if (orbitDragging)
            {
                Vector3 delta = pointer - lastPointer; lastPointer = pointer;
                // Camera-local rotations permit continuous travel over either pole.
                orbit = Quaternion.AngleAxis(-delta.x * 0.22f, orbit * Vector3.up) * orbit;
                orbit = Quaternion.AngleAxis(delta.y * 0.22f, orbit * Vector3.right) * orbit;
                UpdateCamera();
            }
            if (!overPanel)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.001f)
                { cameraDistance = Mathf.Clamp(cameraDistance * Mathf.Exp(-scroll * 0.10f), Radius * 1.25f, Radius * 5); UpdateCamera(); }
                if (Input.GetMouseButtonDown(0)) PickCell(ViewCamera.ScreenPointToRay(pointer));
            }
            if (Input.GetKeyDown(KeyCode.F)) FocusBand();
#endif
        }

        public bool PickCell(Ray ray)
        {
            if (State == null) return false;
            Vector3 o = transform.InverseTransformPoint(ray.origin) / Radius;
            Vector3 d = transform.InverseTransformDirection(ray.direction);
            o.y /= PolarScale; d.y /= PolarScale; d.Normalize();
            float b = Vector3.Dot(o, d), c = Vector3.Dot(o, o) - 1;
            float discriminant = b * b - c;
            if (discriminant < 0) return false;
            float t = -b - Mathf.Sqrt(discriminant);
            if (t < 0) t = -b + Mathf.Sqrt(discriminant);
            if (t < 0) return false;
            Vector3 point = (o + d * t).normalized; double closest = -2; int selected = 0;
            foreach (Cell cell in State.World.Cells)
            {
                double dot = cell.Center.X * point.x + cell.Center.Y * point.y + cell.Center.Z * point.z;
                if (dot > closest) { closest = dot; selected = cell.Id; }
            }
            SelectedCellId = selected; RefreshMarkers(); return true;
        }

        private void Command(Func<string> command)
        {
            status = command(); RefreshMarkers();
        }

        private void OnGUI()
        {
            if (!ShowControls) return;
            panel.height = Mathf.Min(Screen.height - 32, 730);
            GUILayout.BeginArea(panel, GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("CLIO  /  UNITY GLOBE STARTER", GUI.skin.box);
            if (State == null) GUILayout.Label(status);
            else
            {
                GUILayout.Label(State.Player.Name + "  |  " + State.Player.Ancestry);
                GUILayout.Label("Turn " + State.Turn + " · " + State.Season + " · " + State.Actions + " actions");
                GUILayout.Label(State.Player.Population + " people   |   " + Mathf.FloorToInt((float)State.Player.Food) + " provisions");
                GUILayout.Label("Upkeep: " + State.Upkeep(State.Player) + " per turn");
                GUILayout.Space(10);
                Cell cell = State.World.Cells[SelectedCellId];
                GUILayout.Label(State.Place(cell.Id), GUI.skin.box);
                GUILayout.Label(cell.Terrain + " · region " + cell.RegionId + " · cell " + cell.Id);
                GUILayout.Label("Warmth " + cell.Temperature.ToString("P0") + " · moisture " + cell.Moisture.ToString("P0"));
                GUILayout.Label(State.Explored.Contains(cell.Id) ? "Known to your band" : "Unexplored (geography preview)");
                GUI.enabled = State.CanMove(cell.Id);
                if (GUILayout.Button("Move band to selected place")) Command(delegate { return State.Move(SelectedCellId); });
                GUI.enabled = !State.IsOver && State.Actions > 0;
                if (GUILayout.Button("Forage here  +" + State.ForageYield(State.Player.CellId, State.Player))) Command(State.Forage);
                GUILayout.Label("Foraging and encounters happen at the band's current location.");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Hunt")) Command(State.Hunt);
                if (GUILayout.Button("Befriend")) Command(State.Tame);
                GUILayout.EndHorizontal();
                if (GUILayout.Button(State.Player.Settled ? "Seasonal camp established" : "Raise seasonal camp")) Command(State.Camp);
                GUI.enabled = !State.IsOver;
                if (GUILayout.Button("End turn")) Command(State.EndTurn);
                GUI.enabled = true;
                if (GUILayout.Button("Focus band  [F]")) { SelectedCellId = State.Player.CellId; FocusBand(); RefreshMarkers(); }
                GUILayout.Space(8); GUILayout.Label(status, GUI.skin.box);
                GUILayout.Label("Right drag: orbit · wheel: zoom · click: select");
#if !ENABLE_LEGACY_INPUT_MANAGER
                GUILayout.Label("Mouse controls need Active Input Handling = Both. Restart the Editor after changing it.");
#endif
                GUILayout.Space(8); GUILayout.Label("Recent chronicle");
                int first = Mathf.Max(0, State.Chronicle.Count - 4);
                for (int i = first; i < State.Chronicle.Count; i++)
                    GUILayout.Label(State.Chronicle[i].Turn + " · " + State.Chronicle[i].Text);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.enabled = true;
        }

        private void OnDestroy()
        {
            if (globeMesh != null) Destroy(globeMesh);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
            if (runtimeRoot != null) Destroy(runtimeRoot);
        }
    }
}
