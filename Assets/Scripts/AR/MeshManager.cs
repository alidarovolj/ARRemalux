using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using RemaluxAR.Data;
using RemaluxAR.Utils;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Управляет AR mesh'ами - обработка LiDAR mesh на iOS и depth-based mesh на Android
    /// </summary>
    public class MeshManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARManager arManager;
        [SerializeField] private Material meshVisualizationMaterial;

        [Header("Settings")]
        [SerializeField] private bool visualizeMeshes = true;
        [SerializeField] private bool enableMeshColliders = true;
        [SerializeField] private bool colorCodeByClassification = true;

        // Хранилище mesh объектов
        private Dictionary<TrackableId, SurfaceMesh> surfaceMeshes = new Dictionary<TrackableId, SurfaceMesh>();

        // Events
        public event System.Action<SurfaceMesh> OnMeshAdded;
        public event System.Action<SurfaceMesh> OnMeshUpdated;
        public event System.Action<TrackableId> OnMeshRemoved;

        public int MeshCount => surfaceMeshes.Count;
        public IReadOnlyDictionary<TrackableId, SurfaceMesh> SurfaceMeshes => surfaceMeshes;

        private void Awake()
        {
            if (arManager == null)
            {
                arManager = FindObjectOfType<ARManager>();
            }
        }

        private void OnEnable()
        {
            if (arManager != null && arManager.MeshManager != null)
            {
                arManager.MeshManager.meshesChanged += OnMeshesChanged;
            }
        }

        private void OnDisable()
        {
            if (arManager != null && arManager.MeshManager != null)
            {
                arManager.MeshManager.meshesChanged -= OnMeshesChanged;
            }
        }

        /// <summary>
        /// Обработчик изменений mesh (добавление, обновление, удаление)
        /// </summary>
        private void OnMeshesChanged(ARMeshesChangedEventArgs args)
        {
            // Обработка добавленных mesh'ей
            foreach (var meshFilter in args.added)
            {
                AddMesh(meshFilter);
            }

            // Обработка обновлённых mesh'ей
            foreach (var meshFilter in args.updated)
            {
                UpdateMesh(meshFilter);
            }

            // Обработка удалённых mesh'ей
            foreach (var meshFilter in args.removed)
            {
                RemoveMesh(meshFilter);
            }
        }

        /// <summary>
        /// Добавляет новый mesh
        /// </summary>
        private void AddMesh(MeshFilter meshFilter)
        {
            if (meshFilter == null) return;

            // Используем instanceID как уникальный идентификатор
            // В AR Foundation 5.2+ ARMesh компонент может отсутствовать
            TrackableId trackableId = new TrackableId((ulong)meshFilter.GetInstanceID(), 0);

            // Создаём SurfaceMesh объект
            SurfaceMesh surfaceMesh = new SurfaceMesh
            {
                Id = trackableId.ToString(),
                Filter = meshFilter,
                Renderer = meshFilter.GetComponent<MeshRenderer>(),
                GameObject = meshFilter.gameObject,
                LastUpdateTime = Time.time
            };

            // Настраиваем MeshCollider если нужно
            if (enableMeshColliders)
            {
                var collider = meshFilter.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                }
                surfaceMesh.Collider = collider;
                collider.sharedMesh = meshFilter.sharedMesh;
            }

            // Настраиваем материал
            if (surfaceMesh.Renderer != null && meshVisualizationMaterial != null)
            {
                surfaceMesh.Renderer.material = meshVisualizationMaterial;
                surfaceMesh.Renderer.enabled = visualizeMeshes;
            }

            // Пытаемся извлечь классификацию (доступно только на iOS с LiDAR)
            ExtractMeshClassification(meshFilter, surfaceMesh);

            // Применяем цветовое кодирование если включено
            if (colorCodeByClassification)
            {
                surfaceMesh.ApplyClassificationColor();
            }

            surfaceMeshes[trackableId] = surfaceMesh;
            OnMeshAdded?.Invoke(surfaceMesh);

            Debug.Log($"[MeshManager] Mesh added: {trackableId}, vertices: {meshFilter.sharedMesh?.vertexCount ?? 0}");
        }

        /// <summary>
        /// Обновляет существующий mesh
        /// </summary>
        private void UpdateMesh(MeshFilter meshFilter)
        {
            if (meshFilter == null) return;

            // Используем instanceID как уникальный идентификатор
            TrackableId trackableId = new TrackableId((ulong)meshFilter.GetInstanceID(), 0);

            if (surfaceMeshes.TryGetValue(trackableId, out SurfaceMesh surfaceMesh))
            {
                surfaceMesh.LastUpdateTime = Time.time;
                
                // Обновляем collider если есть
                if (surfaceMesh.Collider != null)
                {
                    surfaceMesh.Collider.sharedMesh = meshFilter.sharedMesh;
                }

                // Обновляем классификацию
                ExtractMeshClassification(meshFilter, surfaceMesh);

                // Переприменяем цветовое кодирование
                if (colorCodeByClassification)
                {
                    surfaceMesh.ApplyClassificationColor();
                }

                OnMeshUpdated?.Invoke(surfaceMesh);
            }
        }

        /// <summary>
        /// Удаляет mesh
        /// </summary>
        private void RemoveMesh(MeshFilter meshFilter)
        {
            if (meshFilter == null) return;

            // Используем instanceID как уникальный идентификатор
            TrackableId trackableId = new TrackableId((ulong)meshFilter.GetInstanceID(), 0);

            if (surfaceMeshes.Remove(trackableId))
            {
                OnMeshRemoved?.Invoke(trackableId);
                Debug.Log($"[MeshManager] Mesh removed: {trackableId}");
            }
        }

        /// <summary>
        /// Извлекает классификацию граней mesh (iOS LiDAR)
        /// </summary>
        private void ExtractMeshClassification(MeshFilter meshFilter, SurfaceMesh surfaceMesh)
        {
#if UNITY_IOS && !UNITY_EDITOR
            // В AR Foundation 5.2+ классификация может быть получена через ARMeshManager
            // Пока оставим заглушку - классификация будет добавлена при необходимости
            // TODO: Реализовать получение классификации через ARMeshManager API
            Debug.Log($"[MeshManager] Mesh classification extraction not yet implemented for AR Foundation 5.2+");
#endif
        }

        /// <summary>
        /// Конвертирует mesh classification в наш enum
        /// </summary>
        private SurfaceClassification ConvertClassification(byte classification)
        {
            // ARKit mesh classification values (byte values)
            // 0 = None, 1 = Wall, 2 = Floor, 3 = Ceiling, 4 = Table, 5 = Seat, 6 = Window, 7 = Door
            switch (classification)
            {
                case 1:
                    return SurfaceClassification.Wall;
                case 2:
                    return SurfaceClassification.Floor;
                case 3:
                    return SurfaceClassification.Ceiling;
                case 4:
                    return SurfaceClassification.Table;
                case 5:
                    return SurfaceClassification.Seat;
                case 6:
                    return SurfaceClassification.Window;
                case 7:
                    return SurfaceClassification.Door;
                default:
                    return SurfaceClassification.None;
            }
        }

        /// <summary>
        /// Переключает видимость всех meshes
        /// </summary>
        public void SetMeshesVisible(bool visible)
        {
            visualizeMeshes = visible;
            foreach (var mesh in surfaceMeshes.Values)
            {
                mesh.IsVisible = visible;
            }
        }

        /// <summary>
        /// Очищает все mesh'и
        /// </summary>
        public void ClearAllMeshes()
        {
            surfaceMeshes.Clear();
        }

        /// <summary>
        /// Получает ближайший mesh к указанной точке
        /// </summary>
        public SurfaceMesh GetNearestMesh(Vector3 worldPosition)
        {
            SurfaceMesh nearest = null;
            float minDistance = float.MaxValue;

            foreach (var mesh in surfaceMeshes.Values)
            {
                if (mesh.GameObject != null)
                {
                    float distance = Vector3.Distance(worldPosition, mesh.GameObject.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = mesh;
                    }
                }
            }

            return nearest;
        }
    }
}

