using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RemaluxAR.ML
{
    /// <summary>
    /// УПРОЩЕННЫЙ Мост между Unity AR Camera и CoreML
    /// НЕ требует дополнительных native функций - работает с существующим API
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class ARCameraFrameBridge : MonoBehaviour
    {
        
        [Header("References")]
        [SerializeField] private ARCameraManager arCameraManager;
        [SerializeField] private Camera arCamera;
        
        [Header("Info")]
        [Tooltip("Этот компонент пока отключен - DepthEstimationManager работает напрямую с AR Camera")]
        [SerializeField] private bool isEnabled = false;
        
        private void Awake()
        {
            if (arCameraManager == null)
                arCameraManager = GetComponent<ARCameraManager>();
            
            if (arCamera == null)
                arCamera = Camera.main;
        }
        
        private void Start()
        {
            // ВРЕМЕННО ОТКЛЮЧЕНО: AR Camera frames будут обрабатываться напрямую через ARKit
            // Depth и Segmentation managers работают без этого моста
            
            Debug.Log("[ARCameraFrameBridge] ℹ️ Инициализирован (пока в passive mode)");
            Debug.Log("[ARCameraFrameBridge] ℹ️ AR Camera frames обрабатываются напрямую CoreML plugins");
            
            if (arCameraManager != null)
            {
                Debug.Log($"[ARCameraFrameBridge] ✅ ARCameraManager найден: {arCameraManager.name}");
            }
            
            if (arCamera != null)
            {
                Debug.Log($"[ARCameraFrameBridge] ✅ AR Camera найдена: {arCamera.name}");
            }
        }
    }
}

