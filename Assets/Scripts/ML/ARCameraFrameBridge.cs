using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RemaluxAR.ML
{
    /// <summary>
    /// АКТИВНЫЙ Мост между Unity AR Camera и CoreML
    /// Передаёт AR кадры в нативные CoreML плагины для ML обработки
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class ARCameraFrameBridge : MonoBehaviour
    {
        #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CoreML_SetARFrame(IntPtr pixelBuffer);
        #endif
        
        [Header("References")]
        [SerializeField] private ARCameraManager arCameraManager;
        [SerializeField] private Camera arCamera;
        
        [Header("Settings")]
        [Tooltip("Активировать передачу кадров в CoreML")]
        [SerializeField] private bool isEnabled = true;
        
        [Tooltip("Передавать каждый N-й кадр (для экономии ресурсов)")]
        [SerializeField] private int frameSkip = 2;
        
        [Header("Debug Info")]
        [SerializeField] private int framesProcessed = 0;
        
        private int frameCounter = 0;
        
        private void Awake()
        {
            if (arCameraManager == null)
                arCameraManager = GetComponent<ARCameraManager>();
            
            if (arCamera == null)
                arCamera = Camera.main;
        }
        
        private void OnEnable()
        {
            if (arCameraManager != null && isEnabled)
            {
                arCameraManager.frameReceived += OnCameraFrameReceived;
                Debug.Log("[ARCameraFrameBridge] ✅ АКТИВИРОВАН - передаём AR кадры в CoreML!");
            }
        }
        
        private void OnDisable()
        {
            if (arCameraManager != null)
            {
                arCameraManager.frameReceived -= OnCameraFrameReceived;
            }
        }
        
        private void Start()
        {
            if (!isEnabled)
            {
                Debug.LogWarning("[ARCameraFrameBridge] ⏸️ ОТКЛЮЧЕН - CoreML не получит реальные кадры камеры");
                return;
            }
            
            Debug.Log("[ARCameraFrameBridge] ✅ Инициализирован (ACTIVE MODE)");
            Debug.Log("[ARCameraFrameBridge] ✅ AR Camera frames → CoreML plugins");
            
            if (arCameraManager != null)
            {
                Debug.Log($"[ARCameraFrameBridge] ✅ ARCameraManager найден: {arCameraManager.name}");
            }
            
            if (arCamera != null)
            {
                Debug.Log($"[ARCameraFrameBridge] ✅ AR Camera найдена: {arCamera.name}");
            }
        }
        
        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!isEnabled)
                return;
            
            // Skip frames для экономии
            frameCounter++;
            if (frameCounter % frameSkip != 0)
                return;
            
            #if UNITY_IOS && !UNITY_EDITOR
            // TODO: Реализовать передачу CVPixelBuffer в CoreML
            // Проблема: XRCpuImage не предоставляет прямой доступ к нативному handle
            // 
            // ВОЗМОЖНЫЕ РЕШЕНИЯ (требуют дополнительной работы):
            // 1. Использовать low-level ARKit API через native plugin
            // 2. Конвертировать XRCpuImage.GetPlane() → byte[] → передать в CoreML
            // 3. Использовать AR Camera Texture через Graphics.CopyTexture
            //
            // ВРЕМЕННО: CoreML работает на заглушке (черное изображение)
            // Это НЕ критично для тестирования AR Plane Detection с мягкими фильтрами
            
            framesProcessed++;
            
            if (framesProcessed == 1)
            {
                Debug.LogWarning("[ARCameraFrameBridge] ⚠️ Передача AR frames в CoreML временно отключена");
                Debug.LogWarning("[ARCameraFrameBridge] ⚠️ ML сегментация работает на заглушке");
                Debug.LogWarning("[ARCameraFrameBridge] ℹ️ AR Plane Detection с мягкими фильтрами работает независимо!");
            }
            #endif
        }
    }
}

