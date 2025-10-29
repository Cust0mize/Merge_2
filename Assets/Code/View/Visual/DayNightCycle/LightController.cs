using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using Kingmaker.AreaLogic.TimeOfDay;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Fx;
using Kingmaker.QA;
using Kingmaker.UnitLogic.FactLogic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Kingmaker.View;
using Owlcat.Runtime.Visual.SceneHelpers;
using Owlcat.Runtime.Core.Registry;
using Kingmaker.Visual.Particles.ForcedCulling;
using Owlcat.BuildPipeline;
using Owlcat.Code.ShadowTravelSystem.Utils;
using Owlcat.Runtime.Visual.Waaagh.RendererFeatures.ColoredShadows;
using Owlcat.UnityExtensions;
using RogueTrader.Code.ShaderConsts;

namespace Kingmaker.Visual.DayNightCycle
{
    [ExecuteInEditMode]
    public class LightController : RegisteredBehaviour
    {
        [NonSerialized]
        public bool EditorUpdate = false;
        [HideInInspector]
        public TimeOfDay EditorTimeOfDay;
        [HideInInspector]
        public List<Light> LightsForEdit;
        private List<StaticPrefab> StaticPrefabs;

        [HideInInspector]
        public List<ShadowProxyCombinerBox> ShadowProxyBoxes = new List<ShadowProxyCombinerBox>();

        public static LightController Active
            => ObjectRegistry<LightController>.Instance.SingleOrDefault(c
                => c.gameObject.scene == SceneManager.GetActiveScene());

        [Serializable]
        private class SphericalHarmonics
        {
            [SerializeField]
            public float[] Coefficients = new float[27];
        }

        [Space(10)]
        [Header("Camera Settings")]
        public CameraLightInitStatus CameraLight = CameraLightInitStatus.notinit;
        public CameraClearFlags CameraClearFlag = CameraClearFlags.SolidColor;
        
        [Tooltip("Состояние контурного лайта для персонажей. По дефолту должен быть всегда включен, это системная фича, " +
                 "которая должна работать на всех сценах, но могут быть исключения")]
        public bool CameraContourLightEnabled = true;
        [SerializeField] [Tooltip("Настройки дирекшенал лайта, что висит на камере. Нужен для контурного освещения " +
                                  "персонажей и выделения их из окружения. Дефолтные настройки подобраны достаточно " +
                                  "универсально, но при необходимости их можно менять здесь")]
        private CameraContourLightSettings ContourLightSettings = new CameraContourLightSettings();

        [Serializable]
        private class CameraContourLightSettings
        {
            public float Intensity = 4;
            public Color LightColor = new Color(0.495283f, 0.7442303f, 1f, 1);
        }
        
        [Space(10)]
        [Header("Base Settings")]
        public Light MainLight;

        [HideInInspector]
        public SceneLightConfig m_SoloConfig;
        //[SerializeField]
        private SceneLightConfig m_MorningConfig;
        //[SerializeField]
        private SceneLightConfig m_DayConfig;
        //[SerializeField]
        private SceneLightConfig m_EveningConfig;
        //[SerializeField]
        private SceneLightConfig m_NightConfig;

        [SerializeField] 
        private SceneLightConfig defaultConfig;

        [SerializeField] 
        private Volume defaultPostProcessVolume;
        
        [SerializeField]
        private APVSelector defaultProbeVolumeScenario;
        
        [SerializeField]
        private SceneLightConfig bufferConfig;

        public SceneLightConfig BufferConfig => bufferConfig;

        [SerializeField] 
        private List<LightConfigBinding> m_Bindings;

        public List<LightConfigBinding> Bindings => m_Bindings;

        [Space(10)]
        [Header("Reflection Probes")]
        [SerializeField]
        private List<ReflectionProbe> m_ReflectionProbes;

        public List<ReflectionProbe> ReflectionProbes => m_ReflectionProbes;

        [Space(10)]
        [Header("Post Processing")]
        //[SerializeField]
        private List<Volume> m_SoloPostProcessingVolumes = new();
        //[SerializeField]
        private List<Volume> m_MorningPostProcessingVolumes = new();
        //[SerializeField]
        private List<Volume> m_DayPostProcessingVolumes = new();
        //[SerializeField]
        private List<Volume> m_EveningPostProcessingVolumes = new();
        //[SerializeField]
        private List<Volume> m_NightPostProcessingVolumes = new();
        [Space(10)]

