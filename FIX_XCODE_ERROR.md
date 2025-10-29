# 🔧 Решение ошибки "CompileStoryboard failed"

## ⚠️ Ошибка
```
Command CompileStoryboard failed with a nonzero exit code
```

Эта ошибка возникает при компиляции Launch Screen в Xcode после сборки Unity проекта.

---

## ✅ РЕШЕНИЯ (от простого к сложному)

### 🎯 Решение 1: Изменить Launch Screen Type в Unity (РЕКОМЕНДУЕТСЯ)

**В Unity Editor:**

1. **Edit > Project Settings > Player**
2. Выберите вкладку **iOS** (иконка Apple 🍎)
3. Откройте раздел **Splash Image**
4. Найдите **"Launch Screen Type"**
5. **ИЗМЕНИТЕ на один из вариантов:**
   - **"Image and background (relative size)"** ← попробуйте это
   - **"Image and background (constant size)"**
   - **"None"** (если не нужен splash screen)

6. **Важно:** Если выбрали Image and background:
   - Убедитесь, что **iOS Launch Screen Portrait/Landscape** НЕ назначены
   - Или назначьте простое изображение (PNG)

7. **Пересоберите проект:**
   ```
   File > Build Settings > iOS
   Build (выберите НОВУЮ папку, например Builds/iOS_v2)
   ```

8. Откройте новый проект в Xcode и попробуйте собрать

---

### 🎯 Решение 2: Очистить кэш Xcode

**Автоматически (используя скрипт):**

```bash
cd /Users/olzhasalidarov/Documents/Projects/ARRemalux
./fix_xcode_build.sh
```

**Вручную:**

1. **Закройте Xcode полностью**

2. **Очистите DerivedData:**
   ```bash
   rm -rf ~/Library/Developer/Xcode/DerivedData/*
   ```

3. **Очистите Module Cache:**
   ```bash
   rm -rf ~/Library/Developer/Xcode/ModuleCache.noindex/*
   ```

4. **В Unity пересоберите проект в НОВУЮ папку**

5. **Откройте Xcode:**
   - Откройте новый .xcodeproj
   - **Product > Clean Build Folder** (⌘⇧K)
   - **Product > Build** (⌘B)

---

### 🎯 Решение 3: Исправить в Xcode вручную

**Если ошибка повторяется:**

1. **Откройте проект в Xcode**

2. **В левой панели Project Navigator:**
   - Найдите **LaunchScreen.storyboard**
   - Кликните правой кнопкой → **Delete**
   - Выберите **"Move to Trash"**

3. **В настройках проекта:**
   - Выберите **Unity-iPhone target**
   - Вкладка **General**
   - Раздел **App Icons and Launch Screen**
   - **Launch Screen File**: очистите поле (оставьте пустым)

4. **Попробуйте собрать снова:**
   - **Product > Clean Build Folder** (⌘⇧K)
   - **Product > Build** (⌘B)

---

### 🎯 Решение 4: Отключить Storyboard через Info.plist

**В Xcode проекте:**

1. Откройте **Info.plist**

2. Добавьте новый ключ:
   - Key: **UILaunchStoryboardName**
   - Type: **String**
   - Value: **оставьте ПУСТЫМ**

3. Или удалите ключ `UILaunchStoryboardName` если он есть

4. **Product > Clean Build Folder** и соберите снова

---

### 🎯 Решение 5: Использовать Legacy Launch Images

**В Unity Editor:**

1. **Edit > Project Settings > Player > iOS**
2. **Splash Image**:
   - **Launch Screen Type** → **None**
   - Прокрутите вниз до **Legacy Launch Images**
   - Назначьте изображения для разных размеров экранов
   - (Опционально - можно оставить пустыми для чёрного экрана)

3. Пересоберите проект

---

### 🎯 Решение 6: Обновить Xcode (если версия старая)

Проверьте версию:
```bash
xcodebuild -version
```

**Минимальные требования:**
- Xcode 14.0+ (для iOS 16+)
- Xcode 15.0+ (для iOS 17+)

Если Xcode устарел:
- Обновите через Mac App Store
- Или скачайте с developer.apple.com

---

### 🎯 Решение 7: Пересоздать проект с нуля

**Если ничего не помогло:**

1. **В Unity:**
   ```
   File > Build Settings > iOS
   Удалите старую папку сборки
   Build в СОВЕРШЕННО НОВУЮ папку
   ```

2. **Убедитесь что в Player Settings:**
   - **Launch Screen Type**: Image and background (relative size)
   - **Minimum iOS Version**: 13.0 или выше
   - **Target SDK**: Device SDK
   - **Architecture**: ARM64 (не Universal)

3. Откройте новый проект в Xcode и соберите

---

## 📝 Дополнительная диагностика

Если ошибка повторяется, посмотрите **полный лог ошибки** в Xcode:

1. В Xcode, когда сборка падает
2. Нажмите на **ошибку** в логе
3. Справа появится **полное описание**
4. Скопируйте и изучите детали

**Типичные причины:**
- Отсутствует файл storyboard
- Некорректный XML в storyboard
- Конфликт версий iOS SDK
- Проблемы с правами доступа к файлам

---

## ✅ Рекомендуемая последовательность действий

**Попробуйте в таком порядке:**

1. ✅ **Решение 1** (изменить Launch Screen Type в Unity)
2. ✅ **Решение 2** (очистить кэш Xcode)
3. ✅ **Решение 3** (удалить storyboard в Xcode)
4. ✅ **Решение 7** (пересоздать проект с нуля)

**В 90% случаев помогает Решение 1 + 2!**

---

## 🎯 После успешной сборки

Когда проект соберётся:

1. **Product > Build** (⌘B) - должно быть без ошибок
2. **Product > Run** (⌘R) - запуск на устройстве
3. Убедитесь что приложение запускается
4. Протестируйте AR функции

---

## ❓ Часто задаваемые вопросы

**Q: Почему эта ошибка возникает?**
A: Unity генерирует storyboard, который иногда некорректен для конкретной версии Xcode/iOS.

**Q: Можно ли вообще без Launch Screen?**
A: Да! Установите Launch Screen Type в "None" в Unity.

**Q: Повлияет ли это на функциональность AR?**
A: Нет, Launch Screen - это только экран запуска, AR работает независимо.

**Q: Нужно ли пересобирать каждый раз после изменений?**
A: Да, изменения в Player Settings требуют пересборки из Unity.

---

## 📞 Если ничего не помогло

1. Проверьте версию Unity (рекомендуется Unity 2022.3 LTS+)
2. Проверьте версию Xcode (должен быть актуальный)
3. Убедитесь что macOS обновлён
4. Попробуйте собрать простой пустой проект Unity для iOS - если та же ошибка, проблема в окружении

---

**Удачи! В 99% случаев одно из этих решений помогает! 🚀**

