#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Post-process скрипт для автоматической настройки Xcode проекта
/// Включает Objective-C Exceptions для CoreML плагина
/// </summary>
public class XcodePostProcess
{
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        UnityEngine.Debug.Log("[XcodePostProcess] Настройка Xcode проекта...");

        // Путь к pbxproj файлу
        string projectPath = pathToBuiltProject + "/Unity-iPhone.xcodeproj/project.pbxproj";
        
        // Загружаем проект
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        // Получаем GUID таргетов
        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        // Включаем Objective-C Exceptions для UnityFramework
        project.SetBuildProperty(frameworkTargetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
        
        UnityEngine.Debug.Log("[XcodePostProcess] ✅ Objective-C Exceptions включены для UnityFramework");

        // Сохраняем изменения
        project.WriteToFile(projectPath);

        UnityEngine.Debug.Log("[XcodePostProcess] ✅ Xcode проект настроен!");
    }
}
#endif


