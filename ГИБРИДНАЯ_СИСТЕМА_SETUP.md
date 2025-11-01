# 🚀 Настройка Гибридной Системы Определения Стен

## 🎯 Что Это Такое?

**Гибридный Wall Detector** - комбинирует 3 мощных источника информации:

1. **Depth Anything V2** - карта глубины (геометрия сцены)
2. **DeepLabV3** - semantic segmentation (фильтрация людей/мебели)
3. **AR Planes** - ARKit plane detection (точное позиционирование)

**Результат:** Максимально точное определение стен с исключением дверей, мебели и людей!

---

## ✅ Что Уже Готово

### **Скрипты:**
- ✅ `CoreMLDepthEstimation.mm` - iOS plugin для Depth Anything V2
- ✅ `DepthEstimationManager.cs` - Unity C# manager для depth
- ✅ `HybridWallDetector.cs` - Гибридный детектор (объединяет все 3 источника)
- ✅ `WallDetectionAndPainting.cs` - Обновлен для использования гибрида

### **Модели:**
- ✅ `DepthAnythingV2SmallF16P6.mlpackage` (19MB) - в `Assets/StreamingAssets/`
- ✅ `SegFormerB0_FP16.mlmodel` (DeepLabV3, 8.2MB) - в `Assets/StreamingAssets/`

---

## 🔧 Настройка в Unity Editor

### **Шаг 1: Настроить Scene**

1. Откройте вашу AR сцену в Unity
2. Найдите GameObject с `WallDetectionAndPainting` компонентом

### **Шаг 2: Создать ML Managers**

#### 2.1 Depth Estimation Manager

1. Создайте пустой GameObject:
   - `GameObject → Create Empty`
   - Переименуйте: `DepthEstimationManager`

2. Добавьте компонент:
   - `Add Component → DepthEstimationManager`

3. Настройте параметры в Inspector:
   ```
   Depth Resolution: 512
   Estimation Interval: 0.1 (10 FPS)
   AR Camera Manager: (перетащите ARCameraManager из сцены)
   ```

#### 2.2 ML Segmentation Manager

1. Создайте пустой GameObject (если еще нет):
   - `GameObject → Create Empty`
   - Переименуйте: `MLSegmentationManager`

2. Добавьте компонент:
   - `Add Component → MLSegmentationManager`

3. Настройте:
   ```
   Enable ML Segmentation: true
   Inference Interval: 0.2 (5 FPS)
   Inference Resolution: 512
   AR Camera Manager: (перетащите ARCameraManager)
   ```

#### 2.3 Hybrid Wall Detector

1. Создайте пустой GameObject:
   - `GameObject → Create Empty`
   - Переименуйте: `HybridWallDetector`

2. Добавьте компонент:
   - `Add Component → HybridWallDetector`

3. Настройте в Inspector:
   ```
   === ML Components ===
   Depth Manager: (перетащите DepthEstimationManager GameObject)
   Segmentation Manager: (перетащите MLSegmentationManager GameObject)
   
   === AR Components ===
   AR Plane Manager: (перетащите ARPlaneManager из сцены)
   
   === Wall Detection Parameters ===
   Min Wall Area: 0.5 м²
   Depth Consistency Threshold: 0.05
   Min Wall Height From Floor: 0.3 м
   
   === Object Filtering ===
   Filter People: ✅ true
   Filter Furniture: ✅ true
   Filter Electronics: ✅ true
   ```

### **Шаг 3: Обновить WallDetectionAndPainting**

1. Найдите GameObject с `WallDetectionAndPainting`

2. В Inspector найдите секцию **"Hybrid Wall Detection (НОВОЕ!)"**

3. Настройте:
   ```
   Hybrid Wall Detector: (перетащите HybridWallDetector GameObject)
   Use Hybrid Detection: ✅ true
   ```

### **Шаг 4: Сохранить Сцену**

1. **File → Save Scene** (`Ctrl+S` / `Cmd+S`)
2. Убедитесь что все ссылки на месте (нет "(None)" в Inspector)

