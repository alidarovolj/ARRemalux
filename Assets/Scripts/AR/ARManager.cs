using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using RemaluxAR.Utils;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Главный менеджер AR-сессии, координирует все AR-компоненты
    /// </summary>
    // [RequireComponent(typeof(ARSession))]
    public class ARManager : MonoBehaviour
    {
        [Header("AR Components")]
        [SerializeField] private ARSession arSession;
        [SerializeField] private XROrigin arSessionOrigin;
        [SerializeField] private ARCameraManager arCameraManager;
        [SerializeField] private ARRaycastManager arRaycastManager;
        [SerializeField] private ARPlaneManager arPlaneManager;
        [SerializeField] private AROcclusionManager arOcclusionManager;
        [SerializeField] private ARMeshManager arMeshManager;

        [Header("Settings")]
        [SerializeField] private bool enableMeshScanning = true;
        [SerializeField] private bool enableDepthOcclusion = true;
        [SerializeField] private bool enablePlaneDetection = true;

        // Events
        public event Action OnARSessionInitialized;
        public event Action OnARSessionFailed;

        // Properties
        public ARSession Session => arSession;
        public XROrigin SessionOrigin => arSessionOrigin;
        public ARCameraManager CameraManager => arCameraManager;
        public ARRaycastManager RaycastManager => arRaycastManager;
        public ARPlaneManager PlaneManager => arPlaneManager;
        public AROcclusionManager OcclusionManager => arOcclusionManager;
        public ARMeshManager MeshManager => arMeshManager;

        public bool IsSessionReady { get; private set; }
        public bool IsMeshScanningAvailable { get; private set; }
        public bool IsDepthAvailable { get; private set; }

        private void Awake()
        {
            // Auto-find компоненты если не назначены
            if (arSession == null) arSession = GetComponent<ARSession>();
            if (arSessionOrigin == null) arSessionOrigin = FindObjectOfType<XROrigin>();
            if (arCameraManager == null) arCameraManager = FindObjectOfType<ARCameraManager>();
            if (arRaycastManager == null) arRaycastManager = FindObjectOfType<ARRaycastManager>();
            if (arPlaneManager == null) arPlaneManager = FindObjectOfType<ARPlaneManager>();
            if (arOcclusionManager == null) arOcclusionManager = FindObjectOfType<AROcclusionManager>();
            if (arMeshManager == null) arMeshManager = FindObjectOfType<ARMeshManager>();
        }

        private void Start()
        {
            InitializeARSession();
        }

        private void OnEnable()
        {
            if (arSession != null)
            {
                ARSession.stateChanged += OnARSessionStateChanged;
            }
        }

        private void OnDisable()
        {
            ARSession.stateChanged -= OnARSessionStateChanged;
        }

        /// <summary>
        /// Инициализирует AR-сессию с учётом возможностей платформы
        /// </summary>
        private void InitializeARSession()
        {
            Debug.Log("[ARManager] Initializing AR Session...");
            PlatformAdapter.LogDeviceCapabilities();

            // Проверяем возможности устройства
            IsMeshScanningAvailable = PlatformAdapter.SupportsMeshScanning;
            IsDepthAvailable = PlatformAdapter.SupportsDepth;

            // Настраиваем компоненты на основе возможностей
            ConfigureARComponents();
        }

        /// <summary>
        /// Настраивает AR компоненты на основе возможностей устройства
        /// </summary>
        private void ConfigureARComponents()
        {
            // Plane Detection
            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = enablePlaneDetection;
                if (enablePlaneDetection)
                {
                    // Включаем обнаружение горизонтальных и вертикальных плоскостей
                    arPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
                    Debug.Log("[ARManager] Plane detection enabled (Horizontal + Vertical)");
                }
            }

            // Mesh Scanning (iOS LiDAR)
            if (arMeshManager != null)
            {
                if (enableMeshScanning && IsMeshScanningAvailable)
                {
                    arMeshManager.enabled = true;
                    Debug.Log("[ARManager] Mesh scanning enabled (LiDAR available)");
                }
                else
                {
                    // Безопасно отключаем mesh scanning если не поддерживается
                    if (arMeshManager != null && arMeshManager.enabled)
                    {
                        arMeshManager.enabled = false;
                    }
                    
                    if (enableMeshScanning && !IsMeshScanningAvailable)
                    {
                        Debug.LogWarning("[ARManager] Mesh scanning requested but not available on this device");
                    }
                }
            }

            // Depth Occlusion
            if (arOcclusionManager != null)
            {
                bool shouldBeEnabled = enableDepthOcclusion && IsDepthAvailable;
                
                // Отключаем компонент, только если он должен быть выключен И мы не в редакторе,
                // чтобы избежать сбоя NullReferenceException в симуляции.
                if (!shouldBeEnabled && arOcclusionManager.enabled)
                {
            #if !UNITY_EDITOR
                    arOcclusionManager.enabled = false;
            #else
                    // В редакторе просто оставляем компонент как есть, но не используем его.
            #endif
                }
                else if (shouldBeEnabled && !arOcclusionManager.enabled)
                {
                    arOcclusionManager.enabled = true;
                }

                if (arOcclusionManager.enabled)
                {
                    arOcclusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                    Debug.Log("[ARManager] Depth occlusion enabled");
                }
                else if (enableDepthOcclusion && !IsDepthAvailable)
                {
                    Debug.LogWarning("[ARManager] Depth occlusion requested but not available on this device");
                }
            }
        }

        /// <summary>
        /// Обработчик изменения состояния AR-сессии
        /// </summary>
        private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log($"[ARManager] AR Session state changed: {args.state}");

            switch (args.state)
            {
                case ARSessionState.SessionInitializing:
                    IsSessionReady = false;
                    break;

                case ARSessionState.SessionTracking:
                    IsSessionReady = true;
                    OnARSessionInitialized?.Invoke();
                    Debug.Log("[ARManager] AR Session is ready and tracking");
                    break;

                case ARSessionState.None:
                case ARSessionState.Unsupported:
                    IsSessionReady = false;
                    OnARSessionFailed?.Invoke();
                    Debug.LogError("[ARManager] AR Session failed or unsupported");
                    break;
            }
        }

        /// <summary>
        /// Выполняет raycast в AR сцену
        /// </summary>
        public bool TryRaycast(Vector2 screenPoint, out ARRaycastHit hit, TrackableType trackableTypes = TrackableType.AllTypes)
        {
            hit = default;

            if (arRaycastManager == null || !IsSessionReady)
                return false;

            var hits = new System.Collections.Generic.List<ARRaycastHit>();
            if (arRaycastManager.Raycast(screenPoint, hits, trackableTypes))
            {
                hit = hits[0]; // Берём ближайшее попадание
                return true;
            }

            return false;
        }

        /// <summary>
        /// Переключает видимость обнаруженных плоскостей
        /// </summary>
        public void SetPlanesVisible(bool visible)
        {
            if (arPlaneManager != null)
            {
                foreach (var plane in arPlaneManager.trackables)
                {
                    plane.gameObject.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// Переключает видимость mesh сканирования
        /// </summary>
        public void SetMeshVisible(bool visible)
        {
            if (arMeshManager != null)
            {
                foreach (var mesh in arMeshManager.meshes)
                {
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = visible;
                    }
                }
            }
        }

        /// <summary>
        /// Получает информацию о состоянии AR
        /// </summary>
        public string GetARStatus()
        {
            return $"AR Ready: {IsSessionReady}\n" +
                   $"Platform: {PlatformAdapter.CurrentPlatform}\n" +
                   $"Mesh Scanning: {IsMeshScanningAvailable}\n" +
                   $"Depth: {IsDepthAvailable}\n" +
                   $"Planes: {(arPlaneManager != null ? arPlaneManager.trackables.count : 0)}";
        }
    }
}

