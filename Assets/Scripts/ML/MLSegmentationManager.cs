using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RemaluxAR.ML
{
    /// <summary>
    /// Управляет ML semantic segmentation для точного обнаружения стен
    /// Использует SegFormer-B0 модель для pixel-level классификации
    /// </summary>
    public class MLSegmentationManager : MonoBehaviour
    {
        #if UNITY_IOS
        // ====================================
        // iOS CoreML Native Plugin (P/Invoke)
        // ====================================
        
        [DllImport("__Internal")]
        private static extern bool CoreML_Initialize(string modelPath);
        
        [DllImport("__Internal")]
        private static extern void CoreML_SegmentCurrentFrame(byte[] outputBuffer, int bufferSize, int resolution);
        
        [DllImport("__Internal")]
        private static extern void CoreML_SegmentImage(string imagePath, byte[] outputBuffer, int bufferSize, int resolution);
        
        [DllImport("__Internal")]
        private static extern void CoreML_Cleanup();
        
        [DllImport("__Internal")]
        private static extern bool CoreML_IsInitialized();
        #endif
        
        #if UNITY_ANDROID
        // ====================================
        // Android TFLite Native Plugin (P/Invoke)
        // ====================================
        
        [DllImport("libtflite-segmentation")]
        private static extern bool TFLite_Initialize(string modelPath);
        
        [DllImport("libtflite-segmentation")]
        private static extern void TFLite_SegmentCurrentFrame(byte[] outputBuffer, int bufferSize, int resolution);
        
        [DllImport("libtflite-segmentation")]
        private static extern void TFLite_Cleanup();
        
        [DllImport("libtflite-segmentation")]
        private static extern bool TFLite_IsInitialized();
        #endif
        [Header("ML Model Settings")]
        [Tooltip("Путь к ONNX модели в Assets/ML")]
        [SerializeField] private string modelPath = "ML/optimum:segformer-b0-finetuned-ade-512-512/model.onnx";
        
        [Tooltip("Включить ML segmentation (требует CoreML на iOS / TFLite на Android)")]
        [SerializeField] private bool enableMLSegmentation = true; // ✅ ВКЛЮЧЕНО для ML-first подхода!
        
        [Header("Performance Settings")]
        [Tooltip("Интервал между ML inference (секунды). 0.2 = 5 FPS inference")]
        [SerializeField] private float inferenceInterval = 0.2f;
        
        [Tooltip("Разрешение для inference (меньше = быстрее)")]
        [SerializeField] private int inferenceResolution = 512;
        
        [Header("Class IDs (ADE20K dataset)")]
        private const int WALL_CLASS = 0;
        private const int DOOR_CLASS = 14;
        private const int PERSON_CLASS = 12;
        private const int SOFA_CLASS = 23;
        private const int CHAIR_CLASS = 19;
        private const int TABLE_CLASS = 15;
        private const int TV_CLASS = 89;
        private const int FLOOR_CLASS = 3;
        private const int CEILING_CLASS = 5;
        
        [Header("References")]
        [SerializeField] private ARCameraManager arCameraManager;
        
        // Текущая маска сегментации (512x512 пикселей, каждый пиксель = class ID)
        private byte[] currentSegmentationMask;
        private float lastInferenceTime = 0f;
        private bool isInitialized = false;
        
        // Статистика
        private int totalInferences = 0;
        private float averageInferenceTime = 0f;

        /// <summary>
        /// Проверяет инициализирована ли ML модель
        /// </summary>
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (arCameraManager == null)
            {
                arCameraManager = FindObjectOfType<ARCameraManager>();
            }
        }

        private void Start()
        {
            if (enableMLSegmentation)
            {
                InitializeMLModel();
            }
            else
            {
                Debug.LogWarning("[MLSegmentation] ML segmentation отключена. Включите 'Enable ML Segmentation' для активации.");
            }
        }

        /// <summary>
        /// Инициализация ML модели
        /// </summary>
        private void InitializeMLModel()
        {
            #if UNITY_IOS
            InitializeCoreMLModel();
            #elif UNITY_ANDROID
            InitializeTFLiteModel();
#else
            Debug.LogError("[MLSegmentation] ML segmentation поддерживается только на iOS (CoreML) и Android (TFLite)!");
            enableMLSegmentation = false;
            return;
#endif
            
            // Инициализация буфера для маски
            currentSegmentationMask = new byte[inferenceResolution * inferenceResolution];
            
            isInitialized = true;
            Debug.Log($"[MLSegmentation] Модель инициализирована: {modelPath}");
            Debug.Log($"[MLSegmentation] Inference resolution: {inferenceResolution}x{inferenceResolution}");
            Debug.Log($"[MLSegmentation] Inference interval: {inferenceInterval}s (~{1f/inferenceInterval:F0} FPS)");
        }

        #if UNITY_IOS
        /// <summary>
        /// Инициализация CoreML модели на iOS
        /// </summary>
        private void InitializeCoreMLModel()
        {
            Debug.Log("[MLSegmentation] Загрузка CoreML модели...");
            
            // Путь к CoreML модели в StreamingAssets
            string fullModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "SegFormerB0_FP16.mlmodel");
            
            Debug.Log($"[MLSegmentation] Model path: {fullModelPath}");
            
            // Вызов native iOS плагина
            bool success = CoreML_Initialize(fullModelPath);
            
            if (success)
            {
                Debug.Log("[MLSegmentation] ✅ CoreML модель инициализирована!");
            }
            else
            {
                Debug.LogError("[MLSegmentation] ❌ Не удалось инициализировать CoreML модель!");
                enableMLSegmentation = false;
            }
        }
        #endif

        #if UNITY_ANDROID
        /// <summary>
        /// Инициализация TFLite модели на Android
        /// </summary>
        private void InitializeTFLiteModel()
        {
            // TODO: Вызов native Android плагина для загрузки TFLite модели
            Debug.Log("[MLSegmentation] TODO: Загрузка TFLite модели...");
            // TFLitePlugin.Initialize(modelPath);
        }
        #endif

        private void Update()
        {
            if (!isInitialized || !enableMLSegmentation) return;

            // Throttling: запускаем inference только через определённый интервал
            if (Time.time - lastInferenceTime >= inferenceInterval)
            {
                RunMLInference();
            lastInferenceTime = Time.time;
            }
        }

        /// <summary>
        /// Запуск ML inference на текущем кадре AR камеры
        /// </summary>
        private void RunMLInference()
        {
            float startTime = Time.realtimeSinceStartup;
            
            #if UNITY_IOS
            // Вызов CoreML inference
            int bufferSize = inferenceResolution * inferenceResolution;
            CoreML_SegmentCurrentFrame(currentSegmentationMask, bufferSize, inferenceResolution);
            #elif UNITY_ANDROID
            // Вызов TFLite inference
            int bufferSize = inferenceResolution * inferenceResolution;
            TFLite_SegmentCurrentFrame(currentSegmentationMask, bufferSize, inferenceResolution);
            #endif
            
            float inferenceTime = Time.realtimeSinceStartup - startTime;
            totalInferences++;
            
            // Вычисляем среднее время inference
            averageInferenceTime = (averageInferenceTime * (totalInferences - 1) + inferenceTime) / totalInferences;
            
            if (totalInferences % 30 == 0) // Логируем каждые 30 инференсов
            {
                Debug.Log($"[MLSegmentation] Avg inference time: {averageInferenceTime * 1000f:F1}ms ({1f/averageInferenceTime:F1} FPS)");
            }
        }

        /// <summary>
        /// Получить класс пикселя в экранной позиции
        /// </summary>
        /// <param name="screenPosition">Позиция на экране (0-1 normalized)</param>
        /// <returns>Class ID (0-149) или -1 если недоступно</returns>
        public int GetPixelClass(Vector2 screenPosition)
        {
            if (!isInitialized || currentSegmentationMask == null)
            {
                return -1;
            }
            
            // Конвертация из screen space в mask space
            int x = Mathf.Clamp((int)(screenPosition.x * inferenceResolution), 0, inferenceResolution - 1);
            int y = Mathf.Clamp((int)(screenPosition.y * inferenceResolution), 0, inferenceResolution - 1);
            
            int index = y * inferenceResolution + x;
            return currentSegmentationMask[index];
        }

        /// <summary>
        /// Проверить, является ли пиксель стеной
        /// </summary>
        public bool IsWall(Vector2 screenPosition)
        {
            return GetPixelClass(screenPosition) == WALL_CLASS;
        }

        /// <summary>
        /// Проверить, является ли пиксель дверью
        /// </summary>
        public bool IsDoor(Vector2 screenPosition)
        {
            return GetPixelClass(screenPosition) == DOOR_CLASS;
        }

        /// <summary>
        /// Flood Fill алгоритм для выделения всей стены от точки клика
        /// Возвращает список пикселей, принадлежащих той же стене
        /// </summary>
        /// <param name="clickPosition">Позиция клика (0-1 normalized)</param>
        /// <returns>Список пикселей стены или null если клик не на стене</returns>
        public HashSet<Vector2Int> FloodFillWall(Vector2 clickPosition)
        {
            if (!isInitialized || currentSegmentationMask == null)
        {
                Debug.LogWarning("[MLSegmentation] Модель не инициализирована!");
                return null;
            }
            
            int startX = Mathf.Clamp((int)(clickPosition.x * inferenceResolution), 0, inferenceResolution - 1);
            int startY = Mathf.Clamp((int)(clickPosition.y * inferenceResolution), 0, inferenceResolution - 1);
            
            int startClass = currentSegmentationMask[startY * inferenceResolution + startX];
            
            // Проверяем, что клик был на стене
            if (startClass != WALL_CLASS)
            {
                Debug.LogWarning($"[MLSegmentation] Клик не на стене! Class: {startClass} (expected {WALL_CLASS})");
                return null;
            }
            
            // Flood Fill алгоритм (BFS)
            HashSet<Vector2Int> wallPixels = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            
            Vector2Int startPixel = new Vector2Int(startX, startY);
            queue.Enqueue(startPixel);
            visited.Add(startPixel);
            
            // 4-connected neighbors (up, down, left, right)
            Vector2Int[] neighbors = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // up
                new Vector2Int(0, -1),  // down
                new Vector2Int(1, 0),   // right
                new Vector2Int(-1, 0)   // left
            };
            
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                wallPixels.Add(current);
                
                // Проверяем всех соседей
                foreach (var offset in neighbors)
                {
                    Vector2Int neighbor = current + offset;
                    
                    // Проверка границ
                    if (neighbor.x < 0 || neighbor.x >= inferenceResolution ||
                        neighbor.y < 0 || neighbor.y >= inferenceResolution)
                    {
                        continue;
                    }
                    
                    // Уже посещали?
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }
                    
                    visited.Add(neighbor);
                    
                    // Проверяем класс соседнего пикселя
                    int neighborClass = currentSegmentationMask[neighbor.y * inferenceResolution + neighbor.x];
                    
                    // Если сосед тоже стена - добавляем в очередь
                    if (neighborClass == WALL_CLASS)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
                
                // Защита от бесконечного цикла
                if (wallPixels.Count > inferenceResolution * inferenceResolution / 2)
                {
                    Debug.LogWarning("[MLSegmentation] Flood fill превысил лимит пикселей!");
                    break;
                }
            }
            
            Debug.Log($"[MLSegmentation] ✅ Flood Fill завершён: {wallPixels.Count} пикселей стены");
            return wallPixels;
        }

        /// <summary>
        /// Получить текущую маску сегментации (для отладки)
        /// </summary>
        public byte[] GetCurrentMask()
        {
            return currentSegmentationMask;
        }

        /// <summary>
        /// Получить разрешение маски сегментации
        /// </summary>
        public int GetMaskResolution()
        {
            return inferenceResolution;
        }

        /// <summary>
        /// Найти контур стены (границу между wall и non-wall пикселями)
        /// Используется для создания mesh с точными границами как в Dulux
        /// </summary>
        /// <param name="wallPixels">Множество пикселей стены из FloodFill</param>
        /// <returns>Список точек контура стены</returns>
        public List<Vector2Int> FindWallContour(HashSet<Vector2Int> wallPixels)
        {
            if (wallPixels == null || wallPixels.Count == 0)
            {
                return null;
            }
            
            List<Vector2Int> contourPoints = new List<Vector2Int>();
            
            // Направления для проверки соседей (8-connected)
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // up
                new Vector2Int(1, 1),   // up-right
                new Vector2Int(1, 0),   // right
                new Vector2Int(1, -1),  // down-right
                new Vector2Int(0, -1),  // down
                new Vector2Int(-1, -1), // down-left
                new Vector2Int(-1, 0),  // left
                new Vector2Int(-1, 1)   // up-left
            };
            
            // Для каждого пикселя стены проверяем, есть ли хотя бы один сосед не-стена
            foreach (var pixel in wallPixels)
            {
                bool isEdge = false;
                
                foreach (var dir in directions)
                {
                    Vector2Int neighbor = pixel + dir;
                    
                    // Если сосед за границами ИЛИ не принадлежит стене - это граница
                    if (neighbor.x < 0 || neighbor.x >= inferenceResolution ||
                        neighbor.y < 0 || neighbor.y >= inferenceResolution ||
                        !wallPixels.Contains(neighbor))
                    {
                        isEdge = true;
                        break;
                    }
                }
                
                if (isEdge)
                {
                    contourPoints.Add(pixel);
                }
            }
            
            Debug.Log($"[MLSegmentation] ✅ Найдено {contourPoints.Count} точек контура стены");
            return contourPoints;
        }

        /// <summary>
        /// Получить статистику ML inference
        /// </summary>
        public string GetStats()
        {
            if (!isInitialized)
            {
                return "ML не инициализирована";
            }
            
            return $"Инференсов: {totalInferences}\n" +
                   $"Среднее время: {averageInferenceTime * 1000f:F1}ms\n" +
                   $"FPS: {1f/averageInferenceTime:F1}";
        }

        private void OnDestroy()
        {
            // Очистка ресурсов
            currentSegmentationMask = null;
            
            #if UNITY_IOS
            if (isInitialized)
            {
                CoreML_Cleanup();
                Debug.Log("[MLSegmentation] CoreML ресурсы очищены");
            }
            #elif UNITY_ANDROID
            if (isInitialized)
            {
                TFLite_Cleanup();
                Debug.Log("[MLSegmentation] TFLite ресурсы очищены");
            }
            #endif
    }
}
}
