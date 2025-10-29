using System.Collections.Generic;
using UnityEngine;

namespace RemaluxAR.Data
{
    /// <summary>
    /// Представляет один штрих/мазок краски в AR пространстве
    /// </summary>
    public class PaintStroke
    {
        /// <summary>
        /// Список точек в мировых координатах, составляющих штрих
        /// </summary>
        public List<Vector3> Points { get; private set; }

        /// <summary>
        /// Цвет штриха
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// Толщина штриха (в метрах)
        /// </summary>
        public float Thickness { get; set; }

        /// <summary>
        /// GameObject'ы, представляющие визуализацию штриха
        /// </summary>
        public List<GameObject> Renderers { get; private set; }

        /// <summary>
        /// Уникальный идентификатор штриха
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Время создания штриха
        /// </summary>
        public float CreationTime { get; private set; }

        public PaintStroke(Color color, float thickness)
        {
            Points = new List<Vector3>();
            Renderers = new List<GameObject>();
            Color = color;
            Thickness = thickness;
            Id = System.Guid.NewGuid().ToString();
            CreationTime = Time.time;
        }

        /// <summary>
        /// Добавляет точку к штриху
        /// </summary>
        public void AddPoint(Vector3 point)
        {
            Points.Add(point);
        }

        /// <summary>
        /// Добавляет визуальный объект к штриху
        /// </summary>
        public void AddRenderer(GameObject renderer)
        {
            Renderers.Add(renderer);
        }

        /// <summary>
        /// Очищает все рендереры штриха
        /// </summary>
        public void ClearRenderers()
        {
            foreach (var renderer in Renderers)
            {
                if (renderer != null)
                {
                    Object.Destroy(renderer);
                }
            }
            Renderers.Clear();
        }

        /// <summary>
        /// Получает общее количество точек в штрихе
        /// </summary>
        public int PointCount => Points.Count;
    }
}

