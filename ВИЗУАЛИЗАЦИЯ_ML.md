# 🎨 Визуализация ML Segmentation - Инструкция

## 🚨 КРИТИЧЕСКАЯ ПРОБЛЕМА!

**DeepLabV3 НЕ ПОДХОДИТ для определения стен!**

Модель обучена на PASCAL VOC 2012 (уличные сцены):
```
Классы: aeroplane, bicycle, bird, boat, bottle, bus, car, cat, chair, 
cow, diningTable, dog, horse, motorbike, person, pottedPlant, sheep, 
sofa, train, tvOrMonitor
```

**❌ Класс "wall" ОТСУТСТВУЕТ!**

---

## 🎯 Что Делать СЕЙЧАС

### **1. Добавить Визуализацию (чтобы увидеть что модель видит)**

#### В Unity Editor:

1. **Создайте UI Canvas:**
   - `GameObject → UI → Canvas`
   - Canvas Scaler → UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1920 x 1080**

2. **Создайте RawImage для визуализации:**
   - Правой кнопкой на Canvas → `UI → Raw Image`
   - Переименуйте: `MLSegmentationOverlay`
   - RectTransform:
     - Anchor Presets: **Stretch** (Alt+Click на правый нижний квадрат)
     - Left: 0, Top: 0, Right: 0, Bottom: 0
   - Color: Белый (R:255, G:255, B:255, **A:128** для прозрачности)

3. **Добавьте MLSegmentationDebugViewer:**
   - На объект `MLSegmentationOverlay` добавьте компонент:
     - **Add Component → MLSegmentationDebugViewer**
   - В инспекторе:
     - ML Manager: перетащите ваш `MLSegmentationManager` GameObject
     - Overlay Alpha: **0.5** (50% прозрачность)
     - Show Only Walls: **false** (показываем всё для отладки)

4. **Сохраните сцену и пересоберите!**

---

### **2. Запустите и Посмотрите Что Модель Видит**

После пересборки:
1. Наведите камеру на **людей, мебель, технику**
2. Вы увидите **цветной overlay** на этих объектах
3. **Стены будут невидимы** (так как DeepLabV3 их не знает!)

---

## 🔍 Отладка

### **Посмотрите статистику классов:**

В Unity Editor, когда приложение запущено:
1. Выберите `MLSegmentationOverlay` в Hierarchy
2. В Inspector найдите `MLSegmentationDebugViewer`
3. Нажмите правую кнопку на названии скрипта → **Show Class Statistics**
4. В Console увидите что модель обнаруживает (person, chair, sofa и т.д.)

---

## ✅ Правильная Модель: SegFormer-B0 ADE20K

**Нужна модель обученная на ADE20K Indoor Scenes!**

### Классы ADE20K (есть wall!):
```
0: wall ✅
1: building
2: sky
3: floor ✅
4: tree
5: ceiling ✅
12: person ✅
14: door ✅
19: chair
23: sofa
```

### Где Взять CoreML Модель?

**Проблема:** Готовой CoreML модели SegFormer-B0 ADE20K нет на HuggingFace.

**Решения:**

#### Вариант A: Конвертировать Самостоятельно (сложно)

1. Скачать PyTorch модель: `huggingface.co/nvidia/segformer-b0-finetuned-ade-512-512`
2. Экспортировать в ONNX
3. Конвертировать ONNX → CoreML (через `coremltools`)
4. **Проблема:** Python dependency hell (уже пробовали!)

#### Вариант B: Использовать Готовую CoreML Модель Другой Архитектуры

**Рекомендация:** DeepLabV3 ResNet50 **trained on ADE20K** (не PASCAL VOC!)

Попробуйте найти на:
- Apple's ML Models Gallery: `developer.apple.com/machine-learning/models`
- CoreML Models Zoo: `github.com/john-rocky/CoreML-Models`

#### Вариант C: Упрощенный Подход (быстрый старт)

Использовать **геометрическую фильтрацию** + Depth API (без ML):
1. Depth API → карта глубины
2. Plane detection → стены
3. Фильтры по высоте/размеру/aspect ratio
4. **Проще и быстрее**, но менее точно

---

## 📝 Следующие Шаги

### **Для Демо (Сейчас):**

1. ✅ Добавьте визуализацию (см. выше)
2. ✅ Запустите и убедитесь что DeepLabV3 видит людей/мебель
3. ⚠️ **Временно:** Можно считать что "background" (класс 0) = стена
   - Это **неправильно** но даст какой-то результат для теста

### **Для Production:**

1. ❌ Заменить DeepLabV3 PASCAL VOC
2. ✅ Найти/конвертировать модель с ADE20K классами
3. ✅ Или использовать Depth API + геометрию

---

## 🎨 Цвета Классов (DeepLabV3 PASCAL VOC)

Если увидите эти цвета на экране:

| Цвет | Класс |
|------|-------|
| Красный | person ❤️ |
| Зеленый | chair 🪑 |
| Синий | sofa 🛋️ |
| Желтый | car 🚗 |
| Пурпурный | bottle 🍾 |

**Стены = не будут видны!** (класс отсутствует)

---

## 🆘 Если Вообще Ничего Не Видно

1. Проверьте что `MLSegmentationManager.enableMLSegmentation = true`
2. Проверьте что Canvas → Render Mode = **Screen Space - Overlay**
3. Проверьте что RawImage находится **поверх AR Camera**
4. Проверьте логи: `[CoreMLSegmentation] ⚡ Inference time: XXms`

---

## 💡 Временный Workaround

Если нужно **быстро протестировать**:

В `MLSegmentationManager.cs` измените:
```csharp
private const int WALL_CLASS = 0; // background → временно считаем стеной!
```

Это неправильно, но покажет **что-то** на экране для теста интеграции.

