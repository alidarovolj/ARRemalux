#!/bin/bash

# Скрипт для очистки кэша Xcode и исправления проблем сборки

echo "🔧 Исправление проблем сборки Xcode для Unity проекта..."
echo ""

# 1. Очистка кэша Xcode
echo "1️⃣ Очистка кэша Xcode..."
rm -rf ~/Library/Developer/Xcode/DerivedData/*
echo "✅ Кэш DerivedData очищен"

# 2. Очистка кэша модулей
echo ""
echo "2️⃣ Очистка кэша модулей..."
rm -rf ~/Library/Developer/Xcode/ModuleCache.noindex/*
echo "✅ Кэш модулей очищен"

# 3. Очистка iOS DeviceSupport (опционально)
# Раскомментируйте если нужно:
# rm -rf ~/Library/Developer/Xcode/iOS\ DeviceSupport/*

echo ""
echo "✅ Очистка завершена!"
echo ""
echo "📝 СЛЕДУЮЩИЕ ШАГИ:"
echo "1. Вернитесь в Unity"
echo "2. File > Build Settings > iOS"
echo "3. Выберите 'Build' (не Build and Run) и укажите НОВУЮ папку"
echo "   (например: Builds/iOS_Clean)"
echo "4. После завершения сборки:"
echo "   - Откройте новый .xcodeproj в Xcode"
echo "   - Product > Clean Build Folder (Cmd+Shift+K)"
echo "   - Product > Build (Cmd+B)"
echo ""
echo "Если проблема повторится, смотрите дополнительные решения ниже."
echo ""

