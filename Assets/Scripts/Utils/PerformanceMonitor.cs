using UnityEngine;
using TMPro;

namespace RemaluxAR.Utils
{
    /// <summary>
    /// Монитор производительности - отображает FPS и другую информацию
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool showFPS = true;
        [SerializeField] private bool showMemory = true;
        [SerializeField] private float updateInterval = 0.5f;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI fpsText;
        [SerializeField] private TextMeshProUGUI memoryText;

        private float accumulatedTime = 0f;
        private int frameCount = 0;
        private float currentFPS = 0f;
        private float lastUpdateTime = 0f;

        private void Update()
        {
            // Подсчёт FPS
            accumulatedTime += Time.unscaledDeltaTime;
            frameCount++;

            if (Time.time - lastUpdateTime >= updateInterval)
            {
                currentFPS = frameCount / accumulatedTime;
                accumulatedTime = 0f;
                frameCount = 0;
                lastUpdateTime = Time.time;

                UpdateDisplay();
            }
        }

        /// <summary>
        /// Обновляет отображаемую информацию
        /// </summary>
        private void UpdateDisplay()
        {
            if (showFPS && fpsText != null)
            {
                Color fpsColor = GetFPSColor(currentFPS);
                fpsText.text = $"FPS: {currentFPS:F0}";
                fpsText.color = fpsColor;
            }

            if (showMemory && memoryText != null)
            {
                long totalMemory = System.GC.GetTotalMemory(false);
                float totalMemoryMB = totalMemory / (1024f * 1024f);
                memoryText.text = $"Memory: {totalMemoryMB:F1} MB";
            }
        }

        /// <summary>
        /// Получает цвет для отображения FPS (зелёный = хорошо, красный = плохо)
        /// </summary>
        private Color GetFPSColor(float fps)
        {
            if (fps >= 50f) return Color.green;
            if (fps >= 30f) return Color.yellow;
            return Color.red;
        }

        /// <summary>
        /// Включает/выключает монитор
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (fpsText != null) fpsText.enabled = enabled;
            if (memoryText != null) memoryText.enabled = enabled;
        }
    }
}

