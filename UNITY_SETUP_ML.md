# 🎮 Настройка Unity для ML (Пошаговая)

## Предварительные Требования

- ✅ Модель конвертирована: `Assets/StreamingAssets/SegFormerB0_FP16.mlmodel`
- ✅ Unity проект открыт
- ✅ AR Scene настроена (см. ИНСТРУКЦИЯ.md)

---

## Шаг 1: Добавить MLSegmentationManager

### 1.1 Найти AR Session GameObject

В Hierarchy:
```
AR Session
  └─ XR Origin
      └─ Main Camera
```

### 1.2 Добавить Компонент

1. Выберите `AR Session` в Hierarchy
2. В Inspector кликните **Add Component**
3. Найдите `MLSegmentationManager`
4. Кликните на него

### 1.3 Настроить Параметры

В Inspector найдите **MLSegmentationManager** и установите:

```
┌─────────────────────────────────────────────┐
│ MLSegmentationManager (Script)              │
├─────────────────────────────────────────────┤
│ ML Model Settings                           │
│   Model Path: ML/optimum:segformer-b0...    │
│   ✅ Enable ML Segmentation                 │
│                                             │
│ Performance Settings                        │
│   Inference Interval: 0.2                   │
│   Inference Resolution: 512                 │
│                                             │
│ References                                  │
│   AR Camera Manager: (автоматически)        │
└─────────────────────────────────────────────┘
```

**Важные параметры:**
- ✅ **Enable ML Segmentation** - ОБЯЗАТЕЛЬНО включить!
- `Inference Interval = 0.2` - ML запускается 5 раз/сек (экономия батареи)
- `Inference Resolution = 512` - размер обработки (512×512 пикселей)

---

## Шаг 2: Настроить WallDetectionAndPainting

### 2.1 Найти Компонент

На том же `AR Session` GameObject должен быть компонент `WallDetectionAndPainting`.

Если его нет:
1. Click **Add Component**
2. Найдите `WallDetectionAndPainting`
3. Добавьте

### 2.2 Связать ML Manager

В Inspector найдите **WallDetectionAndPainting**:

```
┌─────────────────────────────────────────────┐
│ WallDetectionAndPainting (Script)           │
├─────────────────────────────────────────────┤
│ AR Components                               │
│   AR Plane Manager: (автоматически)         │
│   AR Raycast Manager: (автоматически)       │
│                                             │
│ ML Integration                              │
│   ML Segmentation Manager: [ПЕРЕТАЩИТЕ]    │ ← ВАЖНО!
│   ✅ Fill Whole Wall Mode                   │ ← Включить!
│                                             │
│ Painting Settings                           │
│   Paint Mark Prefab: ...                    │
│   Default Paint Color: Red                  │
│                                             │
│ Plane Filtering                             │
│   Min Wall Area: 0.3                        │
│   Min Wall Height: 0.4                      │
│   Min Aspect Ratio: 0.5                     │
│   ...                                       │
└─────────────────────────────────────────────┘
```

### 2.3 Перетащить ML Manager

1. В поле **ML Segmentation Manager** кликните на кружок справа (⊙)
2. Выберите `MLSegmentationManager` из списка
3. ИЛИ: перетащите `AR Session` GameObject из Hierarchy прямо в это поле

### 2.4 Включить Dulux-режим

✅ **Fill Whole Wall Mode** - включите галочку!

Это активирует режим "кликнул → вся стена выделилась".

---

## Шаг 3: Проверка Настроек

### 3.1 AR Session

Убедитесь что на `AR Session` есть:
- ✅ `ARSession` компонент
- ✅ `ARInputManager` компонент
- ✅ `MLSegmentationManager` компонент ← **НОВОЕ!**
- ✅ `WallDetectionAndPainting` компонент

### 3.2 XR Origin

Убедитесь что на `XR Origin` есть:
- ✅ `XROrigin` компонент
- ✅ `ARPlaneManager` компонент
- ✅ `ARRaycastManager` компонент
- ✅ `ARCameraManager` компонент

### 3.3 StreamingAssets

Проверьте что файл существует:
```
Assets/StreamingAssets/SegFormerB0_FP16.mlmodel
```

В Unity Project window:
```
Assets
└─ StreamingAssets
    └─ SegFormerB0_FP16.mlmodel  (~7 MB)
```

---

## Шаг 4: Сохранить и Собрать

### 4.1 Сохранить Сцену

```
File → Save Scene
```

ИЛИ: `Ctrl+S` / `Cmd+S`

### 4.2 Собрать на iOS

```
File → Build Settings
  → iOS
  → Build (или Build And Run)
```

### 4.3 Xcode

1. Откройте `.xcodeproj`
2. Проверьте что `SegFormerB0_FP16.mlmodel` в `Data/Raw/`
3. Build: `Cmd+R`

---

## 🐛 Troubleshooting

### ML Manager не появляется в списке компонентов

**Решение:**
1. Проверьте что файл существует: `Assets/Scripts/ML/MLSegmentationManager.cs`
2. Дождитесь завершения компиляции Unity (правый нижний угол)
3. Попробуйте `Assets → Reimport All`

### Не могу перетащить ML Manager в поле

**Причина:** Компонент не добавлен на GameObject

**Решение:**
1. Убедитесь что `MLSegmentationManager` добавлен на `AR Session`
2. Попробуйте кликнуть на кружок (⊙) справа от поля
3. Выберите `MLSegmentationManager` из списка

### Модель не найдена в StreamingAssets

**Решение:**
```bash
# В терминале (из корня проекта):
cp Assets/ML/CoreML/SegFormerB0_FP16.mlmodel Assets/StreamingAssets/

# Обновите Unity:
Assets → Refresh (или F5)
```

### Fill Whole Wall Mode не активируется

**Проверьте:**
1. ✅ Галочка включена в Inspector
2. ✅ `mlSegmentationManager` присвоен (не пустой)
3. ✅ `enableMLSegmentation = true` в `MLSegmentationManager`

---

## ✅ Готово!

Если все настроено правильно, вы увидите:

**В Unity Console (при старте билда):**
```
[MLSegmentation] Модель инициализирована: ML/optimum:segformer-b0...
[MLSegmentation] Inference resolution: 512x512
[MLSegmentation] Inference interval: 0.2s (~5 FPS)
```

**На iPhone (в логах Xcode):**
```
[CoreMLSegmentation] Инициализация с моделью: .../SegFormerB0_FP16.mlmodel
[CoreMLSegmentation] ✅ Модель инициализирована успешно!
[Unity → CoreML] SegmentCurrentFrame called (resolution: 512)
[CoreML → Unity] ✅ Маска скопирована (262144 bytes)
```

**Приступаем к тесту! 🚀**


