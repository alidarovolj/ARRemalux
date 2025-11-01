# 🎉 ML Интеграция - Полное Руководство

## ✅ Что Уже Сделано

### 1. iOS CoreML Плагин
- ✅ `Assets/Plugins/iOS/CoreMLSegmentation.mm` - Native Objective-C++ плагин
- ✅ Поддержка Vision framework для CoreML inference
- ✅ Автоматическая обработка AR camera frames
- ✅ P/Invoke интеграция с Unity

### 2. Unity ML Manager
- ✅ `Assets/Scripts/ML/MLSegmentationManager.cs` - Полностью интегрирован
- ✅ P/Invoke декларации для iOS/Android
- ✅ Flood Fill алгоритм для "вся стена"
- ✅ ADE20K классы (wall, door, person, furniture)
- ✅ Performance throttling и статистика

### 3. Скрипты Конвертации
- ✅ `Assets/ML/convert_to_coreml.py` - ONNX → CoreML
- ✅ `Assets/ML/convert_to_tflite.py` - ONNX → TFLite
- ✅ FP16 quantization для оптимизации размера

---

## 🚀 Что Нужно Сделать СЕЙЧАС

### Шаг 1: Установить Python зависимости

```bash
pip install onnx coremltools onnx-coreml torch transformers
```

### Шаг 2: Конвертировать ONNX → CoreML

```bash
cd Assets/ML
python convert_to_coreml.py
```

**Результат:** `Assets/ML/CoreML/SegFormerB0_FP16.mlmodel` (~7 MB)

### Шаг 3: Скопировать модель в StreamingAssets

```bash
cp CoreML/SegFormerB0_FP16.mlmodel ../StreamingAssets/SegFormerB0_FP16.mlmodel
```

### Шаг 4: Настроить Unity

1. Откройте Unity
2. Найдите `AR Session` GameObject в сцене
3. Добавьте компонент `MLSegmentationManager` (если его нет)
4. В Inspector:
   - ✅ **Enable ML Segmentation** = включить
   - `Inference Interval` = 0.2 (5 FPS inference)
   - `Inference Resolution` = 512

### Шаг 5: Настроить WallDetectionAndPainting

1. Найдите `AR Session` GameObject
2. Найдите компонент `WallDetectionAndPainting`
3. В Inspector:
   - ✅ Присвойте `MLSegmentationManager` в поле `mlSegmentationManager`
   - ✅ **Fill Whole Wall Mode** = включить

### Шаг 6: Build & Test на iPhone

```
File → Build Settings → iOS → Build
```

В Xcode:
1. Убедитесь, что `SegFormerB0_FP16.mlmodel` в `Data/Raw/` папке
2. Build и запустите на iPhone

---

## 🎯 Как Это Работает

### Режим "Вся Стена" (Dulux-Style)

1. **Пользователь нажимает на стену**
2. `WallDetectionAndPainting.TryPaintWholeWall()` вызывается
3. `MLSegmentationManager.IsWall()` проверяет: это стена?
4. `MLSegmentationManager.FloodFillWall()` находит все пиксели этой стены
5. ✅ **Вся стена выделяется** (как в Dulux Visualizer!)

### ML Pipeline

```
AR Camera Frame (1920×1080)
    ↓
Resize to 512×512
    ↓
CoreML Inference (~50ms)
    ↓
Segmentation Mask (512×512 pixels, каждый = class ID)
    ↓
Flood Fill Algorithm
    ↓
Выделена вся стена! 🎨
```

### ADE20K Классы

```csharp
WALL_CLASS = 0      // ✅ Стена (красим!)
DOOR_CLASS = 14     // ❌ Дверь (игнорируем)
FLOOR_CLASS = 3     // ❌ Пол (игнорируем)
PERSON_CLASS = 12   // ❌ Человек (игнорируем)
SOFA_CLASS = 23     // ❌ Диван (игнорируем)
TV_CLASS = 89       // ❌ Телевизор (игнорируем)
```

