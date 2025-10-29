using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace RemaluxAR.Editor
{
    /// <summary>
    /// Помощник для быстрой настройки AR сцены
    /// </summary>
    public class ARSetupHelper : EditorWindow
    {
        [MenuItem("ARRemalux/Setup AR Scene for Testing")]
        public static void SetupARScene()
        {
            Debug.Log("[ARSetupHelper] Настройка AR сцены для тестирования...");

            // 1. Находим или создаём AR Session
            ARSession arSession = FindObjectOfType<ARSession>();
            if (arSession == null)
            {
                GameObject sessionObj = new GameObject("AR Session");
                arSession = sessionObj.AddComponent<ARSession>();
                Debug.Log("[ARSetupHelper] ✓ AR Session создан");
            }

            // 2. Находим или создаём XR Origin
            XROrigin xrOrigin = FindObjectOfType<XROrigin>();
            if (xrOrigin == null)
            {
                GameObject originObj = new GameObject("XR Origin");
                xrOrigin = originObj.AddComponent<XROrigin>();

                // Создаём камеру
                GameObject cameraObj = new GameObject("AR Camera");
                cameraObj.transform.SetParent(originObj.transform);
                Camera cam = cameraObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.tag = "MainCamera";

                // AR компоненты камеры
                cameraObj.AddComponent<ARCameraManager>();
                cameraObj.AddComponent<ARCameraBackground>();

                xrOrigin.Camera = cam;

                Debug.Log("[ARSetupHelper] ✓ XR Origin создан с камерой");
            }

            // 3. Добавляем менеджеры к XR Origin
            GameObject xrOriginGO = xrOrigin.gameObject;
            
            if (xrOriginGO.GetComponent<ARPlaneManager>() == null)
            {
                var planeManager = xrOriginGO.AddComponent<ARPlaneManager>();
                
                // Создаём простой префаб для плоскостей
                GameObject planePrefab = CreateSimplePlanePrefab();
                planeManager.planePrefab = planePrefab;
                
                Debug.Log("[ARSetupHelper] ✓ ARPlaneManager добавлен");
            }

            if (xrOriginGO.GetComponent<ARRaycastManager>() == null)
            {
                xrOriginGO.AddComponent<ARRaycastManager>();
                Debug.Log("[ARSetupHelper] ✓ ARRaycastManager добавлен");
            }

            // 4. Добавляем наш упрощённый компонент
            if (xrOriginGO.GetComponent<RemaluxAR.AR.WallDetectionAndPainting>() == null)
            {
                xrOriginGO.AddComponent<RemaluxAR.AR.WallDetectionAndPainting>();
                Debug.Log("[ARSetupHelper] ✓ WallDetectionAndPainting добавлен");
            }

            // 5. Отключаем старую Main Camera если есть
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (var camera in cameras)
            {
                if (camera.gameObject.name == "Main Camera" && camera.GetComponent<ARCameraManager>() == null)
                {
                    camera.gameObject.SetActive(false);
                    Debug.Log("[ARSetupHelper] ✓ Старая Main Camera отключена");
                }
            }

            Debug.Log("[ARSetupHelper] ========================================");
            Debug.Log("[ARSetupHelper] ✓✓✓ Настройка завершена! ✓✓✓");
            Debug.Log("[ARSetupHelper] ========================================");
            Debug.Log("[ARSetupHelper]");
            Debug.Log("[ARSetupHelper] СЛЕДУЮЩИЕ ШАГИ:");
            Debug.Log("[ARSetupHelper] 1. Перейдите в Edit > Project Settings > XR Plug-in Management");
            Debug.Log("[ARSetupHelper] 2. Включите 'Apple ARKit XR Plugin' для iOS");
            Debug.Log("[ARSetupHelper] 3. Включите 'Google ARCore XR Plugin' для Android");
            Debug.Log("[ARSetupHelper] 4. Для тестирования в редакторе:");
            Debug.Log("[ARSetupHelper]    - Window > XR > AR Foundation > XR Environment (если доступно)");
            Debug.Log("[ARSetupHelper]    - ИЛИ соберите проект на iOS/Android устройство");
            Debug.Log("[ARSetupHelper]");
            Debug.Log("[ARSetupHelper] КАК ИСПОЛЬЗОВАТЬ:");
            Debug.Log("[ARSetupHelper] - Красные плоскости = стены");
            Debug.Log("[ARSetupHelper] - Зелёные плоскости = пол");
            Debug.Log("[ARSetupHelper] - Кликайте на стены для окраски!");
            Debug.Log("[ARSetupHelper] ========================================");

            EditorUtility.SetDirty(xrOrigin.gameObject);
            EditorUtility.SetDirty(arSession.gameObject);
        }

        [MenuItem("ARRemalux/Create Plane Prefab")]
        public static GameObject CreateSimplePlanePrefab()
        {
            // Создаём папку Prefabs если её нет
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            // Проверяем, существует ли уже префаб
            string prefabPath = "Assets/Prefabs/ARPlane.prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                Debug.Log("[ARSetupHelper] Используем существующий ARPlane prefab");
                return existingPrefab;
            }

            // Создаём новый GameObject
            GameObject planePrefab = new GameObject("ARPlane");
            
            // Добавляем ARPlane компонент
            planePrefab.AddComponent<ARPlane>();
            
            // Добавляем MeshFilter и MeshRenderer
            var meshFilter = planePrefab.AddComponent<MeshFilter>();
            var meshRenderer = planePrefab.AddComponent<MeshRenderer>();
            
            // Создаём простой материал
            Material planeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            planeMaterial.color = new Color(1f, 0f, 0f, 0.3f); // Красный полупрозрачный
            planeMaterial.SetFloat("_Surface", 1); // Transparent
            planeMaterial.SetFloat("_Blend", 0); // Alpha
            planeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            planeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            planeMaterial.SetInt("_ZWrite", 0);
            planeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            planeMaterial.renderQueue = 3000;
            
            meshRenderer.material = planeMaterial;
            
            // Добавляем наш скрипт визуализации
            planePrefab.AddComponent<RemaluxAR.AR.ARPlanePrefabSetup>();
            
            // Добавляем LineRenderer для границ
            var lineRenderer = planePrefab.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.color = Color.yellow;
            lineRenderer.material = lineMaterial;
            lineRenderer.useWorldSpace = false;
            
            // Сохраняем как префаб
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(planePrefab, prefabPath);
            
            // Удаляем временный объект
            DestroyImmediate(planePrefab);
            
            Debug.Log($"[ARSetupHelper] ✓ AR Plane prefab создан: {prefabPath}");
            
            return savedPrefab;
        }

        [MenuItem("ARRemalux/Open XR Plugin Management Settings")]
        public static void OpenXRSettings()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        [MenuItem("ARRemalux/Help - Testing in Editor")]
        public static void ShowEditorTestingHelp()
        {
            EditorUtility.DisplayDialog(
                "Тестирование AR в редакторе",
                "ОПЦИЯ 1: XR Environment Simulation\n" +
                "- Window > XR > AR Foundation > XR Environment\n" +
                "- Позволяет симулировать AR в редакторе\n" +
                "- Требует пакет XR Environment или Device Simulator\n\n" +
                "ОПЦИЯ 2: Тестирование на устройстве (рекомендуется)\n" +
                "- File > Build Settings\n" +
                "- Выберите iOS или Android\n" +
                "- Соберите и запустите на реальном устройстве\n\n" +
                "ОПЦИЯ 3: Unity Remote (ограниченно)\n" +
                "- Подключите устройство через Unity Remote\n" +
                "- Ограниченный функционал\n\n" +
                "В редакторе БЕЗ симуляции AR работать не будет!",
                "Понятно"
            );
        }
    }
}