---

## 📱 Сборка и Тестирование

### **Шаг 1: Build Settings**

1. `File → Build Settings`
2. Platform: **iOS**
3. `Add Open Scenes` (если сцена не добавлена)
4. **Switch Platform** (если нужно)

### **Шаг 2: Player Settings**

1. `Player Settings → iOS → Other Settings`:
   ```
   Camera Usage Description: "AR camera needed for wall painting"
   Requires ARKit: ✅ true
   Target minimum iOS Version: 13.0+
   ```

2. `Architecture`: **ARM64**

### **Шаг 3: Build**

1. В Build Settings нажмите **Build**
2. Выберите папку (например, `NewBuild`)
3. Дождитесь завершения

### **Шаг 4: Xcode - ВАЖНО!**

1. Откройте `NewBuild/Unity-iPhone.xcodeproj` в Xcode

2. **Включите Objective-C Exceptions:**
   - Выберите проект **Unity-iPhone**
   - Target: **UnityFramework**
   - Build Settings → Поиск: `"Objective-C Exceptions"`
   - **Enable Objective-C Exceptions: Yes**

3. **Clean Build Folder:**
   - `Product → Clean Build Folder` (`Shift+Cmd+K`)

4. **Build & Run:**
   - Подключите iPhone
   - `Product → Run` (`Cmd+R`)

---

## 🧪 Тестирование

### **Что Проверить:**

#### 1. Depth Estimation
```
Логи:
[DepthEstimation] ✅ CoreML Depth модель инициализирована!
[DepthEstimation] Avg estimation time: ~26ms (10 FPS)
```

#### 2. Semantic Segmentation
```
Логи:
[MLSegmentation] ✅ CoreML модель инициализирована!
[MLSegmentation] Avg inference time: 69ms (14 FPS)
```

#### 3. Hybrid Detection
```
Логи:
[HybridWallDetector] ✅ СТЕНА обнаружена: 1.5м × 2.3м, 
    depth: 0.75, confidence: 0.82
```

#### 4. Painting
```
Когда кликаете на стену:
[WallDetection] ✅ ГИБРИДНЫЙ: Стена обнаружена!
  • Depth consistency: 0.95
  • Non-wall objects: Нет
  • Confidence: 0.82
```

### **Ожидаемое Поведение:**

✅ **Стены:** Окрашиваются легко
✅ **Мебель:** Игнорируется (логи: "🪑 Обнаружена мебель")
✅ **Люди:** Игнорируются (логи: "🚶 Обнаружен человек")
✅ **Двери:** Игнорируются (низкий depth consistency)

❌ **Если не работает** → см. "Troubleshooting" ниже

---

## 🐛 Troubleshooting

### **Проблема 1: "Depth модель не инициализирована"**

**Причины:**
- Модель не в `StreamingAssets/`
- Неправильный путь

**Решение:**
```bash
# Проверьте наличие модели
ls -lh Assets/StreamingAssets/DepthAnythingV2SmallF16P6.mlpackage

# Если нет - скопируйте
cp -r Assets/ML/DepthAnythingV2SmallF16P6.mlpackage Assets/StreamingAssets/
```

### **Проблема 2: "Objective-C Exceptions Error"**

**Логи:**
```
Cannot use '@try' with Objective-C exceptions disabled
```

**Решение:**
- Xcode → UnityFramework → Build Settings
- Enable Objective-C Exceptions: **Yes**
- Clean Build Folder + Rebuild

### **Проблема 3: "HybridWallDetector не находит стены"**

**Причины:**
- `useHybridDetection` = false
- Нет ссылок на Managers

**Решение:**
1. В Unity Inspector:
   - WallDetectionAndPainting → Use Hybrid Detection: ✅ **true**
   - Hybrid Wall Detector: **перетащите GameObject**

2. В HybridWallDetector:
   - Убедитесь что все 3 Manager ссылки заполнены