---

## 📊 Ожидаемая Производительность

### iPhone 13/14 (без LiDAR)

- **CoreML Inference:** ~30-50ms (20-30 FPS)
- **Flood Fill:** ~5-10ms
- **Total Latency:** ~50-100ms ✅ **Реальное время!**
- **Memory:** ~50 MB (модель + buffers)

### Throttling

По умолчанию inference запускается **5 раз в секунду** (`inferenceInterval = 0.2s`):
- Экономия батареи ✅
- Стабильная производительность ✅
- Достаточно для Dulux UX ✅

---

## 🐛 Troubleshooting

### Ошибка: "CoreML модель не найдена"

**Причина:** Модель не скопирована в StreamingAssets

**Решение:**
```bash
cp Assets/ML/CoreML/SegFormerB0_FP16.mlmodel Assets/StreamingAssets/
```

### Ошибка: "CoreML initialization failed"

**Причина:** Модель не скомпилирована для iOS

**Решение:**
```bash
xcrun coremlcompiler compile SegFormerB0_FP16.mlmodel .
```

### ML не активируется

**Проверьте:**
1. ✅ `enableMLSegmentation = true` в Inspector
2. ✅ `mlSegmentationManager` присвоен в `WallDetectionAndPainting`
3. ✅ `fillWholeWallMode = true` в `WallDetectionAndPainting`
4. ✅ Модель в `Assets/StreamingAssets/`

### Низкая производительность (< 10 FPS)

**Решение 1:** Увеличить `inferenceInterval`:
```csharp
inferenceInterval = 0.5f; // 2 FPS inference (экономия батареи)
```

**Решение 2:** Уменьшить `inferenceResolution`:
```csharp
inferenceResolution = 256; // Быстрее, но менее точно
```

---

## 📈 Следующие Улучшения

### Фаза 1 (Текущая) ✅
- ✅ CoreML плагин
- ✅ P/Invoke интеграция
- ✅ Flood Fill алгоритм
- ✅ Базовое обнаружение стен

### Фаза 2 (1-2 недели)
- 🔲 Android TFLite плагин
- 🔲 Оптимизация inference (batch processing)
- 🔲 Улучшенная визуализация (AR overlay маски)
- 🔲 Сохранение/загрузка проектов

### Фаза 3 (1 месяц)
- 🔲 Интеграция с Depth API для non-LiDAR устройств
- 🔲 Temporal filtering (сглаживание между кадрами)
- 🔲 Multi-wall painting (несколько стен одновременно)
- 🔲 Custom ML модели (fine-tuning на ваших данных)

---

## 📚 Дополнительные Ресурсы

### Документация
- `Assets/ML/CONVERSION_GUIDE.md` - Детальная конвертация моделей
- `Assets/ML/README_ML_INTEGRATION.md` - Техническая спецификация
- `ИНСТРУКЦИЯ.md` - Общая документация проекта

### Код
- `Assets/Plugins/iOS/CoreMLSegmentation.mm` - iOS native плагин
- `Assets/Scripts/ML/MLSegmentationManager.cs` - Unity ML manager
- `Assets/Scripts/AR/WallDetectionAndPainting.cs` - ML интеграция

### Модели
- HuggingFace: [segformer-b0-finetuned-ade-512-512](https://huggingface.co/nvidia/segformer-b0-finetuned-ade-512-512)
- ADE20K Dataset: [Classes List](https://groups.csail.mit.edu/vision/datasets/ADE20K/)

---

## 💬 Вопросы?

Если возникли проблемы:
1. Проверьте логи Unity: `[MLSegmentation]` prefix
2. Проверьте логи Xcode: `[CoreMLSegmentation]` prefix
3. Убедитесь что модель в `StreamingAssets/`
4. Проверьте что `enableMLSegmentation = true`

**Удачи! 🚀**


