   # ✅ ML Интеграция - Чеклист

## 📦 Что Уже Готово

- ✅ **iOS CoreML Плагин** - `Assets/Plugins/iOS/CoreMLSegmentation.mm`
- ✅ **Unity ML Manager** - `Assets/Scripts/ML/MLSegmentationManager.cs`
- ✅ **P/Invoke Интеграция** - iOS + Android декларации
- ✅ **Flood Fill Алгоритм** - для "вся стена" режима
- ✅ **Скрипты Конвертации** - ONNX → CoreML/TFLite
- ✅ **Документация** - 5 подробных руководств

---

## 🎯 Что Нужно Сделать (30 минут)

### [ ] Шаг 1: Python Dependencies (2 мин)

```bash
pip install onnx coremltools onnx-coreml torch transformers
```

**Проверка:**
```bash
python -c "import coremltools; print(coremltools.__version__)"
# Должно вывести версию (например: 6.3.0)
```

---

### [ ] Шаг 2: Конвертировать Модель (5 мин)

```bash
cd Assets/ML
python convert_to_coreml.py
```

**Ожидаемый вывод:**
```
🎉 КОНВЕРТАЦИЯ УСПЕШНА!
✅ Модель сохранена: CoreML/SegFormerB0_FP16.mlmodel
📊 Размер: 7.1 MB
```

**Проверка:**
```bash
ls -lh CoreML/SegFormerB0_FP16.mlmodel
# Должен показать файл ~7 MB
```

---

### [ ] Шаг 3: Скопировать Модель (1 мин)

```bash
cp CoreML/SegFormerB0_FP16.mlmodel ../StreamingAssets/SegFormerB0_FP16.mlmodel
```

**Проверка:**
```bash
ls -lh ../StreamingAssets/SegFormerB0_FP16.mlmodel
# Должен показать файл ~7 MB
```

---

### [ ] Шаг 4: Настроить Unity (5 мин)

#### 4.1 Добавить MLSegmentationManager

1. Откройте Unity
2. Найдите `AR Session` в Hierarchy
3. Add Component → `MLSegmentationManager`
4. В Inspector:
   - ✅ **Enable ML Segmentation**
   - `Inference Interval` = 0.2
   - `Inference Resolution` = 512

#### 4.2 Настроить WallDetectionAndPainting

1. На том же `AR Session` найдите `WallDetectionAndPainting`
2. Перетащите `AR Session` в поле **ML Segmentation Manager**
3. ✅ **Fill Whole Wall Mode**

**Детальная инструкция:** [UNITY_SETUP_ML.md](UNITY_SETUP_ML.md)

---

### [ ] Шаг 5: Build & Test (10-15 мин)

#### 5.1 Build iOS

```
File → Build Settings → iOS → Build
```

#### 5.2 Xcode Build

1. Откройте `.xcodeproj`
2. Проверьте: `Data/Raw/SegFormerB0_FP16.mlmodel` присутствует
3. Build: `Cmd+R`

#### 5.3 Тест на iPhone

1. Запустите приложение
2. Наведите камеру на стену (медленно)
3. Кликните на стену
4. **Результат:** Вся стена должна выделиться! ✨

---

## 🔍 Проверка Логов

### Unity Console (при старте)

```
[MLSegmentation] Модель инициализирована: ML/optimum:segformer-b0...
[MLSegmentation] Inference resolution: 512x512
[MLSegmentation] Inference interval: 0.2s (~5 FPS)
[WallDetection] fillWholeWallMode: True
[WallDetection] mlSegmentationManager: установлен ✅
```

### Xcode Console (при запуске на iPhone)

```
[CoreMLSegmentation] Инициализация с моделью: .../SegFormerB0_FP16.mlmodel
[CoreMLSegmentation] ✅ Модель инициализирована успешно!
[CoreMLSegmentation] Model Description: ...
```

### Xcode Console (при клике на стену)

