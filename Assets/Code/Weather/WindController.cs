using UnityEngine;

namespace Owlcat.Weather
{
    public class WindController : MonoBehaviour
    {
        private static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindAmplitude = Shader.PropertyToID("_WindAmplitude");
        private static readonly int WindNoise_Mult = Shader.PropertyToID("_WindNoise_Mult");
        private static readonly int WindNoise_Time = Shader.PropertyToID("_WindNoise_Time");
        private static readonly int WindNoise_Length = Shader.PropertyToID("_WindNoise_Length");
        private static readonly int windDirection = Shader.PropertyToID("_WindDirection");

        [Range(0, 2)]
        public float speed;
        [Range(0, 2)]
        public float amplitude;
        
        [Range(-1, 2)]
                public float WindNoiseMult = 1f;
        [Range(0, 5)]
                public float WindNoiseTime = 2f;
        [Range(0, 2)]
                public float WindNoiseLength = 0.18f;
        
        private void Awake()
        {
            UpdateWind();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateWind();
        }
        #endif
        
        private void UpdateWind()
        {
            Shader.SetGlobalFloat(WindSpeed, speed);
            Shader.SetGlobalFloat(WindAmplitude, amplitude);
            Shader.SetGlobalFloat(WindNoise_Mult, WindNoiseMult);
            Shader.SetGlobalFloat(WindNoise_Time, WindNoiseTime);
            Shader.SetGlobalFloat(WindNoise_Length, WindNoiseLength);
        }
    }
}