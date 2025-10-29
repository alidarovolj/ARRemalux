using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using RemaluxAR.Drawing;
using RemaluxAR.UI;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Автоматически настраивает AR сцену при запуске
    /// Создаёт все необходимые компоненты если их нет
    /// </summary>
    public class ARSceneSetup : MonoBehaviour
    {
        [Header("Prefab References")]
        [SerializeField] private GameObject paintDotPrefab;

        [Header("Auto Setup")]
        [SerializeField] private bool autoSetupOnStart = false; // Отключено - сцена уже настроена

        private ARSession arSession;
        private XROrigin arSessionOrigin;
        private ARManager arManager;
        private DrawingManager drawingManager;
        private MeshManager meshManager;
        private UIController uiController;

        private void Start()
        {
            if (autoSetupOnStart)
            {
                SetupARScene();
            }
        }

        /// <summary>
        /// Настраивает всю AR сцену
        /// </summary>
        [ContextMenu("Setup AR Scene")]
        public void SetupARScene()
        {
            Debug.Log("[ARSceneSetup] Setting up AR Scene...");

            SetupARSession();
            SetupARSessionOrigin();
            SetupARManager();
            SetupMeshManager();
            SetupDrawingManager();
            SetupUIController();

            Debug.Log("[ARSceneSetup] AR Scene setup complete!");
        }

        /// <summary>
        /// Создаёт или находит ARSession
        /// </summary>
        private void SetupARSession()
        {
            arSession = FindObjectOfType<ARSession>();
            if (arSession == null)
            {
                GameObject sessionObj = new GameObject("AR Session");
                arSession = sessionObj.AddComponent<ARSession>();
                Debug.Log("[ARSceneSetup] Created ARSession");
            }
            else
            {
                Debug.Log("[ARSceneSetup] Using existing ARSession");
            }
        }

        /// <summary>
        /// Создаёт или находит XROrigin
        /// </summary>
        private void SetupARSessionOrigin()
        {
            arSessionOrigin = FindObjectOfType<XROrigin>();
            if (arSessionOrigin == null)
            {
                GameObject originObj = new GameObject("XR Origin");
                arSessionOrigin = originObj.AddComponent<XROrigin>();

                // Создаём AR Camera
                GameObject arCameraObj = new GameObject("AR Camera");
                arCameraObj.transform.SetParent(originObj.transform);
                Camera arCamera = arCameraObj.AddComponent<Camera>();
                arCamera.clearFlags = CameraClearFlags.SolidColor;
                arCamera.backgroundColor = Color.black;
                arCamera.nearClipPlane = 0.1f;
                arCamera.farClipPlane = 20f;

                // Добавляем AR Camera компоненты
                arCameraObj.AddComponent<ARCameraManager>();
                arCameraObj.AddComponent<ARCameraBackground>();

                // Назначаем камеру в XROrigin
                arSessionOrigin.Camera = arCamera;

                // Добавляем необходимые менеджеры
                originObj.AddComponent<ARRaycastManager>();
                originObj.AddComponent<ARPlaneManager>();
                originObj.AddComponent<AROcclusionManager>();
                originObj.AddComponent<ARMeshManager>();

                Debug.Log("[ARSceneSetup] Created XROrigin with camera and managers");
            }
            else
            {
                Debug.Log("[ARSceneSetup] Using existing XROrigin");
                
                // НЕ добавляем менеджеры автоматически - они должны быть настроены вручную
                // Это избегает конфликтов с существующей структурой сцены
            }

            // Отключаем старую MainCamera если есть
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name == "Main Camera" && !cam.GetComponent<ARCameraManager>())
                {
                    cam.gameObject.SetActive(false);
                    Debug.Log("[ARSceneSetup] Disabled old Main Camera");
                }
            }
        }

        /// <summary>
        /// Создаёт или находит ARManager
        /// </summary>
        private void SetupARManager()
        {
            arManager = FindObjectOfType<ARManager>();
            if (arManager == null)
            {
                // Добавляем ARManager к существующему AR Session объекту
                if (arSession != null)
                {
                    arManager = arSession.gameObject.AddComponent<ARManager>();
                    Debug.Log("[ARSceneSetup] Created ARManager on AR Session");
                }
                else
                {
                    GameObject managerObj = new GameObject("AR Manager");
                    arManager = managerObj.AddComponent<ARManager>();
                    Debug.Log("[ARSceneSetup] Created ARManager");
                }
            }
            else
            {
                Debug.Log("[ARSceneSetup] Using existing ARManager");
            }
        }

        /// <summary>
        /// Создаёт или находит MeshManager
        /// </summary>
        private void SetupMeshManager()
        {
            meshManager = FindObjectOfType<MeshManager>();
            if (meshManager == null)
            {
                // Добавляем MeshManager к EnvironmentMeshManager если он есть
                GameObject envMeshObj = GameObject.Find("EnvironmentMeshManager");
                if (envMeshObj != null && envMeshObj.GetComponent<ARMeshManager>() != null)
                {
                    meshManager = envMeshObj.AddComponent<MeshManager>();
                    Debug.Log("[ARSceneSetup] Created MeshManager on EnvironmentMeshManager");
                }
                else
                {
                    GameObject managerObj = arManager != null ? arManager.gameObject : new GameObject("Mesh Manager");
                    meshManager = managerObj.AddComponent<MeshManager>();
                    Debug.Log("[ARSceneSetup] Created MeshManager");
                }
            }
            else
            {
                Debug.Log("[ARSceneSetup] Using existing MeshManager");
            }
        }

        /// <summary>
        /// Создаёт или находит DrawingManager
        /// </summary>
        private void SetupDrawingManager()
        {
            drawingManager = FindObjectOfType<DrawingManager>();
            if (drawingManager == null)
            {
                GameObject managerObj = new GameObject("Drawing Manager");
                drawingManager = managerObj.AddComponent<DrawingManager>();

                // Присваиваем prefab если есть
                if (paintDotPrefab != null)
                {
                    var field = typeof(DrawingManager).GetField("paintDotPrefab", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(drawingManager, paintDotPrefab);
                    }
                }

                Debug.Log("[ARSceneSetup] Created DrawingManager");
            }
            else
            {
                Debug.Log("[ARSceneSetup] Using existing DrawingManager");
            }
        }

        /// <summary>
        /// Создаёт или находит UIController
        /// </summary>
        private void SetupUIController()
        {
            uiController = FindObjectOfType<UIController>();
            if (uiController == null)
            {
                Debug.Log("[ARSceneSetup] UIController не найден, создается новый");
                // UI требует Canvas, создадим его если нет
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasObj = new GameObject("Canvas");
                    canvas = canvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                    canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }

                GameObject uiObj = new GameObject("UI Controller");
                uiObj.transform.SetParent(canvas.transform);
                uiController = uiObj.AddComponent<UIController>();

                Debug.Log("[ARSceneSetup] Created UIController");
            }
        }

        /// <summary>
        /// Сбрасывает всю настройку (для отладки)
        /// </summary>
        [ContextMenu("Reset AR Scene")]
        public void ResetARScene()
        {
            Debug.LogWarning("[ARSceneSetup] Resetting AR Scene - this will destroy AR components!");

            if (arSession != null) DestroyImmediate(arSession.gameObject);
            if (arSessionOrigin != null) DestroyImmediate(arSessionOrigin.gameObject);
            if (arManager != null) DestroyImmediate(arManager.gameObject);
            if (drawingManager != null) DestroyImmediate(drawingManager.gameObject);
            if (meshManager != null && meshManager.gameObject != arManager?.gameObject) 
                DestroyImmediate(meshManager.gameObject);

            Debug.Log("[ARSceneSetup] AR Scene reset complete. Call SetupARScene() to recreate.");
        }
    }
}

