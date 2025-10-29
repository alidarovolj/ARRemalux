using System.Collections.Generic;
using UnityEngine;

namespace RemaluxAR.Drawing
{
    /// <summary>
    /// Продвинутый рендерер для создания гладких 3D-линий из последовательности точек
    /// Генерирует трубчатый или ленточный mesh вместо отдельных сфер
    /// </summary>
    public class MeshLineRenderer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float tubeRadius = 0.01f;          // Радиус трубы
        [SerializeField] private int radialSegments = 8;           // Количество сегментов вокруг трубы
        [SerializeField] private float minSegmentLength = 0.01f;   // Минимальная длина сегмента
        [SerializeField] private bool smoothNormals = true;        // Сглаживание нормалей
        [SerializeField] private Material lineMaterial;            // Материал для линии

        private List<Vector3> points = new List<Vector3>();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh lineMesh;

        private void Awake()
        {
            // Создаём компоненты
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            
            lineMesh = new Mesh();
            lineMesh.name = "DynamicLineMesh";
            meshFilter.mesh = lineMesh;

            if (lineMaterial != null)
            {
                meshRenderer.material = lineMaterial;
            }
        }

        /// <summary>
        /// Добавляет точку к линии
        /// </summary>
        public void AddPoint(Vector3 worldPoint)
        {
            // Проверяем минимальное расстояние
            if (points.Count > 0)
            {
                float distance = Vector3.Distance(points[points.Count - 1], worldPoint);
                if (distance < minSegmentLength)
                    return;
            }

            points.Add(worldPoint);
            RegenerateMesh();
        }

        /// <summary>
        /// Очищает все точки
        /// </summary>
        public void ClearPoints()
        {
            points.Clear();
            lineMesh.Clear();
        }

        /// <summary>
        /// Устанавливает цвет линии
        /// </summary>
        public void SetColor(Color color)
        {
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.SetColor("_BaseColor", color);
                meshRenderer.material.SetColor("_Color", color);
            }
        }

        /// <summary>
        /// Перегенерирует mesh линии
        /// </summary>
        private void RegenerateMesh()
        {
            if (points.Count < 2)
            {
                lineMesh.Clear();
                return;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // Создаём tube mesh вдоль линии
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                
                // Вычисляем направление вперёд
                Vector3 forward;
                if (i == 0)
                {
                    forward = (points[i + 1] - point).normalized;
                }
                else if (i == points.Count - 1)
                {
                    forward = (point - points[i - 1]).normalized;
                }
                else
                {
                    forward = (points[i + 1] - points[i - 1]).normalized;
                }

                // Вычисляем right и up векторы
                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
                {
                    up = Vector3.right;
                }
                Vector3 right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;

                // Создаём кольцо вершин вокруг точки
                float angleStep = 360f / radialSegments;
                for (int j = 0; j < radialSegments; j++)
                {
                    float angle = j * angleStep * Mathf.Deg2Rad;
                    Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * tubeRadius;
                    
                    vertices.Add(point + offset);
                    
                    // Нормаль направлена наружу от центра трубы
                    Vector3 normal = offset.normalized;
                    normals.Add(normal);

                    // UV координаты
                    float u = (float)j / radialSegments;
                    float v = (float)i / (points.Count - 1);
                    uvs.Add(new Vector2(u, v));
                }

                // Создаём треугольники между текущим и предыдущим кольцом
                if (i > 0)
                {
                    int currentRingStart = i * radialSegments;
                    int previousRingStart = (i - 1) * radialSegments;

                    for (int j = 0; j < radialSegments; j++)
                    {
                        int next = (j + 1) % radialSegments;

                        // Первый треугольник
                        triangles.Add(previousRingStart + j);
                        triangles.Add(currentRingStart + j);
                        triangles.Add(previousRingStart + next);

                        // Второй треугольник
                        triangles.Add(previousRingStart + next);
                        triangles.Add(currentRingStart + j);
                        triangles.Add(currentRingStart + next);
                    }
                }
            }

            // Закрываем концы трубы (caps)
            AddEndCap(vertices, triangles, normals, uvs, 0, -1);
            AddEndCap(vertices, triangles, normals, uvs, points.Count - 1, 1);

            // Обновляем mesh
            lineMesh.Clear();
            lineMesh.SetVertices(vertices);
            lineMesh.SetTriangles(triangles, 0);
            lineMesh.SetNormals(normals);
            lineMesh.SetUVs(0, uvs);

            if (smoothNormals)
            {
                lineMesh.RecalculateNormals();
            }

            lineMesh.RecalculateBounds();
        }

        /// <summary>
        /// Добавляет крышку на конец трубы
        /// </summary>
        private void AddEndCap(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, 
                               List<Vector2> uvs, int pointIndex, int normalDirection)
        {
            Vector3 center = points[pointIndex];
            Vector3 normal = Vector3.zero;

            if (pointIndex == 0 && points.Count > 1)
            {
                normal = (points[0] - points[1]).normalized;
            }
            else if (pointIndex == points.Count - 1 && points.Count > 1)
            {
                normal = (points[points.Count - 1] - points[points.Count - 2]).normalized;
            }

            int centerIndex = vertices.Count;
            vertices.Add(center);
            normals.Add(normal * normalDirection);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ringStart = pointIndex * radialSegments;

            for (int i = 0; i < radialSegments; i++)
            {
                int next = (i + 1) % radialSegments;

                if (normalDirection > 0)
                {
                    // Для конца линии (обратный порядок)
                    triangles.Add(centerIndex);
                    triangles.Add(ringStart + next);
                    triangles.Add(ringStart + i);
                }
                else
                {
                    // Для начала линии
                    triangles.Add(centerIndex);
                    triangles.Add(ringStart + i);
                    triangles.Add(ringStart + next);
                }
            }
        }

        /// <summary>
        /// Получает количество точек в линии
        /// </summary>
        public int PointCount => points.Count;

        /// <summary>
        /// Устанавливает радиус трубы
        /// </summary>
        public void SetTubeRadius(float radius)
        {
            tubeRadius = radius;
            if (points.Count > 0)
            {
                RegenerateMesh();
            }
        }

        private void OnDestroy()
        {
            if (lineMesh != null)
            {
                Destroy(lineMesh);
            }
        }
    }
}

