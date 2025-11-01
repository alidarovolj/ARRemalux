# 🔴 Отключение Debug Overlay (Красный Экран)

## Проблема

Красный overlay застрял на экране и не двигается.

**Это нормально!** Это debug UI для ML модели, не AR объект.

---

## ✅ Решение: Отключить

### **Вариант 1: Отключить в Unity (рекомендуется)**

1. Откройте Unity
2. В **Hierarchy** найдите: `Raw Image`
3. В **Inspector** → `MLSegmentationDebugViewer`
4. **Снимите галочку** ✅ (Disable component)
5. **File → Save Scene** (`Cmd+S`)
6. Пересоберите

### **Вариант 2: Удалить GameObject**

1. В **Hierarchy** → `Raw Image`
2. Правой кнопкой → **Delete**
3. **File → Save Scene**
4. Пересоберите

---

## 🎯 Что Вы Должны Видеть После:

### **Вместо красного overlay:**

1. **AR Camera Feed** - чистый видео поток
2. **Красные/розовые плоскости** на стенах (когда ARKit их обнаружит)
3. **Красные сферы** краски при клике на стену

### **Логи:**

```
✅ [HybridWallDetector] СТЕНА обнаружена
✅ [WallDetection] Создана метка краски #1
```

---

## 📝 Зачем Был Debug Viewer?

Это инструмент для **визуализации** ML segmentation:

- Показывает что модель "видит"
- Красный = background/floor
- Другие цвета = person, furniture, etc.

**Для production не нужен!** Только для отладки ML модели.

---

## 🚀 После Отключения:

1. Unity → Save Scene
2. File → Build Settings → Build (replace)
3. Xcode → Clean → Run
4. Тестируйте обнаружение стен!