        [SerializeField]
        private List<LocalLightSettings> m_LocalLights;
        [SerializeField]
        public List<LocalObjectsSettings> m_LocalObjects;
        private Dictionary<VisualStateEffectType, Volume> m_PostProcessingEffectVolumesMap = new();

        public bool TryGetPostProcessingEffect(VisualStateEffectType type, out Volume effect)
        {
            effect = null;
            if (m_PostProcessingEffectVolumesMap.TryGetValue(type, out var volume))
            {
                effect = volume;
                return true;
            }

            return false;
        }
            

        public List<LocalLightSettings> LocalLights
            => m_LocalLights;

        private SceneLightConfig m_OverrideConfig;
        private TimeOfDay m_CurrentTimeOfDay;

        public TimeOfDay CurrentTimeOfDay => m_CurrentTimeOfDay;

        [System.Serializable]
        public class LocalLightSettings
        {
            public Light Light;
            public LightConfig MorningConfig;
            public LightConfig DayConfig;
            public LightConfig EveningConfig;
            public LightConfig NightConfig;
        }

        [System.Serializable]
        public class LocalObjectsSettings
        {
            public GameObject Obj;
            public ObjectConfig MorningConfig;
            public ObjectConfig DayConfig;
            public ObjectConfig EveningConfig;
            public ObjectConfig NightConfig;
        }

        [System.Serializable]
        public class LightConfig
        {
            public float intensity = 0;
            public Color color = Color.black;
            public bool enabled = false;
        }

        [System.Serializable]
        public class ObjectConfig
        {
            public bool enabled = false;
        }

        public enum CameraLightInitStatus
        {
            notinit,
            enabled,
            disabled
        }

        class BakingProbe
        {
            public BakingProbe(ReflectionProbe probe)
            {
                m_Probe = probe;
                m_Id = m_Probe.RenderProbe();
            }

            int m_Id;

            ReflectionProbe m_Probe;

            public bool IsBaked 
                => m_Probe == null || m_Probe.gameObject == null || m_Probe.IsFinishedRendering(m_Id);
		}

        List<BakingProbe> m_BakingProbes = new List<BakingProbe>();

        public bool IsProbeBaking => m_BakingProbes.Count > 0;
 
        private object ColoredShadowsOverrideKey => this;

#if UNITY_EDITOR
        private const string FOG_ENABLED_ERROR =
            "Fog must be enabled, if you don't want to use it, just set its alpha to 0.";
        private const string FOG_WRONG_TYPE_ERROR =
            "Fog must be enabled, and set to Linear type.";
        public void OnValidate()
        {
            if (RuntimeBuildUtility.IsBuildMachine)
                 return;
            
            ValidateFog();
        }

        private void ValidateFog()
        {
            if (!SceneManager.GetActiveScene().name.Contains("_Light"))
                return;
            if (!RenderSettings.fog)
            {
                foreach (SceneView scene in SceneView.sceneViews)
                    scene.ShowNotification(
                        new GUIContent(FOG_ENABLED_ERROR));
                PFLog.TechArt.Error(FOG_ENABLED_ERROR);
            }

            if (RenderSettings.fog && RenderSettings.fogMode != FogMode.Linear)
            {
                foreach (SceneView scene in SceneView.sceneViews)
                    scene.ShowNotification(
                        new GUIContent(FOG_WRONG_TYPE_ERROR));
                PFLog.TechArt.Error(FOG_WRONG_TYPE_ERROR);
            }
        }
#endif
        
        private static void SetArCombatGridOverrides(SceneLightConfig lightConfig)
        {
            // CombatHudSurfaceRenderer sr = FindObjectOfType<CombatHudSurfaceRenderer>();
            // if (sr == null)
                return;
            // if (lightConfig.ArCombatGridOverrideMaterials == null || 
            //     lightConfig.ArCombatGridOverrideMaterials.Length < 1)
            // {
            //     sr.SetOverrideMaterials(null);
            //     return;
            // }
            //
            // foreach (var mat in lightConfig.ArCombatGridOverrideMaterials)
            // {
            //     if (mat == null)
            //     {
            //         sr.SetOverrideMaterials(null);
            //         return;
            //     }
            // }
            // sr.SetOverrideMaterials(lightConfig.ArCombatGridOverrideMaterials);
        }
        
