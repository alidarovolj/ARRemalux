//
//  CoreMLSegmentation.mm
//  ARRemalux - CoreML Semantic Segmentation Plugin for Unity
//
//  Использует SegFormer-B0 модель для pixel-level классификации сцены
//  Поддерживает 150 классов ADE20K dataset (wall, door, person, furniture, etc.)
//

#import <Foundation/Foundation.h>
#import <CoreML/CoreML.h>
#import <Vision/Vision.h>
#import <ARKit/ARKit.h>
#import <UIKit/UIKit.h>

// ===============================
// MARK: - CoreML Segmentation Class
// ===============================

@interface CoreMLSegmentation : NSObject

@property (nonatomic, strong) MLModel *mlModel;
@property (nonatomic, strong) VNCoreMLModel *visionModel;
@property (nonatomic, strong) VNCoreMLRequest *segmentationRequest;
@property (nonatomic, assign) BOOL isInitialized;

+ (instancetype)sharedInstance;
- (BOOL)initializeWithModelPath:(NSString *)modelPath;
- (NSData *)segmentImage:(UIImage *)image withResolution:(NSInteger)resolution;
- (void)cleanup;

@end

@implementation CoreMLSegmentation

// Singleton instance
+ (instancetype)sharedInstance {
    static CoreMLSegmentation *instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[CoreMLSegmentation alloc] init];
    });
    return instance;
}

// Инициализация CoreML модели
- (BOOL)initializeWithModelPath:(NSString *)modelPath {
    NSLog(@"[CoreMLSegmentation] Инициализация с моделью: %@", modelPath);
    
    @try {
        // Проверка существования файла
        if (![[NSFileManager defaultManager] fileExistsAtPath:modelPath]) {
            NSLog(@"[CoreMLSegmentation] ❌ Файл модели не найден: %@", modelPath);
            return NO;
        }
        
        // Загрузка CoreML модели
        NSURL *modelURL = [NSURL fileURLWithPath:modelPath];
        NSError *error = nil;
        
        // Компиляция модели (если нужно)
        NSURL *compiledURL = [MLModel compileModelAtURL:modelURL error:&error];
        if (error) {
            NSLog(@"[CoreMLSegmentation] ❌ Ошибка компиляции модели: %@", error.localizedDescription);
            return NO;
        }
        
        // Загрузка скомпилированной модели
        self.mlModel = [MLModel modelWithContentsOfURL:compiledURL error:&error];
        if (error) {
            NSLog(@"[CoreMLSegmentation] ❌ Ошибка загрузки модели: %@", error.localizedDescription);
            return NO;
        }
        
        // Создание Vision модели
        self.visionModel = [VNCoreMLModel modelForMLModel:self.mlModel error:&error];
        if (error) {
            NSLog(@"[CoreMLSegmentation] ❌ Ошибка создания Vision модели: %@", error.localizedDescription);
            return NO;
        }
        
        // Создание Vision request для segmentation
        self.segmentationRequest = [[VNCoreMLRequest alloc] initWithModel:self.visionModel];
        self.segmentationRequest.imageCropAndScaleOption = VNImageCropAndScaleOptionScaleFill;
        
        self.isInitialized = YES;
        NSLog(@"[CoreMLSegmentation] ✅ Модель инициализирована успешно!");
        
        // Вывод информации о модели
        NSLog(@"[CoreMLSegmentation] Model Description: %@", self.mlModel.modelDescription);
        
        return YES;
    }
    @catch (NSException *exception) {
        NSLog(@"[CoreMLSegmentation] ❌ Exception при инициализации: %@", exception.reason);
        return NO;
    }
}

// Сегментация изображения
- (NSData *)segmentImage:(UIImage *)image withResolution:(NSInteger)resolution {
    if (!self.isInitialized) {
        NSLog(@"[CoreMLSegmentation] ❌ Модель не инициализирована!");
        return nil;
    }
    
    @try {
        // Измеряем время inference
        CFAbsoluteTime startTime = CFAbsoluteTimeGetCurrent();
        
        // Предобработка: изменить размер изображения до resolution×resolution
        UIImage *resizedImage = [self resizeImage:image toSize:CGSizeMake(resolution, resolution)];
        
        // Создание VNImageRequestHandler
        CIImage *ciImage = [CIImage imageWithCGImage:resizedImage.CGImage];
        VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] 
                                          initWithCIImage:ciImage 
                                          options:@{}];
        
        // Выполнение inference
        NSError *error = nil;
        [handler performRequests:@[self.segmentationRequest] error:&error];
        
        if (error) {
            NSLog(@"[CoreMLSegmentation] ❌ Ошибка inference: %@", error.localizedDescription);
            return nil;
        }
        
        // Извлечение результата
        VNCoreMLFeatureValueObservation *observation = self.segmentationRequest.results.firstObject;
        if (!observation) {
            NSLog(@"[CoreMLSegmentation] ❌ Нет результатов inference");
            return nil;
        }
        
        // Извлечение маски сегментации из MLMultiArray
        MLMultiArray *segmentationMap = observation.featureValue.multiArrayValue;
        
        // Конвертация MLMultiArray → byte array
        NSData *maskData = [self convertMultiArrayToByteArray:segmentationMap 
                                                  withResolution:resolution];
        
        // Измеряем время
        CFAbsoluteTime elapsedTime = CFAbsoluteTimeGetCurrent() - startTime;
        NSLog(@"[CoreMLSegmentation] ⚡ Inference time: %.1f ms", elapsedTime * 1000.0);
        
        return maskData;
    }
    @catch (NSException *exception) {
        NSLog(@"[CoreMLSegmentation] ❌ Exception при segmentation: %@", exception.reason);
        return nil;
    }
}

