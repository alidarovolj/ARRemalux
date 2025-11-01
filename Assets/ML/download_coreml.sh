#!/bin/bash
# Скрипт для скачивания готовой CoreML модели

echo "🔍 Поиск готовой CoreML модели SegFormer..."
echo ""
echo "📋 Варианты:"
echo ""
echo "1️⃣  HuggingFace Hub:"
echo "   https://huggingface.co/models?library=coreml&search=segformer"
echo ""
echo "2️⃣  Apple Model Gallery:"
echo "   https://developer.apple.com/machine-learning/models/"
echo ""
echo "3️⃣  Используйте готовую DeepLabV3 CoreML модель (альтернатива):"
echo "   https://ml-assets.apple.com/coreml/models/Image/ImageSegmentation/DeepLabV3/DeepLabV3.mlmodel"
echo ""
echo "4️⃣  Или скачайте конвертированную SegFormer (если доступна):"
echo "   # Пример команды для скачивания"
echo "   # curl -L -o CoreML/SegFormerB0_FP16.mlmodel [URL]"
echo ""
echo "⚠️  Для SegFormer пока нет официальной CoreML версии на HuggingFace."
echo "   Рекомендую использовать DeepLabV3 (сегментация 21 класс) для теста."
echo ""
echo "📥 Скачать DeepLabV3 CoreML модель?"
echo "   Это рабочая альтернатива для проверки инфраструктуры."
echo ""
read -p "Скачать DeepLabV3? (y/n): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]
then
    echo "📥 Скачивание DeepLabV3 CoreML..."
    mkdir -p CoreML
    curl -L -o CoreML/DeepLabV3.mlmodel \
        "https://ml-assets.apple.com/coreml/models/Image/ImageSegmentation/DeepLabV3/DeepLabV3.mlmodel"
    
    # Переименовываем для использования в проекте
    cp CoreML/DeepLabV3.mlmodel CoreML/SegFormerB0_FP16.mlmodel
    
    echo "✅ Модель скачана: CoreML/SegFormerB0_FP16.mlmodel"
    echo "⚠️  Это DeepLabV3 (21 класс), не SegFormer (150 классов)"
    echo "   Используйте для проверки что CoreML плагин работает!"
fi


