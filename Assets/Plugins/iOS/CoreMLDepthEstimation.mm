#import <Foundation/Foundation.h>
#import <CoreML/CoreML.h>
#import <Vision/Vision.h>
#import <AVFoundation/AVFoundation.h>
#import <Accelerate/Accelerate.h>

// ====================================
// CoreML Depth Estimation Plugin для Unity
// Depth Anything V2 - Monocular Depth Estimation
// ====================================

static MLModel* depthModel = nil;
static VNImageRequestHandler* requestHandler = nil;
static CVPixelBufferRef currentPixelBuffer = nil;

// Результат depth estimation (512x512 float values)
static NSMutableData* depthMapData = nil;

#ifdef __cplusplus
extern "C" {
#endif

// ====================================
// Initialization
// ====================================

bool CoreMLDepth_Initialize(const char* modelPath) {
    NSLog(@"[CoreMLDepth] Инициализация с моделью: %s", modelPath);
    
    NSString* path = [NSString stringWithUTF8String:modelPath];
    NSURL* modelURL = [NSURL fileURLWithPath:path];
    
    NSError* error = nil;
    
    // Загружаем CoreML модель
    MLModelConfiguration* config = [[MLModelConfiguration alloc] init];
    config.computeUnits = MLComputeUnitsAll; // CPU + GPU + Neural Engine
    
    depthModel = [MLModel modelWithContentsOfURL:modelURL configuration:config error:&error];
    
    if (error != nil || depthModel == nil) {
        NSLog(@"[CoreMLDepth] ❌ Ошибка загрузки модели: %@", error.localizedDescription);
        return false;
    }
    
    NSLog(@"[CoreMLDepth] ✅ Модель инициализирована успешно!");
    NSLog(@"[CoreMLDepth] Model Description: \n%@", depthModel.modelDescription);
    
    // Выделяем буфер для depth map (512x512 floats = 262144 floats = 1048576 bytes)
    depthMapData = [[NSMutableData alloc] initWithLength:512 * 512 * sizeof(float)];
    
    return true;
}

// ====================================
// Depth Estimation from Current AR Frame
// ====================================

void CoreMLDepth_EstimateDepth(float* outputBuffer, int bufferSize, int resolution) {
    if (depthModel == nil) {
        NSLog(@"[CoreMLDepth] ⚠️ Модель не инициализирована!");
        return;
    }
    
    // Проверяем доступность AR frame
    CVPixelBufferRef pixelBuffer = currentPixelBuffer;
    
    if (pixelBuffer == nil) {
        // Первый запуск - еще нет frames, это нормально
        static int warningCount = 0;
        if (warningCount < 3) {
            NSLog(@"[CoreMLDepth] ⏳ Ожидание AR frame... (попытка %d)", ++warningCount);
        }
        return;
    }
    
    CFTimeInterval startTime = CACurrentMediaTime();
    
    @try {
        // Создаем VNImageRequestHandler
        VNImageRequestHandler* handler = [[VNImageRequestHandler alloc] 
            initWithCVPixelBuffer:pixelBuffer 
            options:@{}];
        
        // Создаем CoreML Vision Request
        VNCoreMLModel* visionModel = [VNCoreMLModel modelForMLModel:depthModel error:nil];
        VNCoreMLRequest* request = [[VNCoreMLRequest alloc] initWithModel:visionModel];
        
        // Выполняем inference
        NSError* error = nil;
        [handler performRequests:@[request] error:&error];
        
        if (error != nil) {
            NSLog(@"[CoreMLDepth] ❌ Ошибка inference: %@", error.localizedDescription);
            return;
        }
        
        // Получаем результат
        VNObservation* observation = request.results.firstObject;
        
        if ([observation isKindOfClass:[VNPixelBufferObservation class]]) {
            VNPixelBufferObservation* pixelBufferObs = (VNPixelBufferObservation*)observation;
            CVPixelBufferRef depthBuffer = pixelBufferObs.pixelBuffer;
            
            // Конвертируем depth buffer в float array
            CVPixelBufferLockBaseAddress(depthBuffer, kCVPixelBufferLock_ReadOnly);
            
            void* baseAddress = CVPixelBufferGetBaseAddress(depthBuffer);
            size_t width = CVPixelBufferGetWidth(depthBuffer);
            size_t height = CVPixelBufferGetHeight(depthBuffer);
            
            // Копируем depth values
            float* depthData = (float*)baseAddress;
            memcpy(outputBuffer, depthData, bufferSize * sizeof(float));
            
            CVPixelBufferUnlockBaseAddress(depthBuffer, kCVPixelBufferLock_ReadOnly);
            
            CFTimeInterval elapsedTime = (CACurrentMediaTime() - startTime) * 1000.0;
            NSLog(@"[CoreMLDepth] ⚡ Inference time: %.1f ms", elapsedTime);
            NSLog(@"[CoreMLDepth → Unity] ✅ Depth map ready (resolution: %zux%zu)", width, height);
        }
    }
    @catch (NSException* exception) {
        NSLog(@"[CoreMLDepth] 💥 Exception: %@", exception.reason);
    }
}

// ====================================
// Helper: Set Current AR Frame
// ====================================

void CoreMLDepth_SetARFrame(CVPixelBufferRef pixelBuffer) {
    if (currentPixelBuffer != nil) {
        CVPixelBufferRelease(currentPixelBuffer);
    }
    currentPixelBuffer = CVPixelBufferRetain(pixelBuffer);
}

// ====================================
// Cleanup
// ====================================

void CoreMLDepth_Cleanup() {
    NSLog(@"[CoreMLDepth] Cleanup...");
    
    if (currentPixelBuffer != nil) {
        CVPixelBufferRelease(currentPixelBuffer);
        currentPixelBuffer = nil;
    }
    
    depthModel = nil;
    requestHandler = nil;
    depthMapData = nil;
    
    NSLog(@"[CoreMLDepth] ✅ Cleanup завершен");
}

// ====================================
// Status Check
// ====================================

bool CoreMLDepth_IsInitialized() {
    return (depthModel != nil);
}

#ifdef __cplusplus
}
#endif

