#!/usr/bin/env python3
"""
Конвертация SegFormer ONNX → CoreML для iOS
Использует: onnx-coreml для конвертации + quantization FP16
"""

import sys
import os
from pathlib import Path

try:
    import onnx
    from onnx_coreml import convert
    import coremltools as ct
except ImportError:
    print("❌ Ошибка: Необходимы библиотеки для конвертации!")
    print("Установите:")
    print("  pip install onnx 'coremltools<7.0' onnx-coreml")
    sys.exit(1)

def convert_onnx_to_coreml():
    """Конвертирует SegFormer ONNX модель в CoreML формат"""
    
    # Пути
    script_dir = Path(__file__).parent
    onnx_path = script_dir / "optimum:segformer-b0-finetuned-ade-512-512" / "model.onnx"
    output_dir = script_dir / "CoreML"
    output_dir.mkdir(exist_ok=True)
    
    output_path = output_dir / "SegFormerB0.mlmodel"
    
    if not onnx_path.exists():
        print(f"❌ ONNX модель не найдена: {onnx_path}")
        return False
    
    print("=" * 60)
    print("🤖 Конвертация SegFormer-B0 ONNX → CoreML")
    print("=" * 60)
    print(f"📁 Input:  {onnx_path}")
    print(f"📁 Output: {output_path}")
    print()
    
    try:
        # Загрузить ONNX модель
        print("📥 Загрузка ONNX модели...")
        onnx_model = onnx.load(str(onnx_path))
        
        # Проверка модели
        print("✅ ONNX модель загружена")
        print(f"   IR Version: {onnx_model.ir_version}")
        print(f"   Producer: {onnx_model.producer_name}")
        print()
        
        # Конвертация ONNX → CoreML
        print("🔄 Конвертация в CoreML...")
        print("   Это может занять несколько минут...")
        
        # Основная конвертация через onnx-coreml
        mlmodel = convert(
            onnx_model,
            minimum_ios_deployment_target='13'
        )
        
        print("✅ Конвертация завершена!")
        print()
        
        # Сохранение модели
        print("💾 Сохранение CoreML модели...")
        mlmodel.save(str(output_path))
        
        # Проверка размера
        size_mb = os.path.getsize(output_path) / (1024 * 1024)
        print(f"✅ Модель сохранена: {output_path}")
        print(f"📊 Размер: {size_mb:.1f} MB")
        print()
        
        # Quantization (FP16) для уменьшения размера
        print("🗜️  Quantization (FP16) для уменьшения размера...")
        quantized_path = output_dir / "SegFormerB0_FP16.mlmodel"
        
        # Загрузить модель для quantization
        model = ct.models.MLModel(str(output_path))
        
        # Применить FP16 quantization
        model_fp16 = ct.models.neural_network.quantization_utils.quantize_weights(
            model, 
            nbits=16
        )
        
        # Сохранить quantized модель
        model_fp16.save(str(quantized_path))
        
        quantized_size_mb = os.path.getsize(quantized_path) / (1024 * 1024)
        print(f"✅ Quantized модель сохранена: {quantized_path}")
        print(f"📊 Размер: {quantized_size_mb:.1f} MB")
        print(f"📉 Сжатие: {(1 - quantized_size_mb/size_mb) * 100:.1f}%")
        print()
        
        print("=" * 60)
        print("🎉 КОНВЕРТАЦИЯ УСПЕШНА!")
        print("=" * 60)
        print()
        print("📋 Следующие шаги:")
        print("1. Скопируйте SegFormerB0_FP16.mlmodel в Assets/StreamingAssets/")
        print("2. Создайте iOS native плагин (CoreMLSegmentation.mm)")
        print("3. Постройте Xcode проект и протестируйте")
        print()
        
        return True
        
    except Exception as e:
        print(f"❌ Ошибка конвертации: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    success = convert_onnx_to_coreml()
    sys.exit(0 if success else 1)

