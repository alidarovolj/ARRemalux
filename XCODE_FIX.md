# 🔧 Исправление Xcode Build Errors

## ❌ Ошибка: Cannot use '@try' with Objective-C exceptions disabled

### Решение 1: Включить Exceptions (Рекомендуется)

1. В Xcode откройте проект
2. Выберите **Unity-iPhone** (синяя иконка проекта)
3. В центральной панели выберите таргет **UnityFramework**
4. Перейдите на вкладку **Build Settings**
5. В поиске введите: **"Objective-C Exceptions"**
6. Найдите опцию **Enable Objective-C Exceptions**
7. Установите значение: **Yes**
8. **Product → Clean Build Folder** (Shift+Cmd+K)
9. **Product → Build** (Cmd+B)

### Решение 2: Автоматически (через post-process скрипт)

Создайте файл в Unity: `Assets/Editor/XcodePostProcess.cs`

Этот скрипт автоматически включит Objective-C Exceptions при каждом билде.

## ⚠️ Warnings (не критичны)

Остальные warnings (deprecated iOS 13.0 API) - это Unity код, можно игнорировать.

## 📱 App Icon Warning

Если хотите убрать warning про App Icon:
1. В Xcode: Unity-iPhone → Images.xcassets → AppIcon
2. Перетащите любую картинку 1024×1024 в слот "App Store iOS 1024pt"

Или игнорируйте - это не мешает тестированию.