```
[Unity → CoreML] SegmentCurrentFrame called (resolution: 512)
[CoreMLSegmentation] ⚡ Inference time: 45.2 ms
[CoreML → Unity] ✅ Маска скопирована (262144 bytes)
[MLSegmentation] ✅ Flood Fill завершён: 15234 пикселей стены
```

---

## 🐛 Troubleshooting

### ❌ "No module named 'onnx_coreml'"

```bash
pip install --upgrade onnx-coreml coremltools
```

### ❌ "CoreML модель не найдена"

```bash
# Проверьте путь:
ls -la Assets/StreamingAssets/SegFormerB0_FP16.mlmodel

# Если нет - скопируйте:
cp Assets/ML/CoreML/SegFormerB0_FP16.mlmodel Assets/StreamingAssets/
```

### ❌ "ML не инициализирована!"

**Проверьте в Unity Inspector:**
1. ✅ `MLSegmentationManager` добавлен на `AR Session`
2. ✅ `Enable ML Segmentation` = включено
3. ✅ `mlSegmentationManager` присвоен в `WallDetectionAndPainting`
4. ✅ `Fill Whole Wall Mode` = включено

### ❌ ML работает медленно (< 10 FPS)

**Решение 1:** Увеличить интервал
```
Inference Interval = 0.5  (2 FPS inference)
```

**Решение 2:** Уменьшить разрешение
```
Inference Resolution = 256  (быстрее, но менее точно)
```

---

## 📊 Ожидаемая Производительность

### iPhone 13/14 (ваше устройство)

| Параметр | Значение | Статус |
|----------|----------|--------|
| **Inference Time** | 30-50ms | ✅ Отлично |
| **Total Latency** | 50-100ms | ✅ Реальное время |
| **FPS (Inference)** | 5 FPS | ✅ Оптимизировано |
| **FPS (UI)** | 60 FPS | ✅ Плавно |
| **Memory** | +50 MB | ✅ Приемлемо |
| **Battery Impact** | Низкий | ✅ Throttling |
| **Accuracy** | 85-90% | ✅ ADE20K |

---

## 🎉 После Успешного Теста

### Что Дальше?

1. ✅ **Оптимизация параметров:**
   - Попробуйте разные `inferenceInterval`
   - Настройте `inferenceResolution` для баланса

2. ✅ **Визуализация:**
   - Добавьте AR overlay маски сегментации
   - Покажите контуры стены

3. ✅ **Android:**
   - Создайте TFLite плагин (аналогично CoreML)
   - См. `Assets/ML/convert_to_tflite.py`

4. ✅ **UI/UX:**
   - Индикатор загрузки ML
   - Выбор цвета стены
   - Сохранение/загрузка проектов

---

## 📚 Документация

| Документ | Описание |
|----------|----------|
| **[НАЧИНАЕМ_ML.md](НАЧИНАЕМ_ML.md)** | Быстрый старт (5 шагов) |
| **[UNITY_SETUP_ML.md](UNITY_SETUP_ML.md)** | Детальная настройка Unity |
| **[ML_INTEGRATION_COMPLETE.md](ML_INTEGRATION_COMPLETE.md)** | Полное руководство |
| **[Assets/ML/CONVERSION_GUIDE.md](Assets/ML/CONVERSION_GUIDE.md)** | Конвертация моделей |
| **[ИНСТРУКЦИЯ.md](ИНСТРУКЦИЯ.md)** | Общая документация |

---

## 💬 Нужна Помощь?

Если что-то не работает:
1. Проверьте чеклист выше ✅
2. Проверьте логи Unity/Xcode
3. См. секцию Troubleshooting в [ML_INTEGRATION_COMPLETE.md](ML_INTEGRATION_COMPLETE.md)

---

## ✨ Успехов!

После выполнения всех шагов у вас будет **Dulux Visualizer режим**: кликнул → вся стена выделилась! 🎨

**Приступаем! 🚀**