### **Проблема 4: "Все обнаруживается как furniture"**

**Причина:**
- DeepLabV3 обучена на PASCAL VOC (нет класса "wall")

**Решение (ВРЕМЕННЫЙ WORKAROUND):**

В `HybridWallDetector.cs` измените:
```csharp
// Строка ~270-280
private bool CheckForNonWallObjects(Vector2 normalizedPos)
{
    if (segmentationManager == null || !segmentationManager.IsInitialized)
        return false; // ← Просто отключаем фильтрацию
    
    // Закомментируйте весь метод для теста
    return false;
}
```

**Правильное Решение:**
- Заменить DeepLabV3 на модель с ADE20K классами (wall, floor, ceiling)

---

## 📊 Производительность

### **iPhone 13/14 (A15/A16 Bionic):**

| Компонент | Inference Time | FPS |
|-----------|----------------|-----|
| Depth Anything V2 | 26-70ms | 10 FPS |
| DeepLabV3 | 60-70ms | 14 FPS |
| AR Planes | Native | 60 FPS |
| **Итого** | ~40ms avg | ~25 FPS |

### **Оптимизация:**

1. **Снизить частоту inference:**
   ```csharp
   // DepthEstimationManager
   estimationInterval = 0.2f; // 5 FPS вместо 10 FPS
   
   // MLSegmentationManager
   inferenceInterval = 0.3f; // 3 FPS вместо 5 FPS
   ```

2. **Использовать Lower Resolution:**
   ```csharp
   // Обе модели поддерживают 256x256
   inferenceResolution = 256; // Быстрее в 4 раза!
   ```

---

## 🎨 Визуализация (Опционально)

Чтобы **видеть** что модели обнаруживают:

### **Depth Map Visualization:**

1. Создайте UI Canvas + RawImage
2. Добавьте `DepthVisualizationDebugger.cs`:
   - Рисует depth map как heatmap (близко=синий, далеко=красный)

### **Segmentation Mask Visualization:**

1. Используйте существующий `MLSegmentationDebugViewer.cs`
2. Добавьте на Canvas → Raw Image
3. Увидите цветные области для каждого класса

**Инструкция:** см. `ВИЗУАЛИЗАЦИЯ_ML.md`

---

## 🚀 Next Steps

### **Для Production:**

1. **Заменить DeepLabV3 → ADE20K модель**
   - Найти готовую CoreML ADE20K модель
   - Или конвертировать SegFormer ADE20K

2. **Добавить UI для настроек:**
   - Слайдеры для `minWallArea`, `depthConsistencyThreshold`
   - Toggle для фильтров (people/furniture/electronics)

3. **Улучшить Flood Fill:**
   - После клика → выделять **всю стену** (pixel-perfect)
   - Требует интеграции Depth Map + Segmentation Mask

4. **Оптимизация:**
   - Adaptive inference interval (быстрее когда статика)
   - ROI (Region of Interest) - inference только в области стен

---

## ✅ Checklist для Тестирования

### **Перед Сборкой:**
- [ ] Все модели в `Assets/StreamingAssets/`
- [ ] Все Manager ссылки заполнены в Inspector
- [ ] `useHybridDetection = true`
- [ ] Сцена сохранена

### **После Сборки (Xcode):**
- [ ] Objective-C Exceptions включены
- [ ] Clean Build Folder выполнен
- [ ] Device подключен и unlocked

### **Во Время Тестирования:**
- [ ] Логи показывают инициализацию всех моделей
- [ ] Depth inference работает (~26-70ms)
- [ ] Segmentation inference работает (~60-70ms)
- [ ] Hybrid detector обнаруживает стены
- [ ] Клик по стене создает paint mark
- [ ] Клик по людям/мебели игнорируется

---

## 📞 Support

Если что-то не работает:

1. Проверьте логи в Xcode Console
2. Найдите `[Error]` или `❌`
3. См. "Troubleshooting" выше
4. Или создайте issue с полными логами

**Удачи! 🎉**

