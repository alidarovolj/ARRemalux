using UnityEngine;
using UnityEngine.XR.ARFoundation;
using TMPro;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Отображает debug информацию о плоскости прямо в AR
    /// Прикрепляется к GameObject плоскости
    /// </summary>
    public class PlaneDebugInfo : MonoBehaviour
    {
        private ARPlane plane;
        private TextMeshPro debugText;
        private GameObject textObject;
        
        private void Start()
        {
            plane = GetComponent<ARPlane>();
            
            if (plane == null)
            {
                Debug.LogError("[PlaneDebugInfo] ARPlane component not found!");
                return;
            }
            
            // Создаем 3D текст над плоскостью
            textObject = new GameObject("DebugInfo");
            textObject.transform.SetParent(transform);
            textObject.transform.localPosition = Vector3.zero;
            textObject.transform.localRotation = Quaternion.identity;
            
            debugText = textObject.AddComponent<TextMeshPro>();
            debugText.fontSize = 0.5f;
            debugText.color = Color.yellow;
            debugText.alignment = TextAlignmentOptions.Center;
            debugText.enableAutoSizing = false;
            
            // Поворачиваем текст к камере
            textObject.transform.LookAt(Camera.main.transform);
            textObject.transform.Rotate(0, 180, 0);
        }
        
        private void Update()
        {
            if (plane == null || debugText == null)
                return;
            
            // Обновляем информацию
            Vector2 size = plane.size;
            float area = size.x * size.y;
            float centerY = plane.center.y;
            
            debugText.text = $"ID: {plane.trackableId.ToString().Substring(0, 8)}\n" +
                            $"Size: {size.x:F2}×{size.y:F2}м\n" +
                            $"Area: {area:F2}м²\n" +
                            $"CenterY: {centerY:F2}м\n" +
                            $"Align: {plane.alignment}";
            
            // Поворачиваем к камере
            if (Camera.main != null)
            {
                textObject.transform.LookAt(Camera.main.transform);
                textObject.transform.Rotate(0, 180, 0);
            }
        }
        
        private void OnDestroy()
        {
            if (textObject != null)
            {
                Destroy(textObject);
            }
        }
    }
}

