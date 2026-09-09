using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Clio.Presentation.Editor
{
    public static class ClioSceneMenu
    {
        [MenuItem("Clio/Create globe scene")]
        public static void CreateGlobeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
            {
                Debug.LogError("Clio requires a Unity 6 HDRP project with an active HDRP asset. Complete HDRP setup before creating the globe.");
                return;
            }
            if (Object.FindFirstObjectByType<ClioGlobeView>() != null)
            {
                Selection.activeGameObject = Object.FindFirstObjectByType<ClioGlobeView>().gameObject;
                Debug.Log("An existing Clio globe was selected. The scene was left unchanged."); return;
            }
            Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Create Clio globe starter");
            GameObject root = new GameObject("Clio globe starter"); Undo.RegisterCreatedObjectUndo(root, "Create Clio globe starter");
            ClioGlobeView view = root.AddComponent<ClioGlobeView>();
            GameObject cameraObject = new GameObject("Clio camera"); cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            HDAdditionalCameraData cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            camera.fieldOfView = 42; camera.nearClipPlane = 0.01f; camera.farClipPlane = 1000;
            camera.depth = 20; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.016f, 0.025f, 0.037f);
            cameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            cameraData.backgroundColorHDR = camera.backgroundColor;
            camera.transform.position = new Vector3(0, 0, -13.5f); camera.transform.LookAt(root.transform);
            view.ViewCamera = camera;
            // Preserve existing cameras, lights, pipeline assets, volumes and unsaved scene objects.
            // New lights are camera-relative so the playable hemisphere remains legible through orbit.
            CreateLight(cameraObject.transform, "Clio warm key", new Vector3(35, -30, 0), new Color(1, 0.88f, 0.70f), 12000);
            CreateLight(cameraObject.transform, "Clio cool fill", new Vector3(-20, 55, 0), new Color(0.63f, 0.78f, 1), 4500);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene); Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Clio globe starter added. Save the scene, set Active Input Handling to Both, then enter Play mode. Existing scene content was preserved.");
        }

        private static void CreateLight(Transform parent, string title, Vector3 rotation, Color color, float lux)
        {
            GameObject root = new GameObject(title); root.transform.SetParent(parent, false);
            root.transform.localRotation = Quaternion.Euler(rotation);
            Light light = root.AddComponent<Light>(); light.type = LightType.Directional;
            root.AddComponent<HDAdditionalLightData>();
            light.lightUnit = LightUnit.Lux; light.intensity = lux; light.color = color;
            light.shadows = LightShadows.Soft;
        }
    }
}