        public void InterpolateLightConfig(SceneLightConfig from, SceneLightConfig to, float interpolationValue)
        {
            BufferConfig.MainLightRotation = Vector3.Lerp(from.MainLightRotation, to.MainLightRotation, interpolationValue);
            BufferConfig.MainLightColor = Color.Lerp(from.MainLightColor, to.MainLightColor, interpolationValue);
            BufferConfig.MainLightIntensity = Mathf.Lerp(from.MainLightIntensity, to.MainLightIntensity, interpolationValue);
            BufferConfig.MainLightIndirectIntensity = Mathf.Lerp(from.MainLightIndirectIntensity, to.MainLightIndirectIntensity,
                interpolationValue);
            BufferConfig.MainLightShadowStrength = Mathf.Lerp(from.MainLightShadowStrength, to.MainLightShadowStrength,
                interpolationValue);
            
            BufferConfig.SkyAmbientColor = Color.Lerp(from.SkyAmbientColor, to.SkyAmbientColor, interpolationValue);
            BufferConfig.EquatorAmbientColor = Color.Lerp(from.EquatorAmbientColor, to.EquatorAmbientColor, interpolationValue);
            BufferConfig.GroundAmbientColor = Color.Lerp(from.GroundAmbientColor, to.GroundAmbientColor, interpolationValue);
            
            BufferConfig.SkyboxColor = Color.Lerp(from.SkyboxColor, to.SkyboxColor, interpolationValue);
            BufferConfig.SkyboxExposure = Mathf.Lerp(from.SkyboxExposure, to.SkyboxExposure, interpolationValue);
            BufferConfig.SkyboxRotation = Mathf.Lerp(from.SkyboxRotation, to.SkyboxRotation, interpolationValue);
            
            BufferConfig.FogColor = Color.Lerp(from.FogColor, to.FogColor, interpolationValue);
            BufferConfig.FogStartDistance = Mathf.Lerp(from.FogStartDistance, to.FogStartDistance, interpolationValue);
            BufferConfig.FogEndDistance = Mathf.Lerp(from.FogEndDistance, to.FogEndDistance, interpolationValue);
            
            UpdateInterpolation();
        }
        
        public void OverrideConfig([CanBeNull] SceneLightConfig config)
        {
            if (m_OverrideConfig != null && config != null)
                PFLog.TechArt.ErrorWithReport("SceneLightConfig already overridden");

            m_OverrideConfig = config;
            ChangeDayTime(m_CurrentTimeOfDay);
        }

        public void UpdateInterpolation()
        {
            ApplySceneRendreringSettings(bufferConfig);
            SetupCamera(bufferConfig);
        }
        
        [CanBeNull]
        public SceneLightConfig SelectConfig(TimeOfDay time)
            => m_OverrideConfig.Or(null) ??
               defaultConfig.Or(null) ??
               m_SoloConfig.Or(null) ??
               time switch
               {
                   TimeOfDay.Morning => m_MorningConfig,
                   TimeOfDay.Day => m_DayConfig,
                   TimeOfDay.Evening => m_EveningConfig,
                   TimeOfDay.Night => m_NightConfig,
                   _ => m_DayConfig
               };

        private float m_LightProbeBakingTimeout = 60;

        public async UniTask WaitReflectionProbes(CancellationToken token = default)
        {
            float timeout = m_LightProbeBakingTimeout;
            if (QualitySettings.realtimeReflectionProbes)
            {
                while (IsProbeBaking)
                {
                    if (timeout <= 0)
                    {
                        PFLog.TechArt.Error("Light probes baking process takes too long!");
                        break;
                    }

                    await Task.Yield();
                    
                    timeout -= Time.unscaledDeltaTime;
                }
                PFLog.TechArt.Log("Ended waiting for light probes baking.");
            }
            else
            {
                PFLog.TechArt.Log("Can't bake reflections. realtimeReflectionProbes is off.");
            }
        }

        public void ChangeDayTime(TimeOfDay time)
        {
            m_CurrentTimeOfDay = time;
            
            //Check LightController scene is active scene. Light scene must be always active.
            if (SceneManager.GetActiveScene() != gameObject.scene)
            {
                PFLog.TechArt.Error("Current scene isn't active. Set LightController scene as active scene for lighting purposes : " + gameObject.scene.name);
                SceneManager.SetActiveScene(gameObject.scene);
            }

            var config = SelectConfig(time);
            if (config == null)
            {
                PFLog.TechArt.Error("[TechArt] Missing Light Config in scene : " + gameObject.scene.name);
                EditorUpdate = false;
                return;
            }
            SetArCombatGridOverrides(config);

            if (config == m_SoloConfig) //if we want to have only one light setup without any dynamic changes. Usually indoors.
            {
                ApplyConfig(config);
                return;
            }
            
            ApplyConfig(config, time);
        }

