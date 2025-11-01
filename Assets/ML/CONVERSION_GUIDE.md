# 🔄 Руководство по Конвертации ML Модели

## 📋 Требования

Установите необходимые Python библиотеки:

```bash
pip install onnx coremltools onnx-coreml torch transformers
```

**Версии (рекомендуемые):**
- Python 3.8+
- coremltools 6.0+
- onnx 1.14+
- onnx-coreml 1.4+

---

## 🚀 Конвертация ONNX → CoreML (iOS)

### Шаг 1: Запуск конвертации

```bash
cd Assets/ML
python convert_to_coreml.py
```

**Результат:**
- `CoreML/SegFormerB0.mlmodel` - Полная модель (~14 MB)
- `CoreML/SegFormerB0_FP16.mlmodel` - Оптимизированная модель FP16 (~7 MB) ✅

### Шаг 2: Копирование модели в StreamingAssets

```bash
# Автоматически или вручную:
cp CoreML/SegFormerB0_FP16.mlmodel ../StreamingAssets/SegFormerB0_FP16.mlmodel
```

---

## ⚙️ Конвертация ONNX → TensorFlow Lite (Android)

### Шаг 1: ONNX → TensorFlow

```bash
pip install onnx-tf tensorflow
python convert_to_tflite.py
```

### Шаг 2: TensorFlow → TFLite

Скрипт автоматически создаст:
- `TFLite/segformer_fp32.tflite` - Полная модель (~14 MB)
- `TFLite/segformer_fp16.tflite` - Оптимизированная модель FP16 (~7 MB) ✅

### Шаг 3: Копирование модели в StreamingAssets

```bash
cp TFLite/segformer_fp16.tflite ../StreamingAssets/segformer_fp16.tflite
```

---

## 📊 Проверка Модели

### iOS (CoreML)

```bash
# Просмотр информации о модели
xcrun coremlcompiler compile CoreML/SegFormerB0_FP16.mlmodel .

# Список слоёв модели
python -c "import coremltools as ct; model = ct.models.MLModel('CoreML/SegFormerB0_FP16.mlmodel'); print(model.get_spec())"
```

### Android (TFLite)

```bash
# Просмотр информации о модели
python -c "import tensorflow as tf; interpreter = tf.lite.Interpreter('TFLite/segformer_fp16.tflite'); print(interpreter.get_input_details()); print(interpreter.get_output_details())"
```

---

## ✅ Ожидаемые Результаты

### Вход модели:
- **Размер:** 512×512×3 (RGB изображение)
- **Формат:** Float32 (normalized [0, 1])
- **Preprocessing:** Нормализация ImageNet mean/std

### Выход модели:
- **Размер:** 512×512 (карта классов)
- **Формат:** Int64 или UInt8 (class ID для каждого пикселя)
- **Классы:** 0-149 (ADE20K dataset)
  - 0 = wall (стена)
  - 14 = door (дверь)
  - 3 = floor (пол)
  - 12 = person (человек)
  - 23 = sofa (диван)
  - ... (всего 150 классов)

---

## 🐛 Troubleshooting

### Ошибка: "No module named 'onnx_coreml'"

```bash
pip install --upgrade onnx-coreml
```

### Ошибка: "CoreML conversion failed"

Попробуйте конвертацию через промежуточный формат:

```bash
# ONNX → TensorFlow → CoreML
pip install onnx-tf tf-coreml
```

### Модель слишком большая (> 10 MB)

Используйте quantization:
- FP16: ~50% меньше, минимальная потеря точности ✅
- INT8: ~75% меньше, возможна потеря точности

---

## 📚 Дополнительные Ресурсы

- [CoreML Tools Documentation](https://coremltools.readme.io/)
- [TensorFlow Lite Converter Guide](https://www.tensorflow.org/lite/convert)
- [SegFormer Model Card](https://huggingface.co/nvidia/segformer-b0-finetuned-ade-512-512)
- [ADE20K Dataset Classes](https://groups.csail.mit.edu/vision/datasets/ADE20K/)


