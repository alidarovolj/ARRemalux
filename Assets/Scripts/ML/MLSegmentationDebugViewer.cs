using UnityEngine;
using UnityEngine.UI;

namespace RemaluxAR.ML
{
    /// <summary>
    /// DEBUG: Отображает ML segmentation маску на экране
    /// для визуализации того что модель "видит"
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MLSegmentationDebugViewer : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Включить/выключить debug overlay")]
        [SerializeField] private bool enableDebugView = false; // По умолчанию ВЫКЛЮЧЕН!
        
        [Tooltip("Прозрачность overlay (0 = прозрачно, 1 = непрозрачно)")]
        [SerializeField] private float overlayAlpha = 0.5f;
        
        [Tooltip("MLSegmentationManager для получения маски")]
        [SerializeField] private MLSegmentationManager mlManager;
        
        [Header("Color Mapping (DeepLabV3 PASCAL VOC)")]
        [Tooltip("Показывать только стены (если модель их обнаруживает)")]
        [SerializeField] private bool showOnlyWalls = false;
        
        private RawImage rawImage;
        private Texture2D visualizationTexture;
        private int textureResolution = 512;
        
        // DeepLabV3 PASCAL VOC colors
        private readonly Color32[] classColors = new Color32[]
        {
            new Color32(0, 0, 0, 0),        // 0: background (прозрачный)
            new Color32(128, 0, 0, 255),    // 1: aeroplane
            new Color32(0, 128, 0, 255),    // 2: bicycle
            new Color32(128, 128, 0, 255),  // 3: bird
            new Color32(0, 0, 128, 255),    // 4: boat
            new Color32(128, 0, 128, 255),  // 5: bottle
            new Color32(0, 128, 128, 255),  // 6: bus
            new Color32(128, 128, 128, 255),// 7: car
            new Color32(64, 0, 0, 255),     // 8: cat
            new Color32(192, 0, 0, 255),    // 9: chair
            new Color32(64, 128, 0, 255),   // 10: cow
            new Color32(192, 128, 0, 255),  // 11: diningTable
            new Color32(64, 0, 128, 255),   // 12: dog
            new Color32(192, 0, 128, 255),  // 13: horse
            new Color32(64, 128, 128, 255), // 14: motorbike
            new Color32(192, 128, 128, 255),// 15: person (важно!)
            new Color32(0, 64, 0, 255),     // 16: pottedPlant
            new Color32(128, 64, 0, 255),   // 17: sheep
            new Color32(0, 192, 0, 255),    // 18: sofa
            new Color32(128, 192, 0, 255),  // 19: train
            new Color32(0, 64, 128, 255),   // 20: tvOrMonitor
        };
        
        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            
            // Создаем текстуру для визуализации
            visualizationTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
            visualizationTexture.filterMode = FilterMode.Point; // Pixel-perfect
            
            rawImage.texture = visualizationTexture;
            rawImage.color = new Color(1, 1, 1, overlayAlpha);
            
            if (mlManager == null)
            {
                mlManager = FindObjectOfType<MLSegmentationManager>();
            }
        }
        
        private void Update()
        {
            // Если debug view выключен - скрываем overlay
            if (!enableDebugView)
            {
                if (rawImage != null && rawImage.enabled)
                    rawImage.enabled = false;
                return;
            }
            
            // Включаем overlay если был выключен
            if (rawImage != null && !rawImage.enabled)
                rawImage.enabled = true;
            
            if (mlManager == null || !mlManager.IsInitialized)
                return;
            
            UpdateVisualization();
        }
        
        private void UpdateVisualization()
        {
            // Получаем маску от ML Manager
            byte[] mask = GetSegmentationMask();
            if (mask == null || mask.Length == 0)
            {
                Debug.LogWarning("[MLDebugViewer] Маска пустая!");
                return;
            }
            
            // Конвертируем class IDs в цвета
            Color32[] pixels = new Color32[mask.Length];
            
            for (int i = 0; i < mask.Length; i++)
            {
                int classId = mask[i];
                
                if (showOnlyWalls)
                {
                    // Показываем только "стены" (но DeepLabV3 их не знает!)
                    // Попробуем показать "background" как красный для теста
                    if (classId == 0)
                        pixels[i] = new Color32(255, 0, 0, 128); // Красный = background
                    else
                        pixels[i] = new Color32(0, 0, 0, 0); // Прозрачный
                }
                else
                {
                    // Показываем все классы
                    if (classId < classColors.Length)
                        pixels[i] = classColors[classId];
                    else
                        pixels[i] = new Color32(255, 255, 255, 255); // Белый для неизвестных
                }
            }
            
            // Обновляем текстуру
            visualizationTexture.SetPixels32(pixels);
            visualizationTexture.Apply();
        }
        
        /// <summary>
        /// Получает текущую segmentation маску через reflection
        /// (так как метод может быть private в MLSegmentationManager)
        /// </summary>
        private byte[] GetSegmentationMask()
        {
            try
            {
                // Используем reflection чтобы получить доступ к приватному полю
                var field = typeof(MLSegmentationManager).GetField("currentSegmentationMask", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    return field.GetValue(mlManager) as byte[];
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MLDebugViewer] Не удалось получить маску: {e.Message}");
            }
            
            return null;
        }
        
        private void OnDestroy()
        {
            if (visualizationTexture != null)
            {
                Destroy(visualizationTexture);
            }
        }
        
        /// <summary>
        /// Показывает статистику по классам в маске
        /// </summary>
        [ContextMenu("Show Class Statistics")]
        public void ShowClassStatistics()
        {
            byte[] mask = GetSegmentationMask();
            if (mask == null) return;
            
            // Подсчитываем пиксели каждого класса
            int[] classCounts = new int[21];
            foreach (byte classId in mask)
            {
                if (classId < classCounts.Length)
                    classCounts[classId]++;
            }
            
            Debug.Log("=== ML Segmentation Class Statistics ===");
            string[] classNames = {
                "background", "aeroplane", "bicycle", "bird", "boat", "bottle", 
                "bus", "car", "cat", "chair", "cow", "diningTable", "dog", 
                "horse", "motorbike", "person", "pottedPlant", "sheep", "sofa", 
                "train", "tvOrMonitor"
            };
            
            for (int i = 0; i < classCounts.Length; i++)
            {
                if (classCounts[i] > 0)
                {
                    float percentage = (classCounts[i] / (float)mask.Length) * 100f;
                    Debug.Log($"  {classNames[i]}: {classCounts[i]} pixels ({percentage:F1}%)");
                }
            }
        }
    }
}