        void Update()
        {
            if (m_BakingProbes.Count > 0)
            {
                for (int i = m_BakingProbes.Count - 1; i >= 0; i--)
                {
                    var probe = m_BakingProbes[i];
                    if (probe.IsBaked)
                        m_BakingProbes.RemoveAt(i);
                }
            }
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying && EditorUpdate)
            {
                ApplyConfig(defaultConfig);
                ApplyVolume(defaultPostProcessVolume);
                ApplyAPVScenario(defaultProbeVolumeScenario.scenario);
            }
#endif
        }

        protected override void OnEnabled()
        {
#if UNITY_EDITOR
            LightsForEdit.Clear();
#endif
        }

        protected override void OnDisabled()
        {
        }

        private void Awake()
        {
            if (CameraLight == CameraLightInitStatus.notinit)
                InitCameraDefaultLightStatusForScene();

            SetLayerInPpVolumes();
            if (Application.isPlaying)
            {
                InitializeEffectsMap();
                CacheLocalLights();
            }
        }
        
        private void OnDestroy()
        {
            ColoredShadowsSettingsOverride.Remove(ColoredShadowsOverrideKey);
        }

        private void SetLayerInPpVolumes()
        {
            if (m_SoloPostProcessingVolumes.Count != 0)
            {
                foreach (var volume in m_SoloPostProcessingVolumes)
                {
                    if (volume)
                        volume.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                }
            }
        }
        
        private void InitializeEffectsMap()
        {
            if (!BlueprintRoot.TryGet<FxRoot>(out var fxRoot))
                return;

            if (fxRoot.PostProcessingEffectsLibrary == null ||
                fxRoot.PostProcessingEffectsLibrary.GetEffectProfiles == null)
            {
                return;
            }
            
            foreach (var profile in fxRoot.PostProcessingEffectsLibrary.GetEffectProfiles)
            {
                var volume = new GameObject(profile.Key.ToString()).AddComponent<Volume>();
                volume.profile = profile.Value;
                volume.weight = 0f;
                volume.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                volume.gameObject.transform.SetParent(gameObject.transform);
                m_PostProcessingEffectVolumesMap[profile.Key] = volume;
            }
        }

        private void ApplyConfig(SceneLightConfig config, TimeOfDay time)
        {
            ApplySceneRendreringSettings(config);
            ApplyPostProcesses(time);
            ApplyLocalLights(time);
            ApplyLocalObjects(time);
            BakeReflectionProbes();
            SetupCamera(config);
        }

        private void ApplyConfig(SceneLightConfig config)
        {
            ApplySceneRendreringSettings(config);
            ApplyPostProcesses();
            SetupCamera(config);
        }

        public void SwitchShadowsInRendererWithProxy(ShadowCastingMode castingMode = ShadowCastingMode.Off)
        {
            StaticPrefab[] staticPrefabs = FindObjectsByType<StaticPrefab>(FindObjectsSortMode.None);
            foreach (var pr in staticPrefabs)
            {
                if (pr.ShadowProxies.Count < 1)
                    continue;

                bool nullShadowProxy = false;
                foreach (var proxy in pr.ShadowProxies)
                {
                    if (proxy == null)
                    {
                        PFLog.Default.Error(pr, "Null shadow proxy in object.");
                        nullShadowProxy = true;
                    }
                }
                if (nullShadowProxy)
                    continue;
                
                Renderer[] rends = pr.VisualRoot.GetComponentsInChildren<Renderer>();
                foreach (var rend in rends)
                {
                    rend.shadowCastingMode = castingMode;
                }
            }
        }

