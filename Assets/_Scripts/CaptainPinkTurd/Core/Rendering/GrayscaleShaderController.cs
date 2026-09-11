using UnityEngine;

namespace CaptainPinkTurd.Core.Rendering
{
    public class GrayscaleShaderController : MonoBehaviour
    {
        //if you want to use this material, you'll have to setup Full Screen Pass Renderer Feature in the 2D URP Renderer
        //and set the pass material as the material reference in this script
        [SerializeField] private Material _material;
        
        private float _intensity;

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        public float Intensity
        {
            get => _intensity;
            set
            {
                _intensity = Mathf.Clamp01(value);
                Apply();
            }
        }

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        void Apply() => _material?.SetFloat(IntensityId, _intensity);
    }
}