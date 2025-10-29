using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RemaluxAR.AR;
using RemaluxAR.Drawing;

namespace RemaluxAR.UI
{
    /// <summary>
    /// Управляет UI приложения - палитра цветов, настройки кисти, кнопки управления
    /// </summary>
    public class UIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARManager arManager;
        [SerializeField] private DrawingManager drawingManager;
        [SerializeField] private MeshManager meshManager;

        [Header("UI Elements - Color Picker")]
        [SerializeField] private GameObject colorPickerPanel;
        [SerializeField] private Button[] colorButtons;
        [SerializeField] private Image currentColorIndicator;

        [Header("UI Elements - Brush Settings")]
        [SerializeField] private Slider brushThicknessSlider;
        [SerializeField] private TextMeshProUGUI brushThicknessText;

        [Header("UI Elements - Controls")]
        [SerializeField] private Button clearAllButton;
        [SerializeField] private Toggle meshVisibilityToggle;
        [SerializeField] private Toggle planeVisibilityToggle;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Color Palette")]
        [SerializeField] private Color[] colorPalette = new Color[]
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan,
            Color.white,
            Color.black
        };

        private void Awake()
        {
            // Auto-find компоненты
            if (arManager == null) arManager = FindObjectOfType<ARManager>();
            if (drawingManager == null) drawingManager = FindObjectOfType<DrawingManager>();
            if (meshManager == null) meshManager = FindObjectOfType<MeshManager>();
        }

        private void Start()
        {
            InitializeUI();
            SetupEventListeners();
        }

        /// <summary>
        /// Инициализирует UI элементы
        /// </summary>
        private void InitializeUI()
        {
            // Настраиваем кнопки цветов
            if (colorButtons != null && colorButtons.Length > 0)
            {
                for (int i = 0; i < colorButtons.Length && i < colorPalette.Length; i++)
                {
                    int index = i; // Замыкание для lambda
                    Color color = colorPalette[i];

                    // Устанавливаем цвет кнопки
                    Image buttonImage = colorButtons[i].GetComponent<Image>();
                    if (buttonImage != null)
                    {
                        buttonImage.color = color;
                    }

                    // Добавляем обработчик
                    colorButtons[i].onClick.AddListener(() => SelectColor(color));
                }

                // Выбираем первый цвет по умолчанию
                SelectColor(colorPalette[0]);
            }

            // Настраиваем слайдер толщины
            if (brushThicknessSlider != null)
            {
                brushThicknessSlider.minValue = 0.005f; // 0.5 см
                brushThicknessSlider.maxValue = 0.1f;   // 10 см
                brushThicknessSlider.value = 0.02f;     // 2 см по умолчанию
                brushThicknessSlider.onValueChanged.AddListener(OnBrushThicknessChanged);
                UpdateBrushThicknessText(brushThicknessSlider.value);
            }

            // Настраиваем кнопку очистки
            if (clearAllButton != null)
            {
                clearAllButton.onClick.AddListener(OnClearAllClicked);
            }

            // Настраиваем toggles
            if (meshVisibilityToggle != null)
            {
                meshVisibilityToggle.isOn = true;
                meshVisibilityToggle.onValueChanged.AddListener(OnMeshVisibilityToggled);
            }

            if (planeVisibilityToggle != null)
            {
                planeVisibilityToggle.isOn = true;
                planeVisibilityToggle.onValueChanged.AddListener(OnPlaneVisibilityToggled);
            }

            UpdateStatusText();
        }

        /// <summary>
        /// Настраивает подписки на события
        /// </summary>
        private void SetupEventListeners()
        {
            if (arManager != null)
            {
                arManager.OnARSessionInitialized += OnARSessionReady;
                arManager.OnARSessionFailed += OnARSessionFailed;
            }

            if (drawingManager != null)
            {
                drawingManager.OnStrokeStarted += OnStrokeStarted;
                drawingManager.OnStrokeEnded += OnStrokeEnded;
                drawingManager.OnAllStrokesCleared += OnStrokesCleared;
            }
        }

        private void Update()
        {
            UpdateStatusText();
        }

        /// <summary>
        /// Выбирает цвет для рисования
        /// </summary>
        public void SelectColor(Color color)
        {
            if (drawingManager != null)
            {
                drawingManager.CurrentColor = color;
            }

            if (currentColorIndicator != null)
            {
                currentColorIndicator.color = color;
            }

            Debug.Log($"[UIController] Color selected: {color}");
        }

        /// <summary>
        /// Обработчик изменения толщины кисти
        /// </summary>
        private void OnBrushThicknessChanged(float value)
        {
            if (drawingManager != null)
            {
                drawingManager.BrushThickness = value;
            }

            UpdateBrushThicknessText(value);
        }

        /// <summary>
        /// Обновляет текст толщины кисти
        /// </summary>
        private void UpdateBrushThicknessText(float value)
        {
            if (brushThicknessText != null)
            {
                // Переводим в сантиметры для удобства
                float cm = value * 100f;
                brushThicknessText.text = $"{cm:F1} см";
            }
        }

        /// <summary>
        /// Обработчик кнопки "Очистить всё"
        /// </summary>
        private void OnClearAllClicked()
        {
            if (drawingManager != null)
            {
                drawingManager.ClearAllStrokes();
            }
        }

        /// <summary>
        /// Обработчик переключения видимости mesh
        /// </summary>
        private void OnMeshVisibilityToggled(bool visible)
        {
            if (meshManager != null)
            {
                meshManager.SetMeshesVisible(visible);
            }
        }

        /// <summary>
        /// Обработчик переключения видимости плоскостей
        /// </summary>
        private void OnPlaneVisibilityToggled(bool visible)
        {
            if (arManager != null)
            {
                arManager.SetPlanesVisible(visible);
            }
        }

        /// <summary>
        /// Обновляет текст статуса
        /// </summary>
        private void UpdateStatusText()
        {
            if (statusText == null) return;

            string status = "";

            if (arManager != null)
            {
                status = arManager.GetARStatus();
            }

            if (drawingManager != null)
            {
                status += $"\nStrokes: {drawingManager.StrokeCount}";
            }

            if (meshManager != null)
            {
                status += $"\nMeshes: {meshManager.MeshCount}";
            }

            statusText.text = status;
        }

        /// <summary>
        /// Переключает видимость палитры цветов
        /// </summary>
        public void ToggleColorPicker()
        {
            if (colorPickerPanel != null)
            {
                colorPickerPanel.SetActive(!colorPickerPanel.activeSelf);
            }
        }

        // Event handlers
        private void OnARSessionReady()
        {
            Debug.Log("[UIController] AR Session is ready");
        }

        private void OnARSessionFailed()
        {
            Debug.LogError("[UIController] AR Session failed");
            if (statusText != null)
            {
                statusText.text = "AR Session Failed!\nCheck device compatibility.";
                statusText.color = Color.red;
            }
        }

        private void OnStrokeStarted(RemaluxAR.Data.PaintStroke stroke)
        {
            Debug.Log("[UIController] Stroke started");
        }

        private void OnStrokeEnded(RemaluxAR.Data.PaintStroke stroke)
        {
            Debug.Log($"[UIController] Stroke ended with {stroke.PointCount} points");
        }

        private void OnStrokesCleared()
        {
            Debug.Log("[UIController] All strokes cleared");
        }

        private void OnDestroy()
        {
            // Отписываемся от событий
            if (arManager != null)
            {
                arManager.OnARSessionInitialized -= OnARSessionReady;
                arManager.OnARSessionFailed -= OnARSessionFailed;
            }

            if (drawingManager != null)
            {
                drawingManager.OnStrokeStarted -= OnStrokeStarted;
                drawingManager.OnStrokeEnded -= OnStrokeEnded;
                drawingManager.OnAllStrokesCleared -= OnStrokesCleared;
            }
        }
    }
}