        public void ApplySceneRendreringSettings(SceneLightConfig config)
        {
            if (config == null)
            {
                PFLog.TechArt.Error("SceneLightConfig is null");
                return;
            }
            
            if (MainLight)
            {
                MainLight.color = config.MainLightColor;
                MainLight.intensity = config.MainLightIntensity;
                MainLight.bounceIntensity = config.MainLightIndirectIntensity;
                MainLight.shadowStrength = config.MainLightShadowStrength;
                MainLight.transform.rotation = Quaternion.Euler(config.MainLightRotation);
            }
             
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = config.SkyAmbientColor;
            RenderSettings.ambientEquatorColor = config.EquatorAmbientColor;
            RenderSettings.ambientGroundColor = config.GroundAmbientColor;

            if (RenderSettings.skybox != null)
            {
                if (config.SkyboxMaterial)
                    RenderSettings.skybox = config.SkyboxMaterial;
                RenderSettings.skybox.SetColor(ShaderProps._Tint, config.SkyboxColor);
                RenderSettings.skybox.SetFloat(ShaderProps._Exposure, config.SkyboxExposure);
                RenderSettings.skybox.SetFloat(ShaderProps._Rotation, config.SkyboxRotation);
            }
            else
            {
                PFLog.TechArt.Error("Missing Skybox in " + SceneManager.GetActiveScene().name + " scene render settings!");
            }

            if (config.ColoredShadowsOverride)
            {
                ColoredShadowsSettingsOverride.Add(ColoredShadowsOverrideKey, config.ColoredShadowsSettings);
            }
            else
            {
                ColoredShadowsSettingsOverride.Remove(ColoredShadowsOverrideKey);
            }

            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = config.FogColor;
            RenderSettings.fogStartDistance = config.FogStartDistance;
            RenderSettings.fogEndDistance = config.FogEndDistance;

            if (!Application.isPlaying)
            {
                LightProbes.Tetrahedralize();
            }
#if UNITY_EDITOR
            UnityEditor.Lightmapping.lightingSettings.lightmapCompression = LightmapCompression.HighQuality;

#endif
        }

        private void SetupCamera(SceneLightConfig config)
        {
            CameraView mainCam = null;
            if (CameraView.Instance != null)
                mainCam = CameraView.Instance;

            if (mainCam == null)
                return;

            mainCam.Camera.clearFlags = CameraClearFlag;
            /*
            mainCam.CameraLight.enabled = GetCameraLightStatus();
            mainCam.CharacterContourLight.enabled = CameraContourLightEnabled;
            mainCam.CharacterContourLight.color = ContourLightSettings.LightColor;
            mainCam.CharacterContourLight.intensity = ContourLightSettings.Intensity;
            */
        }

        bool GetCameraLightStatus()
        {
            if (CameraLight == CameraLightInitStatus.disabled)
                return false;
            if (CameraLight == CameraLightInitStatus.enabled)
                return true;
            
            return InitCameraDefaultLightStatusForScene();
        }

        bool InitCameraDefaultLightStatusForScene()
        {
            if (MainLight == null)
            {
                CameraLight = CameraLightInitStatus.enabled;
                return true;
            }

            CameraLight = CameraLightInitStatus.disabled;
            return false;
        }

        private void ApplyLocalLights(TimeOfDay time)
        {
            if (m_LocalLights.Count <= 0)
            {
                return;
            }
            foreach (var config in m_LocalLights)
            {
                if (config == null)
                {
                    PFLog.TechArt.Error("Local light config is missing in scene : " + gameObject.scene.name);
                    EditorUpdate = false;
                    continue;
                }

                switch (time)
                {
                    case TimeOfDay.Morning:
                        ApplyLightConfig(config.Light, config.MorningConfig);
                        break;
                    case TimeOfDay.Day:
                        ApplyLightConfig(config.Light, config.DayConfig);
                        break;
                    case TimeOfDay.Evening:
                        ApplyLightConfig(config.Light, config.EveningConfig);
                        break;
                    case TimeOfDay.Night:
                        ApplyLightConfig(config.Light, config.NightConfig);
                        break;
                    default:
                        break;
                }
            }
        }

        void Start()
        {
            SetLayerInPpVolumes();
            // if (Application.isPlaying)
            // {
            //     SwitchShadowsInRendererWithProxy(ShadowCastingMode.Off);
            //     CombineShadowProxyMeshes();
            // }

            if (Application.isPlaying)
            {
                if (defaultConfig != null)
                {
                    ApplyConfig(defaultConfig);
                }

                if (defaultPostProcessVolume != null)
                {
                    ApplyVolume(defaultPostProcessVolume);
                }
                
                ApplyAPVScenario(defaultProbeVolumeScenario.scenario);
            }
        }

        public void CombineShadowProxyMeshes()
        {
            ShadowProxyCombinerBox[] sProxyBoxes = FindObjectsOfType<ShadowProxyCombinerBox>();
            foreach (var proxyBox in sProxyBoxes)
                proxyBox.BakeShadowProxies();
        }

