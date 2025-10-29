using System.Collections.Generic;
using UnityEngine;

namespace RemaluxAR.Utils
{
    /// <summary>
    /// Пул объектов для переиспользования GameObject'ов (оптимизация производительности)
    /// </summary>
    /// <typeparam name="T">Тип компонента</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Queue<T> availableObjects = new Queue<T>();
        private readonly HashSet<T> activeObjects = new HashSet<T>();
        private readonly int maxSize;

        public int AvailableCount => availableObjects.Count;
        public int ActiveCount => activeObjects.Count;
        public int TotalCount => AvailableCount + ActiveCount;

        /// <summary>
        /// Создаёт новый пул объектов
        /// </summary>
        /// <param name="prefab">Префаб для создания объектов</param>
        /// <param name="parent">Родительский Transform</param>
        /// <param name="initialSize">Начальный размер пула</param>
        /// <param name="maxSize">Максимальный размер пула (0 = без ограничений)</param>
        public ObjectPool(T prefab, Transform parent = null, int initialSize = 10, int maxSize = 0)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.maxSize = maxSize;

            // Предварительно создаём объекты
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewObject();
            }
        }

        /// <summary>
        /// Создаёт новый объект в пуле
        /// </summary>
        private T CreateNewObject()
        {
            T obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            availableObjects.Enqueue(obj);
            return obj;
        }

        /// <summary>
        /// Получает объект из пула
        /// </summary>
        public T Get()
        {
            T obj;

            if (availableObjects.Count > 0)
            {
                obj = availableObjects.Dequeue();
            }
            else
            {
                // Проверяем лимит
                if (maxSize > 0 && TotalCount >= maxSize)
                {
                    Debug.LogWarning($"[ObjectPool] Reached max size {maxSize}, reusing oldest active object");
                    // Можно реализовать стратегию вытеснения, но пока просто создаём новый
                    return null;
                }

                obj = CreateNewObject();
            }

            obj.gameObject.SetActive(true);
            activeObjects.Add(obj);
            return obj;
        }

        /// <summary>
        /// Возвращает объект в пул
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;

            if (activeObjects.Remove(obj))
            {
                obj.gameObject.SetActive(false);
                availableObjects.Enqueue(obj);
            }
        }

        /// <summary>
        /// Возвращает все активные объекты в пул
        /// </summary>
        public void ReturnAll()
        {
            var objectsToReturn = new List<T>(activeObjects);
            foreach (var obj in objectsToReturn)
            {
                Return(obj);
            }
        }

        /// <summary>
        /// Уничтожает все объекты в пуле
        /// </summary>
        public void DestroyAll()
        {
            foreach (var obj in availableObjects)
            {
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }

            foreach (var obj in activeObjects)
            {
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }

            availableObjects.Clear();
            activeObjects.Clear();
        }

        /// <summary>
        /// Очищает пул до заданного размера
        /// </summary>
        public void Trim(int targetSize)
        {
            while (availableObjects.Count > targetSize && availableObjects.Count > 0)
            {
                var obj = availableObjects.Dequeue();
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }
        }
    }
}

