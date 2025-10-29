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

        [Header("Wall Detection Settings")]
        [SerializeField] private bool showWallBorders = true;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material floorMaterial;
        
        [Header("Фильтрация плоскостей")]
        [SerializeField] private float minWallArea = 0.5f; // Минимальная площадь стены в м²
        [SerializeField] private float minWallHeight = 0.8f; // Минимальная высота стены в метрах
        [SerializeField] private float maxFurnitureHeight = 1.2f; // Высота, выше которой игнорируем горизонтальные плоскости

        [Header("Painting Settings")]
        [SerializeField] private Color paintColor = Color.red;
        [SerializeField] private GameObject paintPrefab;
        [SerializeField] private float paintSize = 0.05f; // 5 см

        // Обнаруженные стены
        private Dictionary<TrackableId, ARPlane> detectedWalls = new Dictionary<TrackableId, ARPlane>();
        private List<GameObject> paintMarks = new List<GameObject>();
        private float floorLevel = float.MinValue; // Уровень пола для фильтрации

        private void Awake()
        {
            // Auto-find компоненты
            if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
            if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
            if (arCamera == null) arCamera = Camera.main;

            // Создаём простой префаб для краски если не назначен
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

        new private void OnDisable()
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
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// Обработка изменений плоскостей
        /// </summary>
        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            // Обработка новых плоскостей
            foreach (var plane in args.added)
            {
                ProcessPlane(plane, isNew: true);
            }

            // Обработка обновлённых плоскостей
            foreach (var plane in args.updated)
            {
                ProcessPlane(plane, isNew: false);
            }

            // Обработка удалённых плоскостей
            foreach (var plane in args.removed)
            {
                if (detectedWalls.ContainsKey(plane.trackableId))
                {
                    detectedWalls.Remove(plane.trackableId);
                    Debug.Log($"[WallDetection] Стена удалена: {plane.trackableId}");
                }
            }

            // Выводим статистику
            int wallCount = detectedWalls.Count;
            int totalPlanes = 0;
            foreach (var plane in planeManager.trackables)
            {
                totalPlanes++;
            }
            Debug.Log($"[WallDetection] Обнаружено плоскостей: {totalPlanes}, из них стен: {wallCount}");
        }

        /// <summary>
        /// Обрабатывает одну плоскость
        /// </summary>
        /// <summary>
        /// Проверяет, подходит ли плоскость по критериям фильтрации
        /// </summary>
        private bool ShouldProcessPlane(ARPlane plane)
        {
            Vector3 planePosition = plane.transform.position;
            Vector2 planeSize = plane.size;
            float planeArea = planeSize.x * planeSize.y;
            
            // Обновляем уровень пола
            if (plane.alignment == PlaneAlignment.HorizontalUp)
            {
                if (floorLevel == float.MinValue || planePosition.y < floorLevel)
                {
                    floorLevel = planePosition.y;
                }
            }
            
            // Фильтруем вертикальные плоскости (стены)
            if (plane.alignment == PlaneAlignment.Vertical)
            {
                // Проверяем минимальную площадь
                if (planeArea < minWallArea)
                {
                    return false;
                }
                
                // Проверяем минимальную высоту
                if (planeSize.y < minWallHeight)
                {
                    return false;
                }
                
                return true;
            }
            
            // Фильтруем горизонтальные плоскости (пол, мебель)
            if (plane.alignment == PlaneAlignment.HorizontalUp || plane.alignment == PlaneAlignment.HorizontalDown)
            {
                // Игнорируем маленькие плоскости
                if (planeArea < minWallArea * 0.5f)
                {
                    return false;
                }
                
                // Игнорируем плоскости выше уровня мебели (диваны, столы)
                if (floorLevel != float.MinValue)
                {
                    float heightAboveFloor = planePosition.y - floorLevel;
                    
                    // Игнорируем если это явно мебель (между полом и maxFurnitureHeight)
                    if (heightAboveFloor > 0.1f && heightAboveFloor < maxFurnitureHeight)
                    {
                        Debug.Log($"[WallDetection] Игнорируем плоскость на высоте {heightAboveFloor:F2}м - похоже на мебель");
                        return false;
                    }
                }
                
                return true;
            }
            
            return true; // Остальные плоскости обрабатываем
        }

        private void ProcessPlane(ARPlane plane, bool isNew)
        {
            // Фильтруем плоскость
            if (!ShouldProcessPlane(plane))
            {
                // Скрываем отфильтрованные плоскости
                plane.gameObject.SetActive(false);
                return;
            }
            
            // Определяем тип плоскости
            bool isWall = plane.alignment == PlaneAlignment.Vertical;
            bool isFloor = plane.alignment == PlaneAlignment.HorizontalUp;
            bool isCeiling = plane.alignment == PlaneAlignment.HorizontalDown;

            // Применяем материал в зависимости от типа
            var meshRenderer = plane.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                if (isWall)
                {
                    meshRenderer.material = wallMaterial;
                    if (isNew)
                    {
                        Debug.Log($"[WallDetection] ✓ СТЕНА обнаружена! ID: {plane.trackableId}, размер: {plane.size}");
                        detectedWalls[plane.trackableId] = plane;
                        
                        // Визуализируем границы стены
                        if (showWallBorders)
                        {
                            VisualizeWallBorders(plane);
                        }
                    }
                }
                else if (isFloor)
                {
                    meshRenderer.material = floorMaterial;
                    if (isNew)
                    {
                        Debug.Log($"[WallDetection] ✓ ПОЛ обнаружен! ID: {plane.trackableId}, размер: {plane.size}");
                    }
                }
                else if (isCeiling)
                {
                    meshRenderer.material = CreateDefaultMaterial(new Color(0.5f, 0.5f, 1f, 0.3f)); // Голубой
                    if (isNew)
                    {
                        Debug.Log($"[WallDetection] ✓ ПОТОЛОК обнаружен! ID: {plane.trackableId}");
                    }
                }

                // Делаем плоскости видимыми
                meshRenderer.enabled = true;
            }

            // Делаем плоскость активной
            plane.gameObject.SetActive(true);
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
        /// </summary>
        private void TryPaintAtPosition(Vector2 screenPosition)
        {
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
        /// Окрашивает стену в указанной позиции
        /// </summary>
        private void PaintWall(Vector3 position, Quaternion rotation, ARPlane plane)
        {
            // Создаём метку краски
            GameObject paintMark = Instantiate(paintPrefab, position, rotation);
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
            
            // Haptic feedback (вибрация)
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif

            Debug.Log($"[WallDetection] Создана метка краски #{paintMarks.Count}. Всего меток: {paintMarks.Count}");
        }

        /// <summary>
        /// Очищает все метки краски
        /// </summary>
        [ContextMenu("Очистить всю краску")]
        public void ClearAllPaint()
        {
            foreach (var mark in paintMarks)
            {
                if (mark != null)
                {
                    Destroy(mark);
                }
            }
            paintMarks.Clear();
            Debug.Log("[WallDetection] Вся краска очищена!");
        }

        /// <summary>
        /// Меняет цвет краски
        /// </summary>
        public void SetPaintColor(Color color)
        {
            paintColor = color;
            Debug.Log($"[WallDetection] Цвет краски изменён на {color}");
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