        private void ApplyLocalObjects(TimeOfDay time)
        {
            if (m_LocalObjects.Count <= 0)
            {
                return;
            }
            foreach (var config in m_LocalObjects)
            {
                if (config == null)
                {
                    PFLog.TechArt.Error("Local objects config is missing in scene : " + gameObject.scene.name);
                    EditorUpdate = false;
                    continue;
                }

                switch (time)
                {
                    case TimeOfDay.Morning:
                        ApplyObjectConfig(config.Obj, config.MorningConfig);
                        break;
                    case TimeOfDay.Day:
                        ApplyObjectConfig(config.Obj, config.DayConfig);
                        break;
                    case TimeOfDay.Evening:
                        ApplyObjectConfig(config.Obj, config.EveningConfig);
                        break;
                    case TimeOfDay.Night:
                        ApplyObjectConfig(config.Obj, config.NightConfig);
                        break;
                    default:
                        break;
                }
            }
        }

        private void ApplyLightConfig(Light light, LightConfig config)
        {
            if (!light || config == null)
            {
                PFLog.TechArt.Error("Missing light object in light config in scene :" + gameObject.scene.name);
                EditorUpdate = false;
                return;
            }
            light.intensity = config.intensity;
            light.color = config.color;
            light.enabled = config.enabled;
            light.GetComponentInParent<ForcedCullingRadius>().Or(null)?.SetLightEnabledByDefault(light);
        }

        private void ApplyObjectConfig(GameObject obj, ObjectConfig config)
        {
            if (!obj || config == null)
            {
                PFLog.TechArt.Error("Missing object in object config in scene :" + gameObject.scene.name);
                EditorUpdate = false;
                return;
            }

            if (obj.GetComponent<Light>() != null)
                obj.GetComponent<Light>().enabled = config.enabled;
            else
            {
                obj.SetActive(config.enabled);
            }
        }

        public void DisableAllPostProcessVolumes()
        {
            SwitchPpVolumes(m_SoloPostProcessingVolumes, false);
            SwitchPpVolumes(m_MorningPostProcessingVolumes, false);
            SwitchPpVolumes(m_DayPostProcessingVolumes, false);
            SwitchPpVolumes(m_EveningPostProcessingVolumes, false);
            SwitchPpVolumes(m_NightPostProcessingVolumes, false);
        }

        private void SwitchPpVolumes(List<Volume> volumes, bool state = true)
        {
            if (volumes.Count <= 0)
                return;
            foreach (var pp in volumes)
            {
                if (pp == null)
                    continue;
                pp.enabled = state;
            }
        }

        private void ApplyAPVScenario(string scenario)
        {
            if (scenario == String.Empty)
            {
                return;
            }
            
            ProbeReferenceVolume.instance.lightingScenario = scenario;
            ProbeReferenceVolume.instance.scenarioBlendingFactor = 0f;
        }

        private void ApplyVolume(Volume volume)
        {
            foreach (LightConfigBinding binding in m_Bindings)
            {
                bool isCurrentBinding = binding.volume == volume;
                binding.volume.enabled = isCurrentBinding;
                binding.volume.weight = isCurrentBinding ? 1f : 0f;
            }
        }
        
        private void ApplyPostProcesses(TimeOfDay time = TimeOfDay.Day)
        {
            DisableAllPostProcessVolumes();
            if (m_SoloConfig) //is indoor with solo light setup
            {
                SwitchPpVolumes(m_SoloPostProcessingVolumes, true);
                return;
            }

            switch(time)
            {
                case TimeOfDay.Morning:
                    SwitchPpVolumes(m_MorningPostProcessingVolumes, true);
                    break;
                case TimeOfDay.Day:
                    SwitchPpVolumes(m_DayPostProcessingVolumes, true);
                    break;
                case TimeOfDay.Evening:
                    SwitchPpVolumes(m_EveningPostProcessingVolumes, true);
                    break;
                case TimeOfDay.Night:
                    SwitchPpVolumes(m_NightPostProcessingVolumes, true);
                    break;
            }
        }

        private void BakeReflectionProbes()
        {
            if (m_ReflectionProbes.Count <= 0)
            {
                return;
            }

            if (m_BakingProbes.Count > 0)
			{
                m_BakingProbes.Clear();
            }

            foreach (var probe in m_ReflectionProbes)
            {
                if (probe == null)
                    continue;
                m_BakingProbes.Add(new BakingProbe(probe));
            }
        }

        public void AssignReflectionProbes()
        {
            m_ReflectionProbes = GameObject.FindObjectsOfType<ReflectionProbe>().ToList();
            foreach (var rProbe in m_ReflectionProbes)
            {
                rProbe.farClipPlane = rProbe.farClipPlane > 50 ? 50 : rProbe.farClipPlane;
                rProbe.shadowDistance = rProbe.shadowDistance > 50 ? 50 : rProbe.shadowDistance;
            }
        }

