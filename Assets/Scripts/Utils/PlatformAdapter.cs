using UnityEngine;

namespace RemaluxAR.Utils
{
    /// <summary>
    /// Абстракция платформенных различий между iOS и Android
    /// </summary>
    public static class PlatformAdapter
    {
        /// <summary>
        /// Проверяет, поддерживается ли LiDAR mesh scanning на текущем устройстве
        /// </summary>
        public static bool SupportsMeshScanning
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                // На iOS проверяем через XRMeshSubsystem - правильный способ!
                // Subsystem будет недоступен на устройствах без LiDAR
                try
                {
                    var subsystems = new System.Collections.Generic.List<UnityEngine.XR.XRMeshSubsystem>();
                    UnityEngine.SubsystemManager.GetSubsystems(subsystems);
                    
                    // Если есть хотя бы один активный mesh subsystem - значит LiDAR поддерживается
                    foreach (var subsystem in subsystems)
                    {
                        if (subsystem != null && subsystem.running)
                        {
                            return true;
                        }
                    }
                    
                    return false;
                }
                catch
                {
                    // Если произошла ошибка - LiDAR недоступен
                    return false;
                }
#else
                // На Android нет встроенного mesh scanning
                return false;
#endif
            }
        }

        /// <summary>
        /// Проверяет, поддерживается ли Depth API на текущем устройстве
        /// </summary>
        public static bool SupportsDepth
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var occlusionManager = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.AROcclusionManager>();
                if (occlusionManager != null)
                {
                    var descriptor = occlusionManager.descriptor;
                    return descriptor?.environmentDepthImageSupported == UnityEngine.XR.ARSubsystems.Supported.Supported;
                }
                return false;
#elif UNITY_IOS && !UNITY_EDITOR
                // На iOS с LiDAR также есть depth
                var occlusionManager = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.AROcclusionManager>();
                return occlusionManager != null;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Получает текущую платформу как строку
        /// </summary>
        public static string CurrentPlatform
        {
            get
            {
#if UNITY_IOS
                return "iOS";
#elif UNITY_ANDROID
                return "Android";
#else
                return "Editor";
#endif
            }
        }

        /// <summary>
        /// Проверяет, запущено ли приложение в редакторе
        /// </summary>
        public static bool IsEditor => Application.isEditor;

        /// <summary>
        /// Логирует информацию о возможностях устройства
        /// </summary>
        public static void LogDeviceCapabilities()
        {
            Debug.Log($"[PlatformAdapter] Platform: {CurrentPlatform}");
            Debug.Log($"[PlatformAdapter] Mesh Scanning Supported: {SupportsMeshScanning}");
            Debug.Log($"[PlatformAdapter] Depth API Supported: {SupportsDepth}");
            Debug.Log($"[PlatformAdapter] Is Editor: {IsEditor}");
        }

        /// <summary>
        /// Получает рекомендуемое количество raycast точек для текущей платформы
        /// </summary>
        public static int GetRecommendedRaycastCount()
        {
#if UNITY_IOS
            return SupportsMeshScanning ? 5 : 3; // Больше raycast'ов если есть mesh
#else
            return 3; // На Android ограничиваемся плоскостями
#endif
        }

        /// <summary>
        /// Получает максимальное рекомендуемое количество активных paint strokes
        /// </summary>
        public static int GetMaxPaintStrokes()
        {
            // Ограничиваем для производительности
            return 1000;
        }

        /// <summary>
        /// Получает рекомендуемый throttle интервал для рисования (в секундах)
        /// </summary>
        public static float GetDrawingThrottleInterval()
        {
            return 0.05f; // 20 точек в секунду
        }
    }
}

