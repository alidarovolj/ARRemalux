using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using RemaluxAR.Data;
using RemaluxAR.AR;
using RemaluxAR.Utils;

namespace RemaluxAR.Drawing
{
    /// <summary>
    /// Управляет рисованием в AR - создание и управление мазками краски
    /// </summary>
    public class DrawingManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARManager arManager;
        [SerializeField] private GameObject paintDotPrefab; // Префаб точки краски (маленькая сфера/квад)

        [Header("Drawing Settings")]
        [SerializeField] private Color currentColor = Color.red;
        [SerializeField] private float brushThickness = 0.02f; // 2 см
        [SerializeField] private float minDistanceBetweenPoints = 0.01f; // 1 см
        [SerializeField] private int maxStrokes = 1000;

        [Header("Input Settings")]
        [SerializeField] private float drawingThrottle = 0.05f; // Минимальный интервал между точками
        [SerializeField] private LayerMask raycastMask = ~0; // Все слои по умолчанию

        // Состояние
        private List<PaintStroke> allStrokes = new List<PaintStroke>();
        private PaintStroke currentStroke;
        private bool isDrawing = false;
        private float lastDrawTime;
        private Vector3 lastDrawnPoint;
        private ObjectPool<Transform> paintDotPool;

        // Events
        public event System.Action<PaintStroke> OnStrokeStarted;
        public event System.Action<PaintStroke> OnStrokeEnded;
        public event System.Action OnAllStrokesCleared;

        // Properties
        public Color CurrentColor
        {
            get => currentColor;
            set => currentColor = value;
        }

        public float BrushThickness
        {
            get => brushThickness;
            set => brushThickness = Mathf.Max(0.001f, value);
        }

        public int StrokeCount => allStrokes.Count;
        public bool IsDrawing => isDrawing;
        public IReadOnlyList<PaintStroke> AllStrokes => allStrokes;