        public void ClearReflectionProbes()
        {
            m_ReflectionProbes.Clear();
        }

        public void CollectLocalLights()
        {
#if UNITY_EDITOR
            if (m_LocalLights.Count > 0)
            {
                m_LocalLights.Clear();
            }

            if (Selection.gameObjects.Length > 0)
            {
                foreach (var obj in Selection.gameObjects)
                {
                    if (obj.GetComponent<Light>() == null)
                        continue;
                    LocalLightSettings newLocalLight = new LocalLightSettings
                    {
                        Light = obj.GetComponent<Light>(),
                        MorningConfig = new LightConfig(),
                        DayConfig = new LightConfig(),
                        EveningConfig = new LightConfig(),
                        NightConfig = new LightConfig()
                    };
                    m_LocalLights.Add(newLocalLight);
                }
            }

            if (m_LocalLights.Count > 0)
            {
                foreach (var newLight in m_LocalLights)
                {
                    SetLocalLightParameters(newLight, newLight.Light, TimeOfDay.Day);
                    SetLocalLightParameters(newLight, newLight.Light, TimeOfDay.Evening);
                    SetLocalLightParameters(newLight, newLight.Light, TimeOfDay.Morning);
                    SetLocalLightParameters(newLight, newLight.Light, TimeOfDay.Night);
                }
            }
#endif
        }

        public void CollectLocalObjects()
        {
#if UNITY_EDITOR
            if (m_LocalObjects.Count > 0)
            {
                m_LocalObjects.Clear();
            }

            if (Selection.gameObjects.Length > 0)
            {
                foreach (var obj in Selection.gameObjects)
                {
                    if (obj == null)
                        continue;
                    LocalObjectsSettings newLocalObject = new LocalObjectsSettings
                    {
                        Obj = obj,
                        MorningConfig = new ObjectConfig(),
                        DayConfig = new ObjectConfig(),
                        EveningConfig = new ObjectConfig(),
                        NightConfig = new ObjectConfig()
                    };
                    m_LocalObjects.Add(newLocalObject);
                }
            }

            if (m_LocalObjects.Count > 0)
            {
                foreach (var newObj in m_LocalObjects)
                {
                    SetLocalObjParameters(newObj, newObj.Obj, TimeOfDay.Day);
                    SetLocalObjParameters(newObj, newObj.Obj, TimeOfDay.Evening);
                    SetLocalObjParameters(newObj, newObj.Obj, TimeOfDay.Morning);
                    SetLocalObjParameters(newObj, newObj.Obj, TimeOfDay.Night);
                }
            }
#endif
        }

        public void CollectLocalLightsProperties(TimeOfDay timeOfDay)
        {
#if UNITY_EDITOR
            if (m_LocalLights.Count <= 0)
            {
                PFLog.TechArt.Error("Can't collect lights parameters, list of lights is empty.");
                EditorUpdate = false;
                return;
            }

            if (Selection.gameObjects.Length > 0)
            {
                foreach (var obj in Selection.gameObjects)
                {
                    if (obj.GetComponent<Light>() == null)
                        continue;

                    var selectedLight = obj.GetComponent<Light>();
                    foreach (var localLight in m_LocalLights)
                    {
                        if (localLight.Light == selectedLight)
                        {
                            SetLocalLightParameters(localLight, selectedLight, EditorTimeOfDay);
                        }
                    }
                }
            }
#endif
        }

        public void CollectLocalObjectsProperties(TimeOfDay timeOfDay)
        {
#if UNITY_EDITOR
            if (m_LocalObjects.Count <= 0)
            {
                PFLog.TechArt.Error("Can't collect objects parameters, list of objects is empty.");
                EditorUpdate = false;
                return;
            }

            if (Selection.gameObjects.Length > 0)
            {
                foreach (var obj in Selection.gameObjects)
                {
                    if (obj == null)
                        continue;

                    foreach (var localObj in m_LocalObjects)
                    {
                        if (localObj.Obj == obj)
                        {
                            SetLocalObjParameters(localObj, obj, EditorTimeOfDay);
                        }
                    }
                }
            }
#endif
        }

