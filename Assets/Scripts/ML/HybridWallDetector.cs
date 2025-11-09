using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RemaluxAR.ML
{
    /// <summary>
    /// ГИБРИДНЫЙ ДЕТЕКТОР СТЕН - Комбинирует 3 источника:
    /// 1. Depth Anything V2 - геометрия (карта глубины)
    /// 2. DeepLabV3/DETR - фильтрация объектов (person, furniture)
    /// 3. AR Planes - вертикальные плоскости
    /// 
    /// Алгоритм:
    /// - Depth map → находим вертикальные плоскости с consistent depth
    /// - Semantic segmentation → исключаем non-wall объекты (person, furniture)
    /// - AR Planes → валидация и точное позиционирование
    /// </summary>
    public class HybridWallDetector : MonoBehaviour
    {
        [Header("ML Components")]
        [Tooltip("Depth Estimation Manager (Depth Anything V2)")]
        [SerializeField] private DepthEstimationManager depthManager;
        
        [Tooltip("Semantic Segmentation Manager (DeepLabV3 для фильтрации объектов)")]
        [SerializeField] private MLSegmentationManager segmentationManager;
        
        [Header("AR Components")]
        [Tooltip("AR Plane Manager для вертикальных плоскостей")]
        [SerializeField] private ARPlaneManager arPlaneManager;
        
        [Header("Wall Detection Parameters - ULTRA SOFT DEBUG")]
        [Tooltip("🔥 DEBUG: 0.1 м² - ВИДИМ ПОЧТИ ВСЁ!")]
        [SerializeField] private float minWallArea = 0.1f;
        
        // [Tooltip("Depth consistency threshold (0-1). Меньше = строже")]
        // [SerializeField] private float depthConsistencyThreshold = 0.05f; // Временно не используется (depth отключен)
        
        [Tooltip("🔥 DEBUG: -1.0 м - ПРИНИМАЕМ ВСЁ, ДАЖЕ ПОЛ!")]
        [SerializeField] private float minWallHeightFromFloor = -1.0f;
        
        [Header("Object Filtering (ВРЕМЕННО ОТКЛЮЧЕНО)")]
        [Tooltip("⚠️ ОТКЛЮЧЕНО: DeepLabV3 PASCAL VOC не имеет класса 'wall'")]
        [SerializeField] private bool filterPeople = false;
        
        [Tooltip("⚠️ ОТКЛЮЧЕНО: может давать false positives")]
        [SerializeField] private bool filterFurniture = false;
        
        [Tooltip("⚠️ ОТКЛЮЧЕНО: может давать false positives")]
        [SerializeField] private bool filterElectronics = false;
        
        // COCO class IDs (DeepLabV3 PASCAL VOC)
        private const int PERSON_CLASS = 15;
        private const int CHAIR_CLASS = 9;
        private const int SOFA_CLASS = 18;
        private const int DININGTABLE_CLASS = 11;
        private const int TV_CLASS = 20;
        
        // Обнаруженные стены
        private Dictionary<TrackableId, WallInfo> detectedWalls = new Dictionary<TrackableId, WallInfo>();
        
        /// <summary>
        /// Информация об обнаруженной стене
        /// </summary>
        public class WallInfo
        {
            public ARPlane arPlane;
            public float averageDepth;
            public float depthConsistency;
            public bool hasNonWallObjects;
            public Vector3 center;
            public Vector2 size;
            public float confidence; // 0-1
        }
        
        private void Awake()
        {
            if (depthManager == null)
                depthManager = FindObjectOfType<DepthEstimationManager>();
            
            if (segmentationManager == null)
                segmentationManager = FindObjectOfType<MLSegmentationManager>();
            
            if (arPlaneManager == null)
                arPlaneManager = FindObjectOfType<ARPlaneManager>();
        }
        
        private void OnEnable()
        {
            if (arPlaneManager != null)
            {
                arPlaneManager.planesChanged += OnPlanesChanged;
                Debug.Log("[HybridWallDetector] ✅ Подписаны на AR Plane events");
            }
        }
        
        private void OnDisable()
        {
            if (arPlaneManager != null)
            {
                arPlaneManager.planesChanged -= OnPlanesChanged;
            }
        }
        
        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            // DEBUG: Логируем ВСЕ изменения плоскостей
            if (args.added.Count > 0)
            {
                Debug.Log($"[HybridWallDetector] 🆕 ARKit нашел {args.added.Count} новых плоскостей");
            }
            
            if (args.updated.Count > 0)
            {
                Debug.Log($"[HybridWallDetector] 🔄 ARKit обновил {args.updated.Count} плоскостей");
            }
            
            if (args.removed.Count > 0)
            {
                Debug.Log($"[HybridWallDetector] ❌ ARKit удалил {args.removed.Count} плоскостей");
            }
            
            foreach (var plane in args.added)
            {
                AnalyzePlane(plane);
            }
            
            foreach (var plane in args.updated)
            {
                AnalyzePlane(plane);
            }
            
            foreach (var plane in args.removed)
            {
                if (detectedWalls.ContainsKey(plane.trackableId))
                {
                    detectedWalls.Remove(plane.trackableId);
                }
            }
        }
        
        /// <summary>
        /// Анализирует AR Plane с использованием Depth + Segmentation
        /// </summary>
        private void AnalyzePlane(ARPlane plane)
        {
            Vector2 planeSize = plane.size;
            float planeArea = planeSize.x * planeSize.y;
            float centerY = plane.transform.position.y;
            
            // DEBUG: Логируем ВСЕ плоскости до фильтрации
            Debug.Log($"[HybridWallDetector] 📐 Плоскость: " +
                     $"ID={plane.trackableId}, " +
                     $"Alignment={plane.alignment}, " +
                     $"Size={planeSize.x:F2}×{planeSize.y:F2}м, " +
                     $"Area={planeArea:F2}м², " +
                     $"CenterY={centerY:F2}м");
            
            // Фильтр 1: Только вертикальные плоскости
            if (plane.alignment != PlaneAlignment.Vertical)
            {
                Debug.Log($"[HybridWallDetector] ❌ Игнор: НЕ вертикальная (alignment={plane.alignment})");
                return;
            }
            
            // Фильтр 2: Минимальная площадь
            if (planeArea < minWallArea)
            {
                Debug.Log($"[HybridWallDetector] ⏳ Ожидаем роста: {planeArea:F2}м² < {minWallArea}м²");
                return;
            }
            
            // Фильтр 3: Минимальная высота от пола (ВРЕМЕННО ОТКЛЮЧЕН ДЛЯ DEBUG)
            if (false && centerY < minWallHeightFromFloor)
            {
                Debug.Log($"[HybridWallDetector] ❌ Игнор: слишком низко (centerY={centerY:F2} < {minWallHeightFromFloor}м)");
                return;
            }
            
            Debug.Log($"[HybridWallDetector] ℹ️ CenterY={centerY:F2}м (фильтр отключен для DEBUG)");
            
            // === DEPTH ANALYSIS (ВРЕМЕННО ОТКЛЮЧЕНО) ===
            // Depth estimation временно недоступен - требуется AR frame integration
            // Используем только AR Planes + Segmentation
            
            Camera arCamera = Camera.main;
            if (arCamera == null)
                return;
            
            Vector3 screenPos = arCamera.WorldToViewportPoint(plane.center);
            Vector2 normalizedPos = new Vector2(screenPos.x, screenPos.y);
            
            // Fallback: без depth проверки (пропускаем)
            float averageDepth = 0.5f; // Fallback значение
            
            // NOTE: Depth check будет добавлен когда AR frame integration готов
            
            // === SEMANTIC SEGMENTATION FILTERING ===
            bool hasNonWallObjects = CheckForNonWallObjects(normalizedPos);
            
            if (hasNonWallObjects)
            {
                Debug.LogWarning($"[HybridWallDetector] ⚠️ Обнаружены non-wall объекты (person/furniture): {planeSize}");
                
                // Не отбрасываем полностью, но снижаем confidence
            }
            
            // === COMPUTE CONFIDENCE ===
            float confidence = ComputeWallConfidence(plane, averageDepth, !hasNonWallObjects);
            
            if (confidence > 0.5f)
            {
                WallInfo wallInfo = new WallInfo
                {
                    arPlane = plane,
                    averageDepth = averageDepth,
                    depthConsistency = 0.95f, // TODO: вычислить реально
                    hasNonWallObjects = hasNonWallObjects,
                    center = plane.center,
                    size = planeSize,
                    confidence = confidence
                };
                
                detectedWalls[plane.trackableId] = wallInfo;
                
                Debug.Log($"[HybridWallDetector] ✅ СТЕНА обнаружена: {planeSize.x:F2}м × {planeSize.y:F2}м, " +
                         $"depth: {averageDepth:F2}, confidence: {confidence:F2}");
            }
        }
        
        /// <summary>
        /// Проверяет наличие non-wall объектов в регионе
        /// ВРЕМЕННО ОТКЛЮЧЕНО для быстрого тестирования
        /// </summary>
        private bool CheckForNonWallObjects(Vector2 normalizedPos)
        {
            // ВРЕМЕННО: Отключаем segmentation фильтр так как он может давать false positives
            // DeepLabV3 обучена на PASCAL VOC (нет класса "wall")
            // TODO: Заменить на ADE20K модель для production
            
            if (segmentationManager == null || !segmentationManager.IsInitialized)
            {
                // Нет segmentation - пропускаем все
                return false;
            }
            
            // МОЖНО ВКЛЮЧИТЬ ВРУЧНУЮ в Inspector:
            // filterPeople = true, filterFurniture = true, filterElectronics = true
            
            if (!filterPeople && !filterFurniture && !filterElectronics)
            {
                // Все фильтры отключены
                return false;
            }
            
            int pixelClass = segmentationManager.GetPixelClass(normalizedPos);
            
            // DeepLabV3 PASCAL VOC classes
            if (filterPeople && pixelClass == PERSON_CLASS)
            {
                Debug.Log($"[HybridWallDetector] 🚶 Обнаружен человек в регионе");
                return true;
            }
            
            if (filterFurniture && (pixelClass == CHAIR_CLASS || pixelClass == SOFA_CLASS || pixelClass == DININGTABLE_CLASS))
            {
                Debug.Log($"[HybridWallDetector] 🪑 Обнаружена мебель в регионе");
                return true;
            }
            
            if (filterElectronics && pixelClass == TV_CLASS)
            {
                Debug.Log($"[HybridWallDetector] 📺 Обнаружена техника в регионе");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Вычисляет confidence score для стены
        /// </summary>
        private float ComputeWallConfidence(ARPlane plane, float depth, bool noObjects)
        {
            float confidence = 0.5f; // Base confidence from AR Plane
            
            // Depth consistency boost
            if (depthManager != null && depthManager.IsInitialized)
            {
                confidence += 0.2f;
            }
            
            // No non-wall objects boost
            if (noObjects)
            {
                confidence += 0.2f;
            }
            
            // Size boost (larger = more confident)
            float area = plane.size.x * plane.size.y;
            if (area > 2.0f)
                confidence += 0.1f;
            
            return Mathf.Clamp01(confidence);
        }
        
        /// <summary>
        /// Получает информацию о стене по TrackableId
        /// </summary>
        public WallInfo GetWallInfo(TrackableId trackableId)
        {
            return detectedWalls.ContainsKey(trackableId) ? detectedWalls[trackableId] : null;
        }
        
        /// <summary>
        /// Проверяет является ли AR Plane стеной
        /// </summary>
        public bool IsWall(ARPlane plane)
        {
            return detectedWalls.ContainsKey(plane.trackableId);
        }
        
        /// <summary>
        /// Возвращает все обнаруженные стены
        /// </summary>
        public Dictionary<TrackableId, WallInfo> GetAllWalls()
        {
            return detectedWalls;
        }
        
        /// <summary>
        /// Проверяет является ли screen position стеной (для клика)
        /// </summary>
        public bool IsWallAtScreenPosition(Vector2 screenPosition, out WallInfo wallInfo)
        {
            wallInfo = null;
            
            Camera arCamera = Camera.main;
            if (arCamera == null)
                return false;
            
            // Raycast от screen position
            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            
            foreach (var kvp in detectedWalls)
            {
                ARPlane plane = kvp.Value.arPlane;
                
                // Проверяем пересечение луча с плоскостью
                Plane geometryPlane = new Plane(plane.transform.up, plane.center);
                
                if (geometryPlane.Raycast(ray, out float distance))
                {
                    Vector3 hitPoint = ray.GetPoint(distance);
                    
                    // Проверяем что точка внутри bounds плоскости
                    Vector3 localPoint = plane.transform.InverseTransformPoint(hitPoint);
                    
                    if (Mathf.Abs(localPoint.x) <= plane.size.x / 2f &&
                        Mathf.Abs(localPoint.y) <= plane.size.y / 2f)
                    {
                        wallInfo = kvp.Value;
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}

