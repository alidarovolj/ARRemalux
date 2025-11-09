using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Управляет окраской стен - "Fill Whole Wall" режим (как Dulux)
    /// Клик на стену → вся стена закрашивается выбранным цветом
    /// </summary>
    public class WallPaintingManager : MonoBehaviour
    {
        [Header("Paint Settings")]
        [Tooltip("Текущий цвет краски")]
        [SerializeField] private Color paintColor = new Color(0.89f, 0.82f, 0.76f); // Бежевый как на скриншоте
        
        [Tooltip("Альфа канал (прозрачность) 0.5-0.9 для реалистичности")]
        [SerializeField] private float paintAlpha = 0.85f;
        
        [Header("Materials")]
        [Tooltip("Shader для окраски стен")]
        [SerializeField] private Shader paintShader;
        
        // Окрашенные стены
        private Dictionary<ARPlane, Material> paintedWalls = new Dictionary<ARPlane, Material>();
        
        // Оригинальные материалы (для отката)
        private Dictionary<ARPlane, Material> originalMaterials = new Dictionary<ARPlane, Material>();
        
        private void Awake()
        {
            // Используем URP Lit shader для реалистичной окраски
            if (paintShader == null)
            {
                paintShader = Shader.Find("Universal Render Pipeline/Lit");
                
                if (paintShader == null)
                {
                    Debug.LogWarning("[WallPainting] URP Lit shader не найден, используем Standard");
                    paintShader = Shader.Find("Standard");
                }
            }
        }
        
        /// <summary>
        /// Окрашивает стену выбранным цветом
        /// </summary>
        public void PaintWall(ARPlane wall, Color? customColor = null)
        {
            if (wall == null)
            {
                Debug.LogWarning("[WallPainting] Wall is null!");
                return;
            }
            
            Color colorToUse = customColor ?? paintColor;
            colorToUse.a = paintAlpha;
            
            var meshRenderer = wall.GetComponent<MeshRenderer>();
            
            if (meshRenderer == null)
            {
                Debug.LogWarning($"[WallPainting] ❌ Стена {wall.trackableId} не имеет MeshRenderer!");
                return;
            }
            
            // ⚠️ КРИТИЧЕСКАЯ ПРОВЕРКА: У плоскости должен быть mesh!
            var meshFilter = wall.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.mesh == null || meshFilter.mesh.vertexCount == 0)
            {
                Debug.LogWarning($"[WallPainting] ⏳ Стена {wall.trackableId} еще не имеет mesh! " +
                                $"Подождите пару секунд, пока ARKit сгенерирует geometry. " +
                                $"Продолжайте медленно двигать телефон.");
                return;
            }
            
            Debug.Log($"[WallPainting] ✅ Mesh готов: {meshFilter.mesh.vertexCount} vertices");
            
            // Сохраняем оригинальный материал если еще не сохранен
            if (!originalMaterials.ContainsKey(wall))
            {
                originalMaterials[wall] = meshRenderer.material;
            }
            
            // Создаем или обновляем материал краски
            Material paintMaterial;
            
            if (paintedWalls.ContainsKey(wall))
            {
                // Стена уже окрашена - обновляем цвет
                paintMaterial = paintedWalls[wall];
                paintMaterial.color = colorToUse;
                Debug.Log($"[WallPainting] 🎨 Обновлен цвет стены {wall.trackableId} → {colorToUse}");
            }
            else
            {
                // Первая окраска - создаем новый материал
                paintMaterial = new Material(paintShader);
                ConfigurePaintMaterial(paintMaterial, colorToUse);
                
                meshRenderer.material = paintMaterial;
                paintedWalls[wall] = paintMaterial;
                
                Debug.Log($"[WallPainting] ✅ Стена окрашена: {wall.trackableId}, размер: {wall.size}, цвет: {colorToUse}");
            }
            
            // Вибрация для обратной связи
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif
        }
        
        /// <summary>
        /// Настраивает материал краски для реалистичного вида
        /// </summary>
        private void ConfigurePaintMaterial(Material mat, Color color)
        {
            mat.color = color;
            
            // URP/Lit shader настройки
            if (mat.HasProperty("_Surface"))
            {
                // Прозрачный режим
                mat.SetFloat("_Surface", 1); // 1 = Transparent
                mat.SetFloat("_Blend", 0); // 0 = Alpha blending
                
                // Blend mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                
                mat.renderQueue = 3000; // Transparent queue
            }
            
            // Параметры освещения
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.3f); // Матовая поверхность (как краска)
            }
            
            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", 0.0f); // Не металлическая
            }
        }
        
        /// <summary>
        /// Удаляет краску со стены (возвращает оригинальный материал)
        /// </summary>
        public void UnpaintWall(ARPlane wall)
        {
            if (wall == null || !paintedWalls.ContainsKey(wall))
                return;
            
            var meshRenderer = wall.GetComponent<MeshRenderer>();
            
            if (meshRenderer != null && originalMaterials.ContainsKey(wall))
            {
                meshRenderer.material = originalMaterials[wall];
            }
            
            // Удаляем материал краски
            if (paintedWalls.ContainsKey(wall))
            {
                Destroy(paintedWalls[wall]);
                paintedWalls.Remove(wall);
            }
            
            Debug.Log($"[WallPainting] ❌ Краска удалена со стены {wall.trackableId}");
        }
        
        /// <summary>
        /// Очищает краску со всех стен
        /// </summary>
        public void UnpaintAllWalls()
        {
            foreach (var kvp in paintedWalls)
            {
                ARPlane wall = kvp.Key;
                
                if (wall != null)
                {
                    var meshRenderer = wall.GetComponent<MeshRenderer>();
                    
                    if (meshRenderer != null && originalMaterials.ContainsKey(wall))
                    {
                        meshRenderer.material = originalMaterials[wall];
                    }
                }
                
                // Удаляем material
                Destroy(kvp.Value);
            }
            
            paintedWalls.Clear();
            Debug.Log("[WallPainting] ✅ Вся краска очищена!");
        }
        
        /// <summary>
        /// Устанавливает новый цвет краски
        /// </summary>
        public void SetPaintColor(Color color)
        {
            paintColor = color;
            Debug.Log($"[WallPainting] Цвет краски изменен на {color}");
        }
        
        /// <summary>
        /// Проверяет окрашена ли стена
        /// </summary>
        public bool IsWallPainted(ARPlane wall)
        {
            return paintedWalls.ContainsKey(wall);
        }
        
        /// <summary>
        /// Получает цвет окраски стены
        /// </summary>
        public Color? GetWallColor(ARPlane wall)
        {
            if (paintedWalls.ContainsKey(wall))
            {
                return paintedWalls[wall].color;
            }
            return null;
        }
        
        /// <summary>
        /// Получает количество окрашенных стен
        /// </summary>
        public int GetPaintedWallsCount()
        {
            return paintedWalls.Count;
        }
        
        private void OnDestroy()
        {
            // Cleanup
            foreach (var mat in paintedWalls.Values)
            {
                if (mat != null)
                    Destroy(mat);
            }
        }
    }
}

