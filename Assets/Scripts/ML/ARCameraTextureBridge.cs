using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace RemaluxAR.ML
{
    /// <summary>
    /// РАБОТАЮЩИЙ мост AR Camera → CoreML через XRCpuImage API
    /// Использует правильный метод получения кадров!
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class ARCameraTextureBridge : MonoBehaviour
    {
        #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CoreML_SetARFrameFromTexture(IntPtr pixelData, int width, int height);
        #endif
        
        [Header("References")]
        [SerializeField] private ARCameraManager arCameraManager;
        
        [Header("Settings")]
        [Tooltip("Активировать передачу кадров в CoreML")]
        [SerializeField] private bool isEnabled = true;
        
        [Tooltip("Передавать каждый N-й кадр")]
        [SerializeField] private int frameSkip = 3; // Каждый 3-й кадр (~20 FPS)
        
        [Tooltip("Разрешение для ML (меньше = быстрее)")]
        [SerializeField] private int mlResolution = 512;
        
        [Header("Debug")]
        [SerializeField] private int framesProcessed = 0;
        [SerializeField] private float avgProcessTime = 0f;
        
        private int frameCounter = 0;
        
        private void Awake()
        {
            // Auto-find ARCameraManager на этом же GameObject
            if (arCameraManager == null)
            {
                arCameraManager = GetComponent<ARCameraManager>();
                if (arCameraManager == null)
                {
                    Debug.LogError("[ARCameraTextureBridge] ❌ ARCameraManager не найден! " +
                                  "Добавьте этот компонент на тот же GameObject, где находится ARCameraManager " +
                                  "(обычно Main Camera / AR Camera)");
                }
                else
                {
                    Debug.Log("[ARCameraTextureBridge] ✅ ARCameraManager найден автоматически");
                }
            }
        }
        
        private void Start()
        {
            if (isEnabled)
            {
                Debug.Log($"[ARCameraTextureBridge] ✅ АКТИВИРОВАН (через XRCpuImage API)");
                Debug.Log($"[ARCameraTextureBridge] Resolution: {mlResolution}x{mlResolution}");
            }
            else
            {
                Debug.Log($"[ARCameraTextureBridge] ⏸️ ОТКЛЮЧЕН");
            }
        }
        
        private void Update()
        {
            if (!isEnabled) return;
            
            // Skip frames для экономии
            frameCounter++;
            if (frameCounter % frameSkip != 0)
                return;
            
            #if UNITY_IOS && !UNITY_EDITOR
            CaptureAndSendFrame();
            #endif
        }
        
        #if UNITY_IOS && !UNITY_EDITOR
        private void CaptureAndSendFrame()
        {
            float startTime = Time.realtimeSinceStartup;
            
            try
            {
                // ✅ ПРАВИЛЬНЫЙ МЕТОД: Получаем кадр через XRCpuImage API
                if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                {
                    if (framesProcessed == 0)
                        Debug.LogWarning("[ARCameraTextureBridge] ⚠️ Не удалось получить XRCpuImage (камера еще не готова)");
                    return;
                }
                
                using (image)
                {
                    // Конвертируем в RGB24 формат
                    var conversionParams = new XRCpuImage.ConversionParams
                    {
                        inputRect = new RectInt(0, 0, image.width, image.height),
                        outputDimensions = new Vector2Int(mlResolution, mlResolution),
                        outputFormat = TextureFormat.RGB24,
                        transformation = XRCpuImage.Transformation.MirrorY
                    };
                    
                    // Считаем размер буфера
                    int size = image.GetConvertedDataSize(conversionParams);
                    
                    // Выделяем NativeArray для данных
                    var buffer = new NativeArray<byte>(size, Allocator.Temp);
                    
                    try
                    {
                        // Конвертируем image → buffer
                        image.Convert(conversionParams, buffer);
                        
                        // Передаем в CoreML
                        unsafe
                        {
                            CoreML_SetARFrameFromTexture(
                                (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(buffer), 
                                mlResolution, 
                                mlResolution
                            );
                        }
                        
                        framesProcessed++;
                        
                        float processTime = (Time.realtimeSinceStartup - startTime) * 1000f;
                        avgProcessTime = (avgProcessTime * 0.9f) + (processTime * 0.1f);
                        
                        if (framesProcessed == 1)
                        {
                            Debug.Log($"[ARCameraTextureBridge] ✅ Первый кадр отправлен в CoreML!");
                            Debug.Log($"[ARCameraTextureBridge] Source: {image.width}x{image.height} → ML: {mlResolution}x{mlResolution}");
                            Debug.Log($"[ARCameraTextureBridge] Format: {image.format} → RGB24, Size: {size} bytes");
                        }
                        else if (framesProcessed % 100 == 0)
                        {
                            Debug.Log($"[ARCameraTextureBridge] ℹ️ {framesProcessed} frames, avg time: {avgProcessTime:F1}ms");
                        }
                    }
                    finally
                    {
                        buffer.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ARCameraTextureBridge] ❌ Ошибка: {e.Message}\n{e.StackTrace}");
            }
        }
        #endif
        
        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            Debug.Log($"[ARCameraTextureBridge] {(enabled ? "✅ Включен" : "⏸️ Отключен")}");
        }
    }
}

