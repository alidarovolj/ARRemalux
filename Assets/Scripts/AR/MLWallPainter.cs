using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using RemaluxAR.ML;

namespace RemaluxAR.AR
{
    /// <summary>
    /// ML-FIRST подход к покраске стен как в Dulux Visualizer!
    /// 
    /// АЛГОРИТМ:
    /// 1. Клик → проверка ML маски "это стена?"
    /// 2. FloodFill → находим все пиксели стены
    /// 3. Контур → находим границы стены
    /// 4. Raycast → получаем 3D позицию и нормаль стены
    /// 5. Mesh → создаем 3D mesh из ML контура
    /// 6. Покраска → применяем материал
    /// 
    /// БЕЗ ОЖИДАНИЯ ARKit Plane Detection!
    /// </summary>
    public class MLWallPainter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MLSegmentationManager mlSegmentationManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARCameraManager arCameraManager;
        [SerializeField] private ARPlaneManager planeManager; // 🆕 Для получения размера стены!
        
        [Header("Painting Settings")]
        [SerializeField] private Color paintColor = new Color(0.89f, 0.82f, 0.76f); // Бежевый как Dulux
        [SerializeField] private Material wallMaterial;
        
        [Header("Mesh Settings")]
        [Tooltip("Глубина mesh стены (насколько далеко от стены)")]
        [SerializeField] private float wallMeshDepth = 0.01f;
        
        [Tooltip("Упрощение контура (меньше = точнее, больше = быстрее)")]
        [SerializeField] private float contourSimplificationThreshold = 5f;
        
        [Header("Performance")]
        [Tooltip("Максимальное количество точек контура для mesh")]
        [SerializeField] private int maxContourPoints = 500;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        // Сохраняем покрашенные стены
        private List<GameObject> paintedWalls = new List<GameObject>();
        
        private void Awake()
        {
            // Поиск компонентов если не назначены
            if (mlSegmentationManager == null)
            {
                mlSegmentationManager = FindObjectOfType<MLSegmentationManager>();
                if (mlSegmentationManager == null)
                {
                    Debug.LogError("[MLWallPainter] ❌ MLSegmentationManager не найден!");
                }
            }
            
            if (raycastManager == null)
            {
                raycastManager = FindObjectOfType<ARRaycastManager>();
                if (raycastManager == null)
                {
                    Debug.LogError("[MLWallPainter] ❌ ARRaycastManager не найден!");
                }
            }
            
            if (arCameraManager == null)
            {
                arCameraManager = FindObjectOfType<ARCameraManager>();
            }
            
            if (planeManager == null)
            {
                planeManager = FindObjectOfType<ARPlaneManager>();
                if (planeManager == null)
                {
                    Debug.LogWarning("[MLWallPainter] ⚠️ ARPlaneManager не найден! Будет использоваться fallback без плоскостей.");
                }
            }
            
            // Создаем дефолтный материал если не назначен
            if (wallMaterial == null)
            {
                wallMaterial = CreateDefaultWallMaterial();
            }
        }
        