// Изменение размера изображения
- (UIImage *)resizeImage:(UIImage *)image toSize:(CGSize)newSize {
    UIGraphicsBeginImageContextWithOptions(newSize, NO, 1.0);
    [image drawInRect:CGRectMake(0, 0, newSize.width, newSize.height)];
    UIImage *resizedImage = UIGraphicsGetImageFromCurrentImageContext();
    UIGraphicsEndImageContext();
    return resizedImage;
}

// Конвертация MLMultiArray → byte array (маска классов)
- (NSData *)convertMultiArrayToByteArray:(MLMultiArray *)multiArray 
                          withResolution:(NSInteger)resolution {
    NSInteger totalElements = resolution * resolution;
    NSMutableData *data = [NSMutableData dataWithLength:totalElements];
    uint8_t *bytes = (uint8_t *)data.mutableBytes;
    
    // SegFormer выдаёт argmax класс для каждого пикселя
    // Форма вывода: [1, num_classes, height, width]
    // Нам нужен argmax по оси num_classes
    
    for (NSInteger i = 0; i < totalElements; i++) {
        // Получаем индекс класса с максимальной вероятностью
        // Для SegFormer это уже argmax вывод
        NSNumber *classId = [multiArray objectAtIndexedSubscript:i];
        bytes[i] = [classId unsignedCharValue];
    }
    
    return data;
}

// Очистка ресурсов
- (void)cleanup {
    self.mlModel = nil;
    self.visionModel = nil;
    self.segmentationRequest = nil;
    self.isInitialized = NO;
    NSLog(@"[CoreMLSegmentation] 🧹 Ресурсы очищены");
}

@end

// ===============================
// MARK: - Unity C Bridge
// ===============================

extern "C" {
    
    // Инициализация модели
    bool CoreML_Initialize(const char* modelPath) {
        @autoreleasepool {
            NSString *path = [NSString stringWithUTF8String:modelPath];
            NSLog(@"[Unity → CoreML] Initialize called with path: %@", path);
            
            CoreMLSegmentation *segmentation = [CoreMLSegmentation sharedInstance];
            BOOL success = [segmentation initializeWithModelPath:path];
            
            return success;
        }
    }
    
    // Сегментация текущего AR кадра
    void CoreML_SegmentCurrentFrame(uint8_t* outputBuffer, int bufferSize, int resolution) {
        @autoreleasepool {
            NSLog(@"[Unity → CoreML] SegmentCurrentFrame called (resolution: %d)", resolution);
            
            // TODO: Получить текущий AR frame из ARSession
            // Для начала используем placeholder
            
            CoreMLSegmentation *segmentation = [CoreMLSegmentation sharedInstance];
            
            if (!segmentation.isInitialized) {
                NSLog(@"[CoreML] ❌ Модель не инициализирована!");
                return;
            }
            
            // ВРЕМЕННОЕ РЕШЕНИЕ: создаём тестовое изображение
            // В реальности здесь должен быть AR camera frame
            UIImage *testImage = [UIImage imageNamed:@"test.jpg"];
            if (!testImage) {
                // Создаём черное изображение для теста
                UIGraphicsBeginImageContext(CGSizeMake(resolution, resolution));
                testImage = UIGraphicsGetImageFromCurrentImageContext();
                UIGraphicsEndImageContext();
            }
            
            // Выполняем inference
            NSData *maskData = [segmentation segmentImage:testImage withResolution:resolution];
            
            if (maskData && maskData.length == bufferSize) {
                // Копируем результат в Unity buffer
                memcpy(outputBuffer, maskData.bytes, bufferSize);
                NSLog(@"[CoreML → Unity] ✅ Маска скопирована (%d bytes)", bufferSize);
            } else {
                NSLog(@"[CoreML] ❌ Размер маски не совпадает: %lu != %d", 
                      (unsigned long)maskData.length, bufferSize);
            }
        }
    }
    
    // Сегментация конкретного изображения (для тестирования)
    void CoreML_SegmentImage(const char* imagePath, uint8_t* outputBuffer, int bufferSize, int resolution) {
        @autoreleasepool {
            NSString *path = [NSString stringWithUTF8String:imagePath];
            NSLog(@"[Unity → CoreML] SegmentImage called: %@", path);
            
            CoreMLSegmentation *segmentation = [CoreMLSegmentation sharedInstance];
            
            // Загрузка изображения
            UIImage *image = [UIImage imageWithContentsOfFile:path];
            if (!image) {
                NSLog(@"[CoreML] ❌ Не удалось загрузить изображение: %@", path);
                return;
            }
            
            // Inference
            NSData *maskData = [segmentation segmentImage:image withResolution:resolution];
            
            if (maskData && maskData.length == bufferSize) {
                memcpy(outputBuffer, maskData.bytes, bufferSize);
                NSLog(@"[CoreML → Unity] ✅ Маска скопирована (%d bytes)", bufferSize);
            }
        }
    }
    
    // Очистка ресурсов
    void CoreML_Cleanup() {
        @autoreleasepool {
            NSLog(@"[Unity → CoreML] Cleanup called");
            
            CoreMLSegmentation *segmentation = [CoreMLSegmentation sharedInstance];
            [segmentation cleanup];
        }
    }
    
    // Проверка инициализации
    bool CoreML_IsInitialized() {
        @autoreleasepool {
            CoreMLSegmentation *segmentation = [CoreMLSegmentation sharedInstance];
            return segmentation.isInitialized;
        }
    }
}


