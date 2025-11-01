using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RemaluxAR.ML
{
    /// <summary>
    /// Управляет Depth Estimation через Depth Anything V2 модель
    /// Возвращает карту глубины (depth map) для каждого пикселя
    /// </summary>
    public class DepthEstimationManager : MonoBehaviour
    {
        #if UNITY_IOS
        // ====================================
        // iOS CoreML Depth Plugin (P/Invoke)
        // ====================================
        
        [DllImport("__Internal")]
        private static extern bool CoreMLDepth_Initialize(string modelPath);
        
        [DllImport("__Internal")]
        private static extern void CoreMLDepth_EstimateDepth(float[] outputBuffer, int bufferSize, int resolution);
        
        [DllImport("__Internal")]
        private static extern void CoreMLDepth_Cleanup();
        
        [DllImport("__Internal")]
        private static extern bool CoreMLDepth_IsInitialized();
        #endif
        
        [Header("Depth Model Settings")]
        [Tooltip("Разрешение depth map (512x512 для Depth Anything V2 Small)")]
        [SerializeField] private int depthResolution = 512;
        
        [Tooltip("Интервал между depth estimation (секунды). 0.1 = 10 FPS")]
        [SerializeField] private float estimationInterval = 0.1f;
        
        [Header("References")]
        [SerializeField] private ARCameraManager arCameraManager;
        
        // Текущая depth map (512x512 floats, values 0.0-1.0)
        private float[] currentDepthMap;
        private float lastEstimationTime = 0f;
        private bool isInitialized = false;
        
        // Статистика
        private int totalEstimations = 0;
        private float averageEstimationTime = 0f;
        
        /// <summary>
        /// Проверяет инициализирована ли depth модель
        /// </summary>
        public bool IsInitialized => isInitialized;
        
        /// <summary>
        /// Текущая depth map (read-only)
        /// </summary>
        public float[] CurrentDepthMap => currentDepthMap;
        
        private void Awake()
        {
            if (arCameraManager == null)
            {
                arCameraManager = FindObjectOfType<ARCameraManager>();
            }
        }
        
        private void Start()
        {
            InitializeDepthModel();
        }
        
        private void InitializeDepthModel()
        {
            Debug.Log("[DepthEstimation] Загрузка Depth Anything V2 модели...");
            Debug.LogWarning("[DepthEstimation] ⚠️ AR Camera frame integration в разработке - depth пока недоступен");
            Debug.Log("[DepthEstimation] ℹ️  Система работает с AR Planes + Segmentation (без depth)");
            
            #if UNITY_IOS
            // ВРЕМЕННО ОТКЛЮЧЕНО: AR frame integration требует доработки
            // InitializeCoreMLDepth();
            Debug.Log("[DepthEstimation] ℹ️  Depth модель пропущена для быстрого тестирования");
            #elif UNITY_ANDROID
            Debug.LogWarning("[DepthEstimation] Android TFLite не реализован!");
            #else
            Debug.LogWarning("[DepthEstimation] Depth estimation доступен только на iOS/Android!");
            #endif
        }
        
        #if UNITY_IOS
        private void InitializeCoreMLDepth()
        {
            // Путь к модели в StreamingAssets
            string modelName = "DepthAnythingV2SmallF16P6.mlpackage";
            string modelPath = System.IO.Path.Combine(Application.streamingAssetsPath, modelName);
            
            Debug.Log($"[DepthEstimation] Model path: {modelPath}");
            
            bool success = CoreMLDepth_Initialize(modelPath);
            
            if (success)
            {
                Debug.Log("[DepthEstimation] ✅ CoreML Depth модель инициализирована!");
                
                // Выделяем буфер для depth map
                int totalPixels = depthResolution * depthResolution;
                currentDepthMap = new float[totalPixels];
                
                isInitialized = true;
                
                Debug.Log($"[DepthEstimation] Depth resolution: {depthResolution}x{depthResolution}");
                Debug.Log($"[DepthEstimation] Estimation interval: {estimationInterval}s (~{1f/estimationInterval:F1} FPS)");
            }
            else
            {
                Debug.LogError("[DepthEstimation] ❌ Не удалось инициализировать CoreML Depth модель!");
            }
        }
        #endif
        
        private void Update()
        {
            if (!isInitialized)
                return;
            
            // Throttle: выполняем depth estimation с заданным интервалом
            if (Time.time - lastEstimationTime >= estimationInterval)
            {
                RunDepthEstimation();
                lastEstimationTime = Time.time;
            }
        }
        
        private void RunDepthEstimation()
        {
            #if UNITY_IOS
            CoreMLDepth_EstimateDepth(currentDepthMap, currentDepthMap.Length, depthResolution);
            
            totalEstimations++;
            
            // Логируем статистику каждые 5 секунд
            if (totalEstimations % 50 == 0)
            {
                float avgTime = 1000f * estimationInterval;
                Debug.Log($"[DepthEstimation] Avg estimation time: ~{avgTime:F1}ms ({1f/estimationInterval:F1} FPS)");
            }
            #endif
        }
        
        /// <summary>
        /// Получает глубину для заданной позиции экрана (normalized 0-1)
        /// </summary>
        public float GetDepthAtPosition(Vector2 normalizedPosition)
        {
            if (!isInitialized || currentDepthMap == null)
                return 0f;
            
            // Конвертируем normalized screen position в pixel coordinates
            int x = Mathf.Clamp((int)(normalizedPosition.x * depthResolution), 0, depthResolution - 1);
            int y = Mathf.Clamp((int)(normalizedPosition.y * depthResolution), 0, depthResolution - 1);
            
            int index = y * depthResolution + x;
            
            if (index >= 0 && index < currentDepthMap.Length)
            {
                return currentDepthMap[index];
            }
            
            return 0f;
        }
        
        /// <summary>
        /// Проверяет является ли регион вертикальной плоскостью (стеной)
        /// на основе consistency глубины
        /// </summary>
        public bool IsVerticalPlane(Vector2 normalizedCenter, float radius = 0.05f)
        {
            if (!isInitialized || currentDepthMap == null)
                return false;
            
            float centerDepth = GetDepthAtPosition(normalizedCenter);
            
            // Проверяем depth в окрестности точки
            int samples = 8;
            int consistentSamples = 0;
            float depthThreshold = 0.05f; // 5% variance
            
            for (int i = 0; i < samples; i++)
            {
                float angle = (i / (float)samples) * Mathf.PI * 2f;
                Vector2 samplePos = normalizedCenter + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
                
                float sampleDepth = GetDepthAtPosition(samplePos);
                
                if (Mathf.Abs(sampleDepth - centerDepth) < depthThreshold)
                {
                    consistentSamples++;
                }
            }
            
            // Если > 75% samples имеют consistent depth → это плоская поверхность
            return (consistentSamples / (float)samples) > 0.75f;
        }
        
        private void OnDestroy()
        {
            #if UNITY_IOS
            if (isInitialized)
            {
                CoreMLDepth_Cleanup();
            }
            #endif
        }
    }
}