        private void SetLocalLightParameters(LocalLightSettings lightSettings, Light light, TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Morning:
                    lightSettings.MorningConfig.color = light.color;
                    lightSettings.MorningConfig.enabled = light.enabled;
                    lightSettings.MorningConfig.intensity = light.intensity;
                    break;
                case TimeOfDay.Day:
                    lightSettings.DayConfig.color = light.color;
                    lightSettings.DayConfig.enabled = light.enabled;
                    lightSettings.DayConfig.intensity = light.intensity;
                    break;
                case TimeOfDay.Evening:
                    lightSettings.EveningConfig.color = light.color;
                    lightSettings.EveningConfig.enabled = light.enabled;
                    lightSettings.EveningConfig.intensity = light.intensity;
                    break;
                case TimeOfDay.Night:
                    lightSettings.NightConfig.color = light.color;
                    lightSettings.NightConfig.enabled = light.enabled;
                    lightSettings.NightConfig.intensity = light.intensity;
                    break;
            }

            BakeReflectionProbes();
        }

        private void SetLocalObjParameters(LocalObjectsSettings objSettings, GameObject obj, TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Morning:
                    objSettings.MorningConfig.enabled = obj.activeSelf;
                    break;
                case TimeOfDay.Day:
                    objSettings.DayConfig.enabled = obj.activeSelf;
                    break;
                case TimeOfDay.Evening:
                    objSettings.EveningConfig.enabled = obj.activeSelf;
                    break;
                case TimeOfDay.Night:
                    objSettings.NightConfig.enabled = obj.activeSelf;
                    break;
            }
        }

        public void UpdateLocalLightParameters()
        {
            foreach (var localLight in m_LocalLights)
            {
                if (localLight == null)
                    return;
                SetLocalLightParameters(localLight, localLight.Light, EditorTimeOfDay);
            }
        }

        public void UpdateLocalObjParameters()
        {
            foreach (var localObj in m_LocalObjects)
            {
                if (localObj == null)
                    return;
                SetLocalObjParameters(localObj, localObj.Obj, EditorTimeOfDay);
            }
        }        

        public void CollectLightsForEdit()
        {
            LightsForEdit = FindObjectsOfType<Light>().ToList();
        }
        
        public void CollectEnabledLightsForEdit()
        {
            LightsForEdit = FindObjectsOfType<Light>().Where(a => a.enabled).ToList();
        }
        
        public void ClearLightsForEdit()
        {
            LightsForEdit.Clear();
        }

        public void CollectStaticPrefabs()
        {
            StaticPrefabs = FindObjectsOfType<StaticPrefab>().ToList();
        }
        
        public void ClearStaticPrefabs()
        {
            StaticPrefabs.Clear();
        }
        
        private void ShowShadowProxiesGizmo()
        {
            if (StaticPrefabs == null)
                return;
            
            if (StaticPrefabs.Count < 1)
                return;

            foreach (var staticPrefab in StaticPrefabs)
            {
                if (staticPrefab.VisualRoot == null)
                    continue;

                if (!IsAnyRendererWithShadows(staticPrefab))
                    continue;
                
                if (staticPrefab.ShadowProxies.Count < 1)
                    Gizmos.DrawIcon(staticPrefab.transform.position, "MountIkTarget_Gizmo.png", true);

                if (staticPrefab.ShadowProxies.Count > 0)
                    Gizmos.DrawIcon(staticPrefab.transform.position, "bone.png", true);
            }
        }

        private bool IsAnyRendererWithShadows(StaticPrefab prefab)
        {
            bool result = false;

            Renderer[] renderers = prefab.VisualRoot.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                if (rend.shadowCastingMode != ShadowCastingMode.Off)
                {
                    result = true;
                    break;
                }
            }
            
            return result;
        }
        
        private void CacheLocalLights()
        {
            if (m_Bindings.Count > 0 && m_Bindings.Any(x => !x.localLightsRoot))
                PFLog.TechArt.Warning("One or more LightController.m_Binding.localLightsRoot is empty. Fix it!");
            
            foreach (LightConfigBinding binding in m_Bindings)
            {
                if (!binding.localLightsRoot)
                    continue;
                
                var localLights = binding.localLightsRoot.GetComponentsInChildren<Light>();

                binding.localLights = new LocalLightBuffer[localLights.Length];
                
                for (int i = 0; i <  binding.localLights.Length; i++)
                {
                    binding.localLights[i] = new LocalLightBuffer
                    {
                        light = localLights[i],
                        intensity = localLights[i].intensity,
                    };
                }
            }
        }

        private void OnDrawGizmos()
        {
            ShowShadowProxiesGizmo();
        }
    }
}