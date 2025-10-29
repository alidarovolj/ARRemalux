using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RemaluxAR.Data
{
    /// <summary>
    /// Типы классификации поверхностей (согласно ARKit)
    /// </summary>
    public enum SurfaceClassification
    {
        None = 0,
        Wall = 1,
        Floor = 2,
        Ceiling = 3,
        Table = 4,
        Seat = 5,
        Window = 6,
        Door = 7
    }

    /// <summary>
    /// Представляет сетку поверхности в AR сцене
    /// </summary>
    public class SurfaceMesh
    {
        /// <summary>
        /// MeshFilter компонент
        /// </summary>
        public MeshFilter Filter { get; set; }

        /// <summary>
        /// MeshRenderer компонент
        /// </summary>
        public MeshRenderer Renderer { get; set; }

        /// <summary>
        /// MeshCollider для физических взаимодействий
        /// </summary>
        public MeshCollider Collider { get; set; }

        /// <summary>
        /// Классификации граней меша (если доступно на iOS)
        /// </summary>
        public SurfaceClassification[] FaceClassifications { get; set; }

        /// <summary>
        /// Родительский GameObject
        /// </summary>
        public GameObject GameObject { get; set; }

        /// <summary>
        /// Уникальный идентификатор меша (обычно от TrackableId)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Время последнего обновления меша
        /// </summary>
        public float LastUpdateTime { get; set; }

        /// <summary>
        /// Видимость меша
        /// </summary>
        public bool IsVisible
        {
            get => Renderer != null && Renderer.enabled;
            set
            {
                if (Renderer != null)
                {
                    Renderer.enabled = value;
                }
            }
        }

        public SurfaceMesh()
        {
            LastUpdateTime = Time.time;
        }

        /// <summary>
        /// Применяет цвет к мешу на основе классификации
        /// </summary>
        public void ApplyClassificationColor()
        {
            if (Renderer == null || FaceClassifications == null || FaceClassifications.Length == 0)
                return;

            // Находим наиболее частую классификацию
            SurfaceClassification dominantClass = GetDominantClassification();
            Color color = GetColorForClassification(dominantClass);

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetColor("_BaseColor", color);
            Renderer.SetPropertyBlock(props);
        }

        /// <summary>
        /// Получает доминирующую классификацию меша
        /// </summary>
        private SurfaceClassification GetDominantClassification()
        {
            if (FaceClassifications == null || FaceClassifications.Length == 0)
                return SurfaceClassification.None;

            int[] counts = new int[8];
            foreach (var classification in FaceClassifications)
            {
                counts[(int)classification]++;
            }

            int maxCount = 0;
            int maxIndex = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maxCount)
                {
                    maxCount = counts[i];
                    maxIndex = i;
                }
            }

            return (SurfaceClassification)maxIndex;
        }

        /// <summary>
        /// Получает цвет для визуализации типа поверхности
        /// </summary>
        public static Color GetColorForClassification(SurfaceClassification classification)
        {
            switch (classification)
            {
                case SurfaceClassification.Wall:
                    return new Color(0.8f, 0.8f, 0.9f, 0.5f); // Светло-синий
                case SurfaceClassification.Floor:
                    return new Color(0.6f, 0.4f, 0.2f, 0.5f); // Коричневый
                case SurfaceClassification.Ceiling:
                    return new Color(0.9f, 0.9f, 0.9f, 0.5f); // Белый
                case SurfaceClassification.Table:
                    return new Color(0.7f, 0.5f, 0.3f, 0.5f); // Древесный
                case SurfaceClassification.Seat:
                    return new Color(0.5f, 0.7f, 0.5f, 0.5f); // Зеленоватый
                case SurfaceClassification.Window:
                    return new Color(0.6f, 0.8f, 1.0f, 0.3f); // Голубой прозрачный
                case SurfaceClassification.Door:
                    return new Color(0.8f, 0.6f, 0.4f, 0.5f); // Светло-коричневый
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 0.3f); // Серый
            }
        }
    }
}

