using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace RemaluxAR.AR
{
    /// <summary>
    /// Простой скрипт для визуализации AR плоскостей
    /// Добавьте этот скрипт на префаб плоскости
    /// </summary>
    [RequireComponent(typeof(ARPlane))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ARPlanePrefabSetup : MonoBehaviour
    {
        [SerializeField] private Material planeMaterial;
        
        private ARPlane arPlane;
        private MeshRenderer meshRenderer;

        private void Awake()
        {
            arPlane = GetComponent<ARPlane>();
            meshRenderer = GetComponent<MeshRenderer>();

            // Создаем простой материал если не назначен
            if (planeMaterial == null)
            {
                planeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                planeMaterial.color = new Color(0f, 1f, 0f, 0.3f); // Полупрозрачный зеленый
            }

            meshRenderer.material = planeMaterial;
        }

        private void Update()
        {
            // Можно менять цвет в зависимости от типа плоскости
            if (arPlane != null)
            {
                switch (arPlane.alignment)
                {
                    case UnityEngine.XR.ARSubsystems.PlaneAlignment.HorizontalUp:
                        // Пол - зеленый
                        planeMaterial.color = new Color(0f, 1f, 0f, 0.3f);
                        break;
                    case UnityEngine.XR.ARSubsystems.PlaneAlignment.HorizontalDown:
                        // Потолок - голубой
                        planeMaterial.color = new Color(0f, 0.5f, 1f, 0.3f);
                        break;
                    case UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical:
                        // Стена - красный
                        planeMaterial.color = new Color(1f, 0f, 0f, 0.3f);
                        break;
                }
            }
        }
    }
}


