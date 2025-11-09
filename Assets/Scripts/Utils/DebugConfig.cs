using UnityEngine;

namespace RemaluxAR.Utils
{
    /// <summary>
    /// Глобальные настройки debug логирования
    /// Измените в Inspector для контроля количества логов
    /// </summary>
    public class DebugConfig : MonoBehaviour
    {
        [Header("Logging Settings")]
        [Tooltip("Логи обнаружения плоскостей (HybridWallDetector)")]
        public static bool LogPlaneDetection = false; // ❌ ОТКЛЮЧЕНО - слишком много
        
        [Tooltip("Логи обновления плоскостей")]
        public static bool LogPlaneUpdates = false; // ❌ ОТКЛЮЧЕНО - слишком много
        
        [Tooltip("Логи визуализации границ")]
        public static bool LogBorderVisualization = false; // ❌ ОТКЛЮЧЕНО
        
        [Tooltip("Логи ML Segmentation inference")]
        public static bool LogMLInference = false; // ❌ ОТКЛЮЧЕНО
        
        [Tooltip("✅ ВАЖНЫЕ логи (обнаружение стен, покраска)")]
        public static bool LogImportantEvents = true; // ✅ ОСТАВЛЯЕМ
        
        [Header("Настройки в инспекторе (для изменения в runtime)")]
        [SerializeField] private bool _logPlaneDetection = false;
        [SerializeField] private bool _logPlaneUpdates = false;
        [SerializeField] private bool _logBorderVisualization = false;
        [SerializeField] private bool _logMLInference = false;
        [SerializeField] private bool _logImportantEvents = true;
        
        private void Awake()
        {
            // Применяем настройки из Inspector
            LogPlaneDetection = _logPlaneDetection;
            LogPlaneUpdates = _logPlaneUpdates;
            LogBorderVisualization = _logBorderVisualization;
            LogMLInference = _logMLInference;
            LogImportantEvents = _logImportantEvents;
            
            Debug.Log("[DebugConfig] ✅ Настройки логирования применены");
        }
    }
}