        /// <summary>
        /// ГЛАВНЫЙ МЕТОД: Покраска стены при клике (как Dulux!)
        /// </summary>
        /// <param name="screenPosition">Позиция клика на экране</param>
        /// <returns>true если удалось покрасить</returns>
        public bool TryPaintWallAtClick(Vector2 screenPosition)
        {
            if (showDebugLogs)
                Debug.Log($"[MLWallPainter] 🎨 Клик: {screenPosition}");
            
            // ШАГ 1: Проверяем ML модель
            if (!mlSegmentationManager.IsInitialized)
            {
                Debug.LogWarning("[MLWallPainter] ⚠️ ML модель не инициализирована!");
                return false;
            }
            
            // ШАГ 2: Конвертируем screen position в normalized (0-1)
            Vector2 normalizedPos = new Vector2(
                screenPosition.x / Screen.width,
                screenPosition.y / Screen.height
            );
            
            // ШАГ 3: Проверяем, клик на стене?
            if (!mlSegmentationManager.IsWall(normalizedPos))
            {
                if (showDebugLogs)
                    Debug.Log("[MLWallPainter] ⚠️ Клик НЕ на стене (ML говорит это не wall)");
                return false;
            }
            
            if (showDebugLogs)
                Debug.Log("[MLWallPainter] ✅ ML подтвердил: это СТЕНА!");
            
            // ШАГ 4: FloodFill - находим все пиксели стены
            HashSet<Vector2Int> wallPixels = mlSegmentationManager.FloodFillWall(normalizedPos);
            
            if (wallPixels == null || wallPixels.Count == 0)
            {
                Debug.LogWarning("[MLWallPainter] ❌ FloodFill не нашел пикселей стены!");
                return false;
            }
            
            if (showDebugLogs)
                Debug.Log($"[MLWallPainter] ✅ FloodFill: {wallPixels.Count} пикселей стены");
            
            // ШАГ 5: Находим контур стены
            List<Vector2Int> contourPoints = mlSegmentationManager.FindWallContour(wallPixels);
            
            if (contourPoints == null || contourPoints.Count < 3)
            {
                Debug.LogWarning("[MLWallPainter] ❌ Недостаточно точек контура!");
                return false;
            }
            
            if (showDebugLogs)
                Debug.Log($"[MLWallPainter] ✅ Контур: {contourPoints.Count} точек");
            
            // ШАГ 6: Упрощаем контур если слишком много точек
            if (contourPoints.Count > maxContourPoints)
            {
                contourPoints = SimplifyContour(contourPoints, maxContourPoints);
                if (showDebugLogs)
                    Debug.Log($"[MLWallPainter] ℹ️ Контур упрощен до {contourPoints.Count} точек");
            }
            
            // ШАГ 7: Raycast для получения 3D позиции стены
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            
            // Пробуем сначала Plane (если ARKit уже нашел плоскости)
            bool hitPlane = raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon);
            
            // Если нет plane - используем FeaturePoint
            if (!hitPlane || hits.Count == 0)
            {
                hitPlane = raycastManager.Raycast(screenPosition, hits, TrackableType.FeaturePoint);
            }
            
            if (!hitPlane || hits.Count == 0)
            {
                Debug.LogWarning("[MLWallPainter] ⚠️ Raycast не попал в стену!");
                return false;
            }
            
            // 🆕 ФИЛЬТР: Ищем первый hit на разумном расстоянии (игнорируем близкие объекты)
            const float MIN_WALL_DISTANCE = 0.5f; // 50 см - минимальное расстояние
            const float MAX_WALL_DISTANCE = 10f;  // 10 метров - максимальное расстояние
            
            ARRaycastHit? selectedHit = null;
            foreach (var raycastHit in hits)
            {
                float distance = Vector3.Distance(raycastHit.pose.position, arCameraManager.transform.position);
                
                // Пропускаем слишком близкие и слишком далекие попадания
                if (distance >= MIN_WALL_DISTANCE && distance <= MAX_WALL_DISTANCE)
                {
                    selectedHit = raycastHit;
                    break; // Берем первый подходящий
                }
            }
            
            if (!selectedHit.HasValue)
            {
                Debug.LogWarning($"[MLWallPainter] ⚠️ Raycast попал только в близкие объекты (< {MIN_WALL_DISTANCE}м) или слишком далекие!");
                return false;
            }
            
            // Берем выбранное попадание
            ARRaycastHit hit = selectedHit.Value;
            
            // 🔥 НОВАЯ ЛОГИКА: Пытаемся найти ARPlane для ПОЛНОГО размера стены!
            ARPlane hitARPlane = null;
            if (planeManager != null && hit.trackable is ARPlane)
            {
                hitARPlane = hit.trackable as ARPlane;
            }
            
            Vector3 wallCenter = hit.pose.position;
            float wallDistance = Vector3.Distance(wallCenter, arCameraManager.transform.position);
            Quaternion wallRotation;
            
            GameObject wallObject = null;
            