        private void Awake()
        {
            if (arManager == null)
            {
                arManager = FindObjectOfType<ARManager>();
            }

            // Инициализируем пул объектов
            if (paintDotPrefab != null)
            {
                Transform poolParent = new GameObject("PaintDotsPool").transform;
                poolParent.SetParent(transform);
                paintDotPool = new ObjectPool<Transform>(
                    paintDotPrefab.transform,
                    poolParent,
                    initialSize: 50,
                    maxSize: 5000
                );
            }
            else
            {
                Debug.LogError("[DrawingManager] Paint dot prefab is not assigned!");
            }
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// Обрабатывает пользовательский ввод для рисования
        /// </summary>
        private void HandleInput()
        {
            if (!arManager.IsSessionReady) return;

            // Проверяем касание/клик
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        StartDrawing(touch.position);
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        ContinueDrawing(touch.position);
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EndDrawing();
                        break;
                }
            }
            // Для тестирования в редакторе - используем мышь
            else if (Input.GetMouseButton(0) && Application.isEditor)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    StartDrawing(Input.mousePosition);
                }
                else
                {
                    ContinueDrawing(Input.mousePosition);
                }
            }
            else if (Input.GetMouseButtonUp(0) && Application.isEditor)
            {
                EndDrawing();
            }
        }

        /// <summary>
        /// Начинает новый штрих
        /// </summary>
        private void StartDrawing(Vector2 screenPosition)
        {
            if (isDrawing) return;

            // Проверяем лимит штрихов
            if (allStrokes.Count >= maxStrokes)
            {
                Debug.LogWarning($"[DrawingManager] Max strokes limit reached ({maxStrokes}), removing oldest");
                RemoveOldestStroke();
            }

            currentStroke = new PaintStroke(currentColor, brushThickness);
            isDrawing = true;
            lastDrawTime = 0;

            // Сразу добавляем первую точку
            AddPointAtScreenPosition(screenPosition);

            OnStrokeStarted?.Invoke(currentStroke);
            Debug.Log("[DrawingManager] Started new stroke");
        }

        /// <summary>
        /// Продолжает текущий штрих
        /// </summary>
        private void ContinueDrawing(Vector2 screenPosition)
        {
            if (!isDrawing || currentStroke == null) return;

            // Throttling - не добавляем точки слишком часто
            if (Time.time - lastDrawTime < drawingThrottle)
                return;

            AddPointAtScreenPosition(screenPosition);
        }

        /// <summary>
        /// Заканчивает текущий штрих
        /// </summary>
        private void EndDrawing()
        {
            if (!isDrawing || currentStroke == null) return;

            isDrawing = false;

            // Сохраняем штрих если в нём есть точки
            if (currentStroke.PointCount > 0)
            {
                allStrokes.Add(currentStroke);
                OnStrokeEnded?.Invoke(currentStroke);
                Debug.Log($"[DrawingManager] Stroke ended with {currentStroke.PointCount} points");
            }
            else
            {
                // Если точек нет, очищаем
                currentStroke.ClearRenderers();
            }

            currentStroke = null;
        }

        /// <summary>
        /// Добавляет точку краски в позиции экранного касания
        /// </summary>
        private void AddPointAtScreenPosition(Vector2 screenPosition)
        {
            // Выполняем raycast в AR сцену
            ARRaycastHit hit;
            TrackableType trackableTypes = TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint;

            // На iOS с mesh можно также raycast по mesh
            if (PlatformAdapter.SupportsMeshScanning)
            {
                trackableTypes |= TrackableType.Depth;
            }

            if (arManager.TryRaycast(screenPosition, out hit, trackableTypes))
            {
                Vector3 hitPosition = hit.pose.position;
                Vector3 hitNormal = hit.pose.up; // Нормаль поверхности

                // Проверяем минимальное расстояние от предыдущей точки
                if (currentStroke.PointCount > 0)
                {
                    float distance = Vector3.Distance(lastDrawnPoint, hitPosition);
                    if (distance < minDistanceBetweenPoints)
                        return;
                }

                // Добавляем точку к штриху
                currentStroke.AddPoint(hitPosition);
                lastDrawnPoint = hitPosition;
                lastDrawTime = Time.time;

                // Создаём визуальный объект
                CreatePaintDot(hitPosition, hitNormal);
            }
        }

        /// <summary>
        /// Создаёт визуальную точку краски
        /// </summary>
        private void CreatePaintDot(Vector3 position, Vector3 normal)
        {
            if (paintDotPool == null || currentStroke == null) return;

            Transform dot = paintDotPool.Get();
            if (dot == null)
            {
                Debug.LogWarning("[DrawingManager] Failed to get paint dot from pool");
                return;
            }

            // Позиционируем точку чуть выше поверхности
            dot.position = position + normal * 0.001f;

            // Ориентируем точку по нормали поверхности
            dot.rotation = Quaternion.LookRotation(normal);

            // Масштабируем по толщине кисти
            dot.localScale = Vector3.one * brushThickness;

            // Применяем цвет
            Renderer renderer = dot.GetComponent<Renderer>();
            if (renderer != null)
            {
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetColor("_BaseColor", currentColor);
                props.SetColor("_Color", currentColor); // Для совместимости с разными шейдерами
                renderer.SetPropertyBlock(props);
            }

            // Добавляем к текущему штриху
            currentStroke.AddRenderer(dot.gameObject);
        }

        /// <summary>
        /// Удаляет самый старый штрих
        /// </summary>
        private void RemoveOldestStroke()
        {
            if (allStrokes.Count == 0) return;

            PaintStroke oldest = allStrokes[0];
            allStrokes.RemoveAt(0);

            // Возвращаем объекты в пул
            foreach (var renderer in oldest.Renderers)
            {
                if (renderer != null)
                {
                    Transform dotTransform = renderer.transform;
                    paintDotPool.Return(dotTransform);
                }
            }
        }

        /// <summary>
        /// Очищает все штрихи
        /// </summary>
        public void ClearAllStrokes()
        {
            // Возвращаем все объекты в пул
            foreach (var stroke in allStrokes)
            {
                foreach (var renderer in stroke.Renderers)
                {
                    if (renderer != null)
                    {
                        Transform dotTransform = renderer.transform;
                        paintDotPool.Return(dotTransform);
                    }
                }
            }

            // Очищаем текущий штрих если рисуем
            if (currentStroke != null)
            {
                foreach (var renderer in currentStroke.Renderers)
                {
                    if (renderer != null)
                    {
                        Transform dotTransform = renderer.transform;
                        paintDotPool.Return(dotTransform);
                    }
                }
                currentStroke = null;
                isDrawing = false;
            }

            allStrokes.Clear();
            OnAllStrokesCleared?.Invoke();

            Debug.Log("[DrawingManager] All strokes cleared");
        }

        /// <summary>
        /// Устанавливает видимость всех штрихов
        /// </summary>
        public void SetStrokesVisible(bool visible)
        {
            foreach (var stroke in allStrokes)
            {
                foreach (var renderer in stroke.Renderers)
                {
                    if (renderer != null)
                    {
                        renderer.SetActive(visible);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Очищаем пул при уничтожении
            paintDotPool?.DestroyAll();
        }
    }
}

