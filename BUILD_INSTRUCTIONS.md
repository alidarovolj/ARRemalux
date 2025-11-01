# 📱 Инструкция по сборке iOS с ML

## Unity Build Settings

1. **File → Build Settings**
2. Выберите **iOS**
3. Проверьте что сцена добавлена в "Scenes In Build"
4. **Player Settings:**
   - **Other Settings:**
     - Camera Usage Description: "Для AR сканирования и ML обнаружения стен"
     - Minimum iOS Version: **15.0** (для CoreML)
     - Target SDK: **Device SDK**
   - **XR Plug-in Management:**
     - ✅ Apple ARKit XR Plugin
5. Кликните **Build** (или **Build And Run**)
6. Выберите папку (например: `NewBuild/`)

## Xcode Build

1. Откройте `.xcodeproj` файл в Xcode
2. **ВАЖНО: Проверьте модель!**
   - В Project Navigator найдите: `Data/Raw/SegFormerB0_FP16.mlmodel`
   - Если модели нет → перетащите из `Assets/StreamingAssets/`
3. Выберите ваш iPhone в списке устройств
4. **Product → Build** (`Cmd+B`)
5. **Product → Run** (`Cmd+R`)

## Ожидаемые Логи

### Unity Console:
```
[MLSegmentation] Загрузка CoreML модели...
[MLSegmentation] Model path: .../SegFormerB0_FP16.mlmodel
[MLSegmentation] ✅ CoreML модель инициализирована!
[MLSegmentation] Inference resolution: 512x512
[WallDetection] fillWholeWallMode: True
[WallDetection] mlSegmentationManager: установлен ✅
```

### Xcode Console:
```
[CoreMLSegmentation] Инициализация с моделью: .../SegFormerB0_FP16.mlmodel
[CoreMLSegmentation] ✅ Модель инициализирована успешно!
[Unity → CoreML] SegmentCurrentFrame called (resolution: 512)
[CoreMLSegmentation] ⚡ Inference time: ~50ms
```

## Troubleshooting

### Модель не найдена
```
[MLSegmentation] ❌ Не удалось инициализировать CoreML модель!
```
**Решение:** Убедитесь что `SegFormerB0_FP16.mlmodel` в `Data/Raw/` в Xcode проекте

### ML не активируется
**Проверьте:**
- ✅ `Enable ML Segmentation` = true в Unity Inspector
- ✅ `mlSegmentationManager` присвоен в `WallDetectionAndPainting`
- ✅ Модель в Xcode проекте

### Низкая производительность
**Решение:** Увеличьте `Inference Interval` до 0.5 (2 FPS)


