using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Упрощённый компонент для обнаружения стен и их окраски
    /// Это минималистичная версия для быстрого тестирования
    /// </summary>
    [RequireComponent(typeof(ARPlaneManager))]
    [RequireComponent(typeof(ARRaycastManager))]
    public class WallDetectionAndPainting : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private Camera arCamera;
        
        [Header("ML Segmentation (Dulux mode)")]
        [Tooltip("ML Manager для pixel-perfect определения стен. Если включён - работает режим 'клик → вся стена'")]
        [SerializeField] private RemaluxAR.ML.MLSegmentationManager mlSegmentationManager;
        
        [Header("Hybrid Wall Detection (НОВОЕ!)")]
        [Tooltip("🆕 ГИБРИДНЫЙ детектор: Depth + Segmentation + AR Planes. Если включён - используется вместо обычных фильтров!")]
        [SerializeField] private RemaluxAR.ML.HybridWallDetector hybridWallDetector;
        
        [Tooltip("Использовать гибридный детектор (рекомендуется!)")]
        [SerializeField] private bool useHybridDetection = true;

        [Header("Wall Detection Settings")]
        [SerializeField] private bool showWallBorders = true;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material floorMaterial;
        
        [Header("Режим отладки")]
        [Tooltip("🔧 DEBUG: Показывать ВСЕ плоскости без фильтров (игнорирует все параметры ниже)")]
        [SerializeField] private bool debugShowAllPlanes = false;
        
        [Header("Фильтрация плоскостей - ЭКСТРЕМАЛЬНО МЯГКИЕ для DEBUG")]
        [Tooltip("🔥 DEBUG: 0.2 м² - ВИДИМ ВСЁ!")]
        [SerializeField] private float minWallArea = 0.2f;
        
        [Tooltip("🔥 DEBUG: 0.2 м - МИНИМУМ для обнаружения")]
        [SerializeField] private float minWallHeight = 0.2f;
        
        [Tooltip("🔥 DEBUG: 0.2 - Пропускаем почти всё")]
        [SerializeField] private float minAspectRatio = 0.2f;
        
        [Tooltip("Максимальное соотношение ширины к высоте (6.0 = очень широкие стены OK)")]
        [SerializeField] private float maxAspectRatio = 6.0f;
        
        [Tooltip("🔥 DEBUG: 0.1 м - почти на полу!")]
        [SerializeField] private float minCenterHeightY = 0.1f;

        [Header("Painting Settings")]
        [SerializeField] private Color paintColor = new Color(0.89f, 0.82f, 0.76f); // Бежевый как Dulux
        [SerializeField] private GameObject paintPrefab;
        [SerializeField] private float paintSize = 0.05f; // 5 см
        
        [Header("Painting Mode - КАК DULUX!")]
        [Tooltip("🎨 РЕЖИМ DULUX: Клик закрашивает ВСЮ СТЕНУ! (Рекомендуется!)")]
        [SerializeField] private bool fillWholeWallMode = true;
        
        [Tooltip("Wall Painting Manager для заливки всей стены")]
        [SerializeField] private WallPaintingManager wallPaintingManager;
        
        [Header("UI Подсказки")]
        [SerializeField] private bool showHints = true;
        [SerializeField] private float hintDuration = 5f; // Сколько секунд показывать подсказку
        
        [Header("Оптимизация производительности")]
        [SerializeField] private float planeUpdateThrottle = 0.1f; // Минимальный интервал между обновлениями плоскостей (сек)

        // Обнаруженные стены
        private Dictionary<TrackableId, ARPlane> detectedWalls = new Dictionary<TrackableId, ARPlane>();
        private List<GameObject> paintMarks = new List<GameObject>();
        private float lastPlaneUpdateTime = 0f; // Время последнего обновления плоскостей
        private Coroutine scanningHintsCoroutine; // Ссылка на корутину подсказок для остановки

        private void Awake()
        {
            // Auto-find компоненты
            if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
            if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
            if (arCamera == null) arCamera = Camera.main;
            
            // Auto-find WallPaintingManager
            if (wallPaintingManager == null)
            {
                wallPaintingManager = FindObjectOfType<WallPaintingManager>();
                
                if (wallPaintingManager == null && fillWholeWallMode)
                {
                    // Создаем автоматически если включен fillWholeWallMode
                    GameObject managerGO = new GameObject("WallPaintingManager");
                    wallPaintingManager = managerGO.AddComponent<WallPaintingManager>();
                    Debug.Log("[WallDetection] ✅ WallPaintingManager создан автоматически");
                }
            }

            // Создаём простой префаб для краски если не назначен (для точечного режима)
            if (paintPrefab == null)
            {
                paintPrefab = CreateDefaultPaintPrefab();
            }

            // Создаём материалы если не назначены
            if (wallMaterial == null)
            {
                wallMaterial = CreateDefaultMaterial(new Color(1f, 0f, 0f, 0.3f)); // Красный полупрозрачный
            }
            if (floorMaterial == null)
            {
                floorMaterial = CreateDefaultMaterial(new Color(0f, 1f, 0f, 0.3f)); // Зелёный полупрозрачный
            }
        }

        private void OnEnable()
        {
            if (planeManager != null)
            {
                planeManager.planesChanged += OnPlanesChanged;
                Debug.Log("[WallDetection] Подписались на события плоскостей");
            }
        }

        private void OnDisable()
        {
            // Отписываемся от событий
            if (planeManager != null)
            {
                planeManager.planesChanged -= OnPlanesChanged;
            }
            
            // Отключаем Enhanced Touch
            if (EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Disable();
            }
        }

        private void Start()
        {
            // Включаем Enhanced Touch для нового Input System
            EnhancedTouchSupport.Enable();
            
            Debug.Log("[WallDetection] Инициализация завершена. Начните сканирование помещения!");
            Debug.Log("[WallDetection] Кликайте/тапайте на стены для их окраски.");
            
            // Показываем текущие параметры фильтрации
            if (debugShowAllPlanes)
            {
                Debug.LogWarning("[WallDetection] 🔧 DEBUG MODE: Показываются ВСЕ плоскости без фильтров!");
            }
            else
            {
                Debug.Log($"[WallDetection] Параметры фильтрации:");
                Debug.Log($"  - minWallArea: {minWallArea} м²");
                Debug.Log($"  - minWallHeight: {minWallHeight} м");
                Debug.Log($"  - minAspectRatio: {minAspectRatio}");
                Debug.Log($"  - maxAspectRatio: {maxAspectRatio}");
                Debug.Log($"  - minCenterHeightY: {minCenterHeightY} м");
            }
            
            // ML режим
            if (fillWholeWallMode)
            {
                if (mlSegmentationManager != null)
                {
                    Debug.Log("[WallDetection] 🎨 Режим 'Вся стена' (Dulux) активен с ML!");
                }
                else
                {
                    Debug.LogWarning("[WallDetection] ⚠️ Режим 'Вся стена' включён, но MLSegmentationManager не назначен!");
                }
            }
            
            // Показываем подсказки для быстрого сканирования
            if (showHints)
            {
                scanningHintsCoroutine = StartCoroutine(ShowScanningHints());
            }
        }
        
        /// <summary>
        /// Показывает подсказки для ускорения сканирования
        /// </summary>
        private System.Collections.IEnumerator ShowScanningHints()
        {
            yield return new WaitForSeconds(1f);
            
            Debug.Log("💡 [ПОДСКАЗКА] Медленно двигайте телефон из стороны в сторону для сканирования");
            yield return new WaitForSeconds(hintDuration);
            
            Debug.Log("💡 [ПОДСКАЗКА] Направляйте камеру на стены под разными углами");
            yield return new WaitForSeconds(hintDuration);
            
            Debug.Log("💡 [ПОДСКАЗКА] Хорошее освещение ускоряет обнаружение поверхностей");
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// Обработка изменений плоскостей (КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ)
        /// </summary>
        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            // Throttling: ограничиваем частоту обновлений для снижения нагрузки на CPU
            float currentTime = Time.time;
            if (currentTime - lastPlaneUpdateTime < planeUpdateThrottle)
            {
                return; // Пропускаем обновление, если прошло недостаточно времени
            }
            lastPlaneUpdateTime = currentTime;
            
            // ✅ ОБРАБОТКА НОВЫХ ПЛОСКОСТЕЙ (args.added)
            foreach (var plane in args.added)
            {
                ProcessAddedPlane(plane);
            }

            // ✅ КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: ОБРАБОТКА ОБНОВЛЕННЫХ ПЛОСКОСТЕЙ (args.updated)
            // Это исправляет баг, где плоскость уменьшалась с 1.6м² до 0.51м² но оставалась как "стена"
            foreach (var plane in args.updated)
            {
                ProcessUpdatedPlane(plane);
            }

            // ✅ ОБРАБОТКА УДАЛЕННЫХ ПЛОСКОСТЕЙ (args.removed)
            foreach (var plane in args.removed)
            {
                ProcessRemovedPlane(plane);
            }

            // Выводим статистику
            UpdateDebugStats();
        }

        /// <summary>
        /// Обрабатывает новую плоскость
        /// </summary>
        private void ProcessAddedPlane(ARPlane plane)
        {
            if (ShouldProcessPlane(plane))
            {
                if (!detectedWalls.ContainsKey(plane.trackableId))
                {
                    detectedWalls.Add(plane.trackableId, plane);
                    ApplyWallVisualization(plane);
                    Debug.Log($"[WallDetection] ✅ НОВАЯ СТЕНА обнаружена! ID: {plane.trackableId}, размер: {plane.size}");
                    
                    // Останавливаем подсказки после обнаружения первой стены
                    if (detectedWalls.Count == 1 && scanningHintsCoroutine != null)
                    {
                        StopCoroutine(scanningHintsCoroutine);
                        scanningHintsCoroutine = null;
                        Debug.Log("[WallDetection] Подсказки остановлены - первая стена обнаружена!");
                    }
                }
            }
        }

        /// <summary>
        /// Обрабатывает обновление существующей плоскости (КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ БАГА)
        /// </summary>
        private void ProcessUpdatedPlane(ARPlane plane)
        {
            bool isCurrentlyWall = detectedWalls.ContainsKey(plane.trackableId);
            bool shouldBeWall = ShouldProcessPlane(plane);

            if (shouldBeWall)
            {
                if (!isCurrentlyWall)
                {
                    // Плоскость "выросла" и ТЕПЕРЬ стала валидной стеной
                    detectedWalls.Add(plane.trackableId, plane);
                    ApplyWallVisualization(plane);
                    Debug.Log($"[WallDetection] ✅ СТЕНА ОБНОВИЛАСЬ (стала валидной)! ID: {plane.trackableId}, размер: {plane.size}");
                }
                else
                {
                    // Плоскость все еще является стеной, просто обновились границы
                    UpdateWallVisualization(plane);
                }
            }
            else
            {
                if (isCurrentlyWall)
                {
                    // ⚠️ ЭТО ИСПРАВЛЕНИЕ БАГА 0.51м²!
                    // Плоскость "уменьшилась" и БОЛЬШЕ не соответствует критериям стены
                    detectedWalls.Remove(plane.trackableId);
                    plane.gameObject.SetActive(false);
                    Debug.LogWarning($"[WallDetection] ❌ СТЕНА УДАЛЕНА (стала невалидной)! ID: {plane.trackableId}, размер: {plane.size}");
                }
            }
        }

        /// <summary>
        /// Обрабатывает удаление плоскости ARKit
        /// </summary>
        private void ProcessRemovedPlane(ARPlane plane)
        {
            if (detectedWalls.ContainsKey(plane.trackableId))
            {
                detectedWalls.Remove(plane.trackableId);
                Debug.Log($"[WallDetection] ❌ СТЕНА УДАЛЕНА (ARKit)! ID: {plane.trackableId}");
            }
        }

        /// <summary>
        /// Выводит статистику обнаружения
        /// </summary>
        private void UpdateDebugStats()
        {
            int wallCount = detectedWalls.Count;
            int totalPlanes = 0;
            foreach (var plane in planeManager.trackables)
            {
                totalPlanes++;
            }
            Debug.Log($"[WallDetection] Обнаружено плоскостей: {totalPlanes}, из них стен: {wallCount}");
        }

        /// <summary>
        /// Проверяет, подходит ли плоскость по критериям фильтрации (УЛУЧШЕННАЯ ВЕРСИЯ)
        /// </summary>
        private bool ShouldProcessPlane(ARPlane plane)
        {
            Vector3 planePosition = plane.transform.position;
            Vector2 planeSize = plane.size;
            float planeArea = planeSize.x * planeSize.y;
            float aspectRatio = planeSize.x / planeSize.y;
            float centerHeight = planePosition.y;
            
            // Префикс для логов с размерами плоскости
            string planeInfo = $"[{planeSize.x:F2}м × {planeSize.y:F2}м = {planeArea:F2}м², aspect: {aspectRatio:F2}]";
            
            // 🔧 DEBUG MODE: Показать ВСЕ вертикальные плоскости без фильтров
            if (debugShowAllPlanes)
            {
                if (plane.alignment == PlaneAlignment.Vertical)
                {
                    Debug.LogWarning($"[WallDetection] 🔧 DEBUG: Показываю плоскость БЕЗ ФИЛЬТРОВ: {planeInfo}");
                    return true;
                }
                return false;
            }
            
            // ✅ ФИЛЬТР 1: ТОЛЬКО ВЕРТИКАЛЬНЫЕ ПЛОСКОСТИ (СТЕНЫ)
            if (plane.alignment != PlaneAlignment.Vertical)
            {
                return false; // Молча пропускаем невертикальные плоскости (их много)
            }
            
            // ✅ ФИЛЬТР 2: МИНИМАЛЬНАЯ ПЛОЩАДЬ
            if (planeArea < minWallArea)
            {
                // Более частое логирование только для очень маленьких плоскостей
                if (planeArea < 0.2f || planeArea > minWallArea * 0.8f)
                {
                    Debug.Log($"[WallDetection] ⏳ Ожидаем роста плоскости: {planeInfo} < {minWallArea:F1}м²");
                }
                return false;
            }
            
            // ✅ ФИЛЬТР 3: МИНИМАЛЬНАЯ ВЫСОТА
            if (planeSize.y < minWallHeight)
            {
                Debug.LogWarning($"[WallDetection] ❌ Игнор: {planeInfo} Высота {planeSize.y:F2}м < {minWallHeight}м");
                return false;
            }
            
            // ✅ ФИЛЬТР 4: ASPECT RATIO (КРИТИЧЕСКИЙ!)
            if (aspectRatio < minAspectRatio)
            {
                Debug.LogWarning($"[WallDetection] ❌ Игнор ДВЕРЬ/КОСЯК: {planeInfo} aspect < {minAspectRatio} (слишком узкая)");
                return false;
            }
            
            if (aspectRatio > maxAspectRatio)
            {
                Debug.LogWarning($"[WallDetection] ❌ Игнор: {planeInfo} aspect > {maxAspectRatio} (странная геометрия)");
                return false;
            }
            
            // ✅ ФИЛЬТР 5: ВЫСОТА ЦЕНТРА ПЛОСКОСТИ
            if (centerHeight < minCenterHeightY)
            {
                Debug.LogWarning($"[WallDetection] ❌ Игнор МЕБЕЛЬ: {planeInfo} centerY: {centerHeight:F2}м < {minCenterHeightY}м");
                return false;
            }
            
            // ✅ ВСЕ ФИЛЬТРЫ ПРОЙДЕНЫ - ЭТО СТЕНА!
            Debug.Log($"[WallDetection] ✅ ВАЛИДНАЯ СТЕНА: {planeInfo}");
            return true;
        }

        /// <summary>
        /// Применяет визуализацию к стене
        /// </summary>
        private void ApplyWallVisualization(ARPlane plane)
        {
            var meshRenderer = plane.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = wallMaterial;
                meshRenderer.enabled = true;
                
                // 🔥 DEBUG: Логируем состояние mesh
                var meshFilter = plane.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.mesh != null)
                {
                    Debug.Log($"[WallDetection] Mesh: vertices={meshFilter.mesh.vertexCount}, triangles={meshFilter.mesh.triangles.Length/3}");
                }
                else
                {
                    Debug.LogWarning($"[WallDetection] ⚠️ Mesh отсутствует! Будут только точки boundary.");
                }
            }
            
            plane.gameObject.SetActive(true);
            
            // Визуализируем границы стены (ВСЕГДА для DEBUG)
            VisualizeWallBorders(plane);
        }

        /// <summary>
        /// Обновляет визуализацию существующей стены
        /// </summary>
        private void UpdateWallVisualization(ARPlane plane)
        {
            // Обновляем границы стены (они могли измениться)
            if (showWallBorders)
            {
                VisualizeWallBorders(plane);
            }
        }

        /// <summary>
        /// Визуализирует границы стены
        /// </summary>
        private void VisualizeWallBorders(ARPlane plane)
        {
            // Получаем границу плоскости
            if (plane.boundary.Length > 0)
            {
                var lineRenderer = plane.gameObject.GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = plane.gameObject.AddComponent<LineRenderer>();
                    lineRenderer.startWidth = 0.02f;
                    lineRenderer.endWidth = 0.02f;
                    lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    lineRenderer.startColor = Color.yellow;
                    lineRenderer.endColor = Color.yellow;
                    lineRenderer.useWorldSpace = false;
                    lineRenderer.loop = true;
                }

                // Устанавливаем точки границы
                var boundary = plane.boundary;
                lineRenderer.positionCount = boundary.Length;
                for (int i = 0; i < boundary.Length; i++)
                {
                    lineRenderer.SetPosition(i, boundary[i]);
                }

                Debug.Log($"[WallDetection] Границы стены визуализированы ({boundary.Length} точек)");
            }
        }

        /// <summary>
        /// Обработка пользовательского ввода для окраски
        /// </summary>
        private void HandleInput()
        {
            // Новый Input System - Enhanced Touch
            if (Touch.activeTouches.Count > 0)
            {
                Touch touch = Touch.activeTouches[0];
                
                // Проверяем начало тача
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    TryPaintAtPosition(touch.screenPosition);
                }
            }
            // Обработка клика мыши (для тестирования в редакторе с симуляцией)
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                TryPaintAtPosition(mousePos);
            }
        }

        /// <summary>
        /// Пытается окрасить поверхность в указанной позиции экрана
        /// Поддерживает 3 режима: 
        /// 1. Гибридный детектор (Depth + Segmentation + AR)
        /// 2. "Вся стена" через ML Segmentation
        /// 3. Точечное рисование (базовое)
        /// </summary>
        private void TryPaintAtPosition(Vector2 screenPosition)
        {
            // 🆕 РЕЖИМ 1: ГИБРИДНЫЙ ДЕТЕКТОР (приоритетный!)
            if (useHybridDetection && hybridWallDetector != null)
            {
                TryPaintWithHybridDetector(screenPosition);
                return;
            }
            
            // РЕЖИМ 2: "Вся стена" через ML Segmentation (как Dulux Visualizer)
            if (fillWholeWallMode && mlSegmentationManager != null)
            {
                TryPaintWholeWall(screenPosition);
                return;
            }
            
            // РЕЖИМ 3: Точечное рисование (оригинальный режим)
            // Выполняем raycast
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            
            // Проверяем попадание только по вертикальным плоскостям (стенам)
            if (raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var hit in hits)
                {
                    var plane = planeManager.GetPlane(hit.trackableId);
                    
                    // Окрашиваем только стены
                    if (plane != null && plane.alignment == PlaneAlignment.Vertical)
                    {
                        PaintWall(hit.pose.position, hit.pose.rotation, plane);
                        Debug.Log($"[WallDetection] ✓ Стена окрашена! Позиция: {hit.pose.position}");
                        return;
                    }
                }
            }
            
            Debug.Log("[WallDetection] Клик не попал в стену. Наведите на красную поверхность (стену).");
        }
        
        /// <summary>
        /// 🆕 ГИБРИДНЫЙ РЕЖИМ - использует Depth + Segmentation + AR Planes
        /// </summary>
        private void TryPaintWithHybridDetector(Vector2 screenPosition)
        {
            // Проверяем через гибридный детектор
            if (hybridWallDetector.IsWallAtScreenPosition(screenPosition, out var wallInfo))
            {
                Debug.Log($"[WallDetection] ✅ ГИБРИДНЫЙ: Стена обнаружена! Confidence: {wallInfo.confidence:F2}, " +
                         $"Depth: {wallInfo.averageDepth:F2}, Size: {wallInfo.size}");
                
                // Выполняем raycast для точного позиционирования
                List<ARRaycastHit> hits = new List<ARRaycastHit>();
                
                if (raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
                {
                    foreach (var hit in hits)
                    {
                        var plane = planeManager.GetPlane(hit.trackableId);
                        
                        if (plane != null && plane.trackableId == wallInfo.arPlane.trackableId)
                        {
                            PaintWall(hit.pose.position, hit.pose.rotation, plane);
                            
                            // Показываем расширенную информацию
                            Debug.Log($"[WallDetection] 🎨 Гибридная окраска:\n" +
                                     $"  • Depth consistency: {wallInfo.depthConsistency:F2}\n" +
                                     $"  • Non-wall objects: {(wallInfo.hasNonWallObjects ? "Да (people/furniture)" : "Нет")}\n" +
                                     $"  • Confidence: {wallInfo.confidence:F2}");
                            
                            return;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("[WallDetection] ❌ ГИБРИДНЫЙ: Клик НЕ на стене! " +
                                "(Depth, Segmentation или AR Planes не подтвердили стену)");
            }
        }

        /// <summary>
        /// Режим "Вся стена" - использует ML для выделения всей стены от точки клика
        /// </summary>
        private void TryPaintWholeWall(Vector2 screenPosition)
        {
            // Нормализуем screen position (0-1)
            Vector2 normalizedPos = new Vector2(
                screenPosition.x / Screen.width,
                screenPosition.y / Screen.height
            );
            
            // Проверяем через ML, что клик на стене
            if (!mlSegmentationManager.IsWall(normalizedPos))
            {
                int pixelClass = mlSegmentationManager.GetPixelClass(normalizedPos);
                Debug.LogWarning($"[WallDetection] ❌ Клик НЕ на стене! ML класс: {pixelClass} (expected 0=wall)");
                return;
            }
            
            Debug.Log("[WallDetection] ✅ Клик на стене обнаружен через ML! Запуск Flood Fill...");
            
            // Flood Fill - получить все пиксели той же стены
            HashSet<Vector2Int> wallPixels = mlSegmentationManager.FloodFillWall(normalizedPos);
            
            if (wallPixels == null || wallPixels.Count == 0)
            {
                Debug.LogWarning("[WallDetection] Flood Fill не вернул пиксели стены!");
                return;
            }
            
            Debug.Log($"[WallDetection] 🎨 Применяем краску к {wallPixels.Count} пикселям стены...");
            
            // TODO: Применить текстуру краски ко всей выделенной стене
            // Для этого нужно:
            // 1. Создать 3D mesh из 2D pixel mask
            // 2. Спроецировать на AR plane
            // 3. Применить материал с цветом краски
            
            // ВРЕМЕННО: Визуализируем центр стены
            Vector2 centerScreen = new Vector2(
                normalizedPos.x * Screen.width,
                normalizedPos.y * Screen.height
            );
            
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            if (raycastManager.Raycast(centerScreen, hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var hit in hits)
                {
                    var plane = planeManager.GetPlane(hit.trackableId);
                    if (plane != null && plane.alignment == PlaneAlignment.Vertical)
                    {
                        // Создаем метку краски в центре обнаруженной стены
                        PaintWall(hit.pose.position, hit.pose.rotation, plane);
                        Debug.Log($"[WallDetection] ✅ ВСЯ СТЕНА выделена! ({wallPixels.Count} пикселей)");
                        
                        // TODO: Здесь должна быть полная заливка стены, а не одна точка
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Окрашивает стену в указанной позиции
        /// РЕЖИМ DULUX: Закрашивает ВСЮ СТЕНУ одним кликом!
        /// </summary>
        private void PaintWall(Vector3 position, Quaternion rotation, ARPlane plane)
        {
            if (fillWholeWallMode && wallPaintingManager != null)
            {
                // 🎨 РЕЖИМ DULUX: Закрашиваем ВСЮ СТЕНУ!
                wallPaintingManager.PaintWall(plane, paintColor);
                
                Debug.Log($"[WallDetection] 🎨 ВСЯ СТЕНА окрашена! Plane: {plane.trackableId}, размер: {plane.size}, цвет: {paintColor}");
            }
            else
            {
                // Старый режим: точечная покраска (маленькая сфера)
                GameObject paintMark = Instantiate(paintPrefab, position, rotation);
                paintMark.SetActive(true); // ✅ АКТИВИРУЕМ!
                paintMark.transform.localScale = Vector3.one * paintSize;
                
                // Применяем цвет
                var renderer = paintMark.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = paintColor;
                }

                // Сдвигаем немного вперёд от стены, чтобы была видна
                paintMark.transform.position += paintMark.transform.forward * 0.01f;

                paintMarks.Add(paintMark);
                
                // Визуальная и тактильная обратная связь
                StartCoroutine(AnimatePaintMark(paintMark));
                
                Debug.Log($"[WallDetection] ✅ Создана метка краски #{paintMarks.Count} на {position}");
            }
            
            // Haptic feedback (вибрация) - в обоих режимах
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif
        }

        /// <summary>
        /// Очищает всю краску (и со стен, и точечные метки)
        /// </summary>
        [ContextMenu("Очистить всю краску")]
        public void ClearAllPaint()
        {
            // Очищаем заливки стен (Dulux режим)
            if (wallPaintingManager != null)
            {
                wallPaintingManager.UnpaintAllWalls();
            }
            
            // Очищаем точечные метки (старый режим)
            foreach (var mark in paintMarks)
            {
                if (mark != null)
                {
                    Destroy(mark);
                }
            }
            paintMarks.Clear();
            
            Debug.Log("[WallDetection] ✅ Вся краска очищена!");
        }

        /// <summary>
        /// Меняет цвет краски (для будущих окрасок)
        /// </summary>
        public void SetPaintColor(Color color)
        {
            paintColor = color;
            
            // Обновляем цвет в WallPaintingManager
            if (wallPaintingManager != null)
            {
                wallPaintingManager.SetPaintColor(color);
            }
            
            Debug.Log($"[WallDetection] 🎨 Цвет краски изменён на {color}");
        }

        /// <summary>
        /// Создаёт простой префаб для краски
        /// </summary>
        private GameObject CreateDefaultPaintPrefab()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            prefab.name = "PaintMark";
            
            // Удаляем коллайдер (не нужен)
            var collider = prefab.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Создаём материал
            var renderer = prefab.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = paintColor;
            }

            prefab.SetActive(false);
            return prefab;
        }

        /// <summary>
        /// Создаёт простой полупрозрачный материал
        /// </summary>
        private Material CreateDefaultMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.color = color;
            return mat;
        }

        /// <summary>
        /// Получает количество обнаруженных стен
        /// </summary>
        public int GetWallCount()
        {
            return detectedWalls.Count;
        }

        /// <summary>
        /// Получает информацию о состоянии
        /// </summary>
        public string GetStatusInfo()
        {
            return $"Стен: {detectedWalls.Count}\nМеток краски: {paintMarks.Count}\nЦвет: {paintColor}";
        }

        /// <summary>
        /// Анимация появления метки краски
        /// </summary>
        private System.Collections.IEnumerator AnimatePaintMark(GameObject paintMark)
        {
            if (paintMark == null) yield break;
            
            Vector3 originalScale = paintMark.transform.localScale;
            Vector3 startScale = originalScale * 0.1f;
            
            // Начинаем с маленького размера
            paintMark.transform.localScale = startScale;
            
            // Плавно увеличиваем до нормального размера за 0.2 секунды
            float duration = 0.2f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease-out cubic для плавности
                t = 1f - Mathf.Pow(1f - t, 3f);
                
                if (paintMark != null)
                {
                    paintMark.transform.localScale = Vector3.Lerp(startScale, originalScale, t);
                }
                
                yield return null;
            }
            
            // Устанавливаем финальный размер
            if (paintMark != null)
            {
                paintMark.transform.localScale = originalScale;
            }
        }

        private void OnDestroy()
        {
            ClearAllPaint();
        }
    }
}

