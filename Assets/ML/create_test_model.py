#!/usr/bin/env python3
"""
Создание простой тестовой CoreML модели для проверки инфраструктуры
Эта модель НЕ для реального использования, только для теста!
"""

import sys
from pathlib import Path

print("🔧 Создание тестовой CoreML модели...")
print()

# Попробуем импортировать coremltools любой версии
try:
    import coremltools as ct
    print(f"✅ coremltools {ct.__version__} найден")
except:
    print("❌ coremltools не установлен")
    print("Установите: pip install coremltools")
    sys.exit(1)

try:
    import numpy as np
    print(f"✅ numpy {np.__version__} найден")
except:
    print("❌ numpy не установлен")
    sys.exit(1)

print()

# Создаем простую тестовую модель (512x512 → 512x512 классификация)
print("📝 Создание простой модели для теста...")

# Путь вывода
output_dir = Path(__file__).parent / "CoreML"
output_dir.mkdir(exist_ok=True)
output_path = output_dir / "SegFormerB0_FP16_TEST.mlmodel"

# Создаем минимальную модель через coremltools builder
from coremltools.models import neural_network
from coremltools.models import datatypes

input_features = [('image', datatypes.Array(512, 512, 3))]
output_features = [('segmentation', datatypes.Array(512, 512))]

builder = neural_network.NeuralNetworkBuilder(
    input_features,
    output_features,
    mode=None
)

# Добавляем простой слой (просто для формы)
builder.add_flatten(name='flatten', input_name='image', output_name='flat', mode=0)
builder.add_reshape_static(
    name='reshape',
    input_name='flat',
    output_name='segmentation',
    output_shape=(512, 512)
)

# Сохраняем
model = ct.models.MLModel(builder.spec)
model.save(str(output_path))

size_mb = output_path.stat().st_size / (1024 * 1024)
print(f"✅ Тестовая модель создана: {output_path}")
print(f"📊 Размер: {size_mb:.2f} MB")
print()
print("⚠️  ЭТО ТЕСТОВАЯ МОДЕЛЬ!")
print("   Она не выполняет реальную сегментацию.")
print("   Используйте её ТОЛЬКО для проверки что iOS плагин работает.")
print()
print("📋 Следующие шаги:")
print(f"1. Скопируйте {output_path}")
print("   в Assets/StreamingAssets/SegFormerB0_FP16.mlmodel")
print("2. Постройте проект в Unity")
print("3. Проверьте что ML инфраструктура работает (логи)")
print()
print("После проверки инфраструктуры используйте РЕАЛЬНУЮ модель SegFormer!")


