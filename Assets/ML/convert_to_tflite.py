#!/usr/bin/env python3
"""
Конвертация SegFormer ONNX → TensorFlow Lite для Android
Использует: onnx-tensorflow + TFLite converter
"""

import sys
import os
from pathlib import Path

try:
    import onnx
    from onnx_tf.backend import prepare
    import tensorflow as tf
except ImportError:
    print("❌ Ошибка: Необходимы библиотеки для конвертации!")
    print("Установите:")
    print("  pip install onnx tensorflow onnx-tf")
    sys.exit(1)

def convert_onnx_to_tflite():
    """Конвертирует SegFormer ONNX модель в TensorFlow Lite формат"""
    
    # Пути
    script_dir = Path(__file__).parent
    onnx_path = script_dir / "optimum:segformer-b0-finetuned-ade-512-512" / "model.onnx"
    output_dir = script_dir / "TFLite"
    output_dir.mkdir(exist_ok=True)
    
    tf_model_path = output_dir / "segformer_tf"
    tflite_fp32_path = output_dir / "segformer_fp32.tflite"
    tflite_fp16_path = output_dir / "segformer_fp16.tflite"
    
    if not onnx_path.exists():
        print(f"❌ ONNX модель не найдена: {onnx_path}")
        return False
    
    print("=" * 60)
    print("🤖 Конвертация SegFormer-B0 ONNX → TensorFlow Lite")
    print("=" * 60)
    print(f"📁 Input:  {onnx_path}")
    print(f"📁 Output: {tflite_fp16_path}")
    print()
    
    try:
        # Шаг 1: ONNX → TensorFlow
        print("📥 Шаг 1/3: Загрузка ONNX модели...")
        onnx_model = onnx.load(str(onnx_path))
        
        print("✅ ONNX модель загружена")
        print(f"   IR Version: {onnx_model.ir_version}")
        print()
        
        print("🔄 Шаг 2/3: Конвертация ONNX → TensorFlow...")
        print("   Это может занять несколько минут...")
        
        tf_rep = prepare(onnx_model)
        tf_rep.export_graph(str(tf_model_path))
        
        print(f"✅ TensorFlow модель сохранена: {tf_model_path}")
        print()
        
        # Шаг 2: TensorFlow → TFLite (FP32)
        print("🗜️  Шаг 3/3: Конвертация TensorFlow → TFLite...")
        
        converter = tf.lite.TFLiteConverter.from_saved_model(str(tf_model_path))
        tflite_model = converter.convert()
        
        with open(tflite_fp32_path, 'wb') as f:
            f.write(tflite_model)
        
        size_fp32_mb = os.path.getsize(tflite_fp32_path) / (1024 * 1024)
        print(f"✅ TFLite FP32 модель сохранена: {tflite_fp32_path}")
        print(f"📊 Размер: {size_fp32_mb:.1f} MB")
        print()
        
        # Шаг 3: Quantization (FP16)
        print("🗜️  Quantization (FP16) для уменьшения размера...")
        
        converter_fp16 = tf.lite.TFLiteConverter.from_saved_model(str(tf_model_path))
        converter_fp16.optimizations = [tf.lite.Optimize.DEFAULT]
        converter_fp16.target_spec.supported_types = [tf.float16]
        
        tflite_model_fp16 = converter_fp16.convert()
        
        with open(tflite_fp16_path, 'wb') as f:
            f.write(tflite_model_fp16)
        
        size_fp16_mb = os.path.getsize(tflite_fp16_path) / (1024 * 1024)
        print(f"✅ TFLite FP16 модель сохранена: {tflite_fp16_path}")
        print(f"📊 Размер: {size_fp16_mb:.1f} MB")
        print(f"📉 Сжатие: {(1 - size_fp16_mb/size_fp32_mb) * 100:.1f}%")
        print()
        
        # Проверка модели
        print("🔍 Проверка TFLite модели...")
        interpreter = tf.lite.Interpreter(model_path=str(tflite_fp16_path))
        interpreter.allocate_tensors()
        
        input_details = interpreter.get_input_details()
        output_details = interpreter.get_output_details()
        
        print("   Input:")
        for inp in input_details:
            print(f"     Shape: {inp['shape']}, Dtype: {inp['dtype']}")
        
        print("   Output:")
        for out in output_details:
            print(f"     Shape: {out['shape']}, Dtype: {out['dtype']}")
        print()
        
        print("=" * 60)
        print("🎉 КОНВЕРТАЦИЯ УСПЕШНА!")
        print("=" * 60)
        print()
        print("📋 Следующие шаги:")
        print("1. Скопируйте segformer_fp16.tflite в Assets/StreamingAssets/")
        print("2. Создайте Android TFLite плагин (TFLiteSegmentation.kt)")
        print("3. Постройте Android проект и протестируйте")
        print()
        
        return True
        
    except Exception as e:
        print(f"❌ Ошибка конвертации: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    success = convert_onnx_to_tflite()
    sys.exit(0 if success else 1)