            if (hitARPlane != null)
            {
                // 🎯 РЕЖИМ 1: ЕСТЬ AR PLANE → ИСПОЛЬЗУЕМ ЕГО РАЗМЕР! (ВСЯ СТЕНА!)
                Vector2 planeSize = hitARPlane.size; // Реальный размер плоскости!
                wallCenter = hitARPlane.center; // Центр плоскости
                wallRotation = hitARPlane.transform.rotation; // Поворот плоскости
                
                if (showDebugLogs)
                    Debug.Log($"[MLWallPainter] 🎯 ARPlane найден! Размер: {planeSize.x:F2}м x {planeSize.y:F2}м, distance={wallDistance:F2}м");
                
                // Создаем mesh размером С ПЛОСКОСТЬ! (ВСЯ СТЕНА!)
                wallObject = CreateFullWallMeshFromPlane(hitARPlane, paintColor);
            }
            else
            {
                // 🔶 РЕЖИМ 2: НЕТ AR PLANE → ИСПОЛЬЗУЕМ ML + ДИНАМИЧЕСКИЙ SCALE (FALLBACK)
                Vector3 cameraToWall = wallCenter - arCameraManager.transform.position;
                wallRotation = Quaternion.LookRotation(cameraToWall.normalized);
                
                if (showDebugLogs)
                    Debug.LogWarning($"[MLWallPainter] ⚠️ ARPlane не найден, используем fallback. Raycast: позиция={wallCenter}, distance={wallDistance:F2}м");
                
                // Создаем mesh из ML контура (старая логика)
                wallObject = CreateWallMesh(contourPoints, wallPixels.Count, wallCenter, wallRotation);
            }
            
            if (wallObject != null)
            {
                paintedWalls.Add(wallObject);
                
                if (showDebugLogs)
                    Debug.Log($"[MLWallPainter] 🎉 СТЕНА ПОКРАШЕНА! (всего покрашено: {paintedWalls.Count})");
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 🔥 НОВЫЙ МЕТОД: Создание mesh размером С ВСЮ AR ПЛОСКОСТЬ! (КАК DULUX!)
        /// </summary>
        private GameObject CreateFullWallMeshFromPlane(ARPlane arPlane, Color color)
        {
            Vector2 planeSize = arPlane.size;
            Vector3 planeCenter = arPlane.center;
            Quaternion planeRotation = arPlane.transform.rotation;
            
            // Создаем GameObject для стены
            GameObject wallObject = new GameObject($"MLPaintedWall_FULL_{paintedWalls.Count}");
            wallObject.transform.position = planeCenter;
            wallObject.transform.rotation = planeRotation;
            wallObject.layer = LayerMask.NameToLayer("Default");
            
            // Добавляем компоненты
            MeshFilter meshFilter = wallObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = wallObject.AddComponent<MeshRenderer>();
            
            // Создаем ПРОСТОЙ QUAD mesh размером с плоскость!
            Mesh mesh = new Mesh();
            mesh.name = "FullWallMesh";
            
            float halfWidth = planeSize.x / 2f;
            float halfHeight = planeSize.y / 2f;
            
            // 4 вершины квада (вся плоскость!)
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfWidth, -halfHeight, 0), // Нижний левый
                new Vector3(halfWidth, -halfHeight, 0),  // Нижний правый
                new Vector3(-halfWidth, halfHeight, 0),  // Верхний левый
                new Vector3(halfWidth, halfHeight, 0)    // Верхний правый
            };
            
            // 2 треугольника
            int[] triangles = new int[]
            {
                0, 2, 1,  // Первый треугольник
                2, 3, 1   // Второй треугольник
            };
            
            // Нормали (все смотрят вперед)
            Vector3[] normals = new Vector3[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            };
            
            // UV координаты
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            
            meshFilter.mesh = mesh;
            meshRenderer.material = wallMaterial;
            meshRenderer.material.color = color;
            
            if (showDebugLogs)
                Debug.Log($"[MLWallPainter] 🔥 ПОЛНАЯ СТЕНА! Размер: {planeSize.x:F2}м x {planeSize.y:F2}м");
            
            return wallObject;
        }
        
        /// <summary>
        /// FALLBACK: Создание 3D mesh из ML контура стены (если ARPlane не найден)
        /// </summary>
        /// <param name="contourPoints">Упрощенный контур (для mesh)</param>
        /// <param name="originalWallPixelsCount">ИСХОДНОЕ количество пикселей стены (для scale!)</param>
        private GameObject CreateWallMesh(List<Vector2Int> contourPoints, int originalWallPixelsCount, Vector3 wallCenter, Quaternion wallRotation)
        {
            if (contourPoints.Count < 3)
            {
                Debug.LogWarning("[MLWallPainter] ❌ Недостаточно точек для mesh!");
                return null;
            }
            
            // Создаем GameObject для стены
            GameObject wallObject = new GameObject($"MLPaintedWall_{paintedWalls.Count}");
            wallObject.transform.position = wallCenter;
            wallObject.transform.rotation = wallRotation;
            
            // 🆕 Устанавливаем layer чтобы raycast игнорировал наши mesh'и
            // (если нужно - создайте layer "PaintedWalls" в Unity, иначе используем Default)
            wallObject.layer = LayerMask.NameToLayer("Default");
            
            // 🆕 ДИНАМИЧЕСКИЙ масштаб на основе расстояния и размера контура
            float estimatedWallDistance = Vector3.Distance(arCameraManager.transform.position, wallCenter);
            
            // Вычисляем реальный размер стены в мире
            // Логика: чем дальше стена, тем больше должен быть mesh чтобы покрыть ту же область
            // FOV iPhone ~60 градусов вертикально
            // На расстоянии D метров, видимая высота = 2 * D * tan(FOV/2)
            // Для 60° FOV: height ≈ D * 1.15
            
            // 🆕 ИСПРАВЛЕНИЕ: Используем ИСХОДНОЕ количество пикселей стены, а не упрощенного контура!
            int maskResolution = 512;
            float contourSizeInPixels = Mathf.Sqrt(originalWallPixelsCount); // Примерная сторона квадрата области стены
            float contourSizeRatio = contourSizeInPixels / maskResolution; // [0..1]
            
            // Видимая область на расстоянии D
            float visibleHeightAtDistance = estimatedWallDistance * 1.15f; // ~60° FOV
            
            // Реальный размер стены = видимая область * процент контура
            float estimatedWallSize = visibleHeightAtDistance * contourSizeRatio;
            
            // Mesh в local space имеет размер [-1, 1] = 2 единицы
            // Чтобы получить реальный размер, scale = estimatedWallSize / 2
            float meshScale = estimatedWallSize / 2.0f;
            
            // Ограничиваем scale разумными значениями
            meshScale = Mathf.Clamp(meshScale, 0.3f, 5.0f); // От 30 см до 5 метров
            
            wallObject.transform.localScale = Vector3.one * meshScale;
            
            // Добавляем компоненты
            MeshFilter meshFilter = wallObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = wallObject.AddComponent<MeshRenderer>();
            
            // Создаем mesh
            Mesh mesh = GenerateMeshFromContour(contourPoints);
            
            if (mesh == null)
            {
                Destroy(wallObject);
                return null;
            }
            
            meshFilter.mesh = mesh;
            meshRenderer.material = wallMaterial;
            
            // Устанавливаем цвет
            meshRenderer.material.color = paintColor;
            
            if (showDebugLogs)
            {
                Debug.Log($"[MLWallPainter] ✅ Mesh размещен: distance={estimatedWallDistance:F2}м, scale={meshScale:F2}, " +
                          $"contourSize={contourSizeInPixels:F0}px ({contourSizeRatio:P0})");
            }
            
            return wallObject;
        }
        
        /// <summary>
        /// Генерация Unity Mesh из точек контура
        /// Используем Ear Clipping для триангуляции
        /// </summary>
        private Mesh GenerateMeshFromContour(List<Vector2Int> contourPoints)
        {
            int maskResolution = mlSegmentationManager.GetMaskResolution();
            
            // Конвертируем 2D точки контура (в mask space) в 3D вершины (в локальном пространстве стены)
            List<Vector3> vertices3D = new List<Vector3>();
            
            foreach (var point in contourPoints)
            {
                // Нормализация: mask space [0, maskResolution] → local space [-1, 1]
                float x = (point.x / (float)maskResolution) * 2f - 1f;
                float y = (point.y / (float)maskResolution) * 2f - 1f;
                
                // z = 0 для плоского mesh
                vertices3D.Add(new Vector3(x, y, 0f));
            }
            
            // Простая триангуляция: используем центр как pivot (fan triangulation)
            // Для production лучше использовать Ear Clipping или Delaunay
            Mesh mesh = new Mesh();
            mesh.name = "MLWallMesh";
            
            // Добавляем центральную точку
            Vector3 center = Vector3.zero;
            foreach (var v in vertices3D)
            {
                center += v;
            }
            center /= vertices3D.Count;
            
            List<Vector3> finalVertices = new List<Vector3> { center };
            finalVertices.AddRange(vertices3D);
            
            // Создаем треугольники (fan triangulation)
            List<int> triangles = new List<int>();
            int n = vertices3D.Count;
            
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                
                // Треугольник: center → current → next
                triangles.Add(0);        // center
                triangles.Add(i + 1);    // current
                triangles.Add(next + 1); // next
            }
            
            mesh.vertices = finalVertices.ToArray();
            mesh.triangles = triangles.ToArray();
            
            // Нормали (все смотрят вперед)
            Vector3[] normals = new Vector3[finalVertices.Count];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.forward;
            }
            mesh.normals = normals;
            
            // UV координаты для текстуры
            Vector2[] uvs = new Vector2[finalVertices.Count];
            for (int i = 0; i < finalVertices.Count; i++)
            {
                uvs[i] = new Vector2(
                    (finalVertices[i].x + 1f) * 0.5f,
                    (finalVertices[i].y + 1f) * 0.5f
                );
            }
            mesh.uv = uvs;
            
            mesh.RecalculateBounds();
            
            if (showDebugLogs)
                Debug.Log($"[MLWallPainter] ✅ Mesh создан: {mesh.vertexCount} вершин, {mesh.triangles.Length/3} треугольников");
            
            return mesh;
        }
        
        /// <summary>
        /// Упрощение контура (Douglas-Peucker algorithm)
        /// </summary>
        private List<Vector2Int> SimplifyContour(List<Vector2Int> points, int maxPoints)
        {
            if (points.Count <= maxPoints)
                return points;
            
            // Простое упрощение: берем каждую N-ю точку
            int step = Mathf.CeilToInt(points.Count / (float)maxPoints);
            List<Vector2Int> simplified = new List<Vector2Int>();
            
            for (int i = 0; i < points.Count; i += step)
            {
                simplified.Add(points[i]);
            }
            
            // Добавляем последнюю точку если пропустили
            if (simplified[simplified.Count - 1] != points[points.Count - 1])
            {
                simplified.Add(points[points.Count - 1]);
            }
            
            return simplified;
        }
        
        /// <summary>
        /// Создание дефолтного материала для стены
        /// </summary>
        private Material CreateDefaultWallMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = "MLWallPaintMaterial";
            mat.color = paintColor;
            
            // Настройки для реалистичного вида
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            
            return mat;
        }
        
        /// <summary>
        /// Очистка всех покрашенных стен
        /// </summary>
        public void ClearAllPaintedWalls()
        {
            foreach (var wall in paintedWalls)
            {
                if (wall != null)
                {
                    Destroy(wall);
                }
            }
            
            paintedWalls.Clear();
            Debug.Log("[MLWallPainter] 🧹 Все покрашенные стены удалены");
        }
        
        /// <summary>
        /// Установить цвет краски
        /// </summary>
        public void SetPaintColor(Color color)
        {
            paintColor = color;
            
            // Обновляем материал
            if (wallMaterial != null)
            {
                wallMaterial.color = color;
            }
            
            Debug.Log($"[MLWallPainter] 🎨 Цвет изменен: {color}");
        }
        
        /// <summary>
        /// Получить количество покрашенных стен
        /// </summary>
        public int GetPaintedWallsCount()
        {
            return paintedWalls.Count;
        }
    }
}

