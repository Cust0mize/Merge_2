using System;
using System.Collections.Generic;
using Kingmaker.ResourceLinks;
using Owlcat.Runtime.Visual.Waaagh.RendererFeatures.ColoredShadows;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace Kingmaker.Visual.DayNightCycle
{
    [CreateAssetMenu(fileName = "SceneLightConfig", menuName = "TechArt/Scene Light Config")]
    public partial class SceneLightConfig : ScriptableObject
    {
        [Serializable]
        public partial class Link : WeakResourceLink<SceneLightConfig>
        {
        }
        
        [Header("Main Light")]
        public Vector3 MainLightRotation = new Vector3(-170.962f, -88.48801f, 80.463f);
        public Color MainLightColor = Color.white;
        public float MainLightIntensity = 1f;
        public float MainLightIndirectIntensity = 1f;
        [Range(0, 1)]
        public float MainLightShadowStrength = 0.7f;

        [Header("Ambient Colors")]
        public Color SkyAmbientColor = Color.blue;
        public Color EquatorAmbientColor = Color.gray;
        public Color GroundAmbientColor = Color.black;

        [Header("Skybox")]
        public Material SkyboxMaterial;
        public Color SkyboxColor = Color.gray;
        [HideInInspector] //Deprecated
        public Color SkyboxSkyTint = Color.blue;
        [HideInInspector] //Deprecated
        public Color SkyboxGround = Color.gray;
        public float SkyboxExposure = 1f;
        [Range(0,360)]
        public float SkyboxRotation = 0;

        [Header("Colored Shadows")]
        public bool ColoredShadowsOverride = false;
        public ColoredShadowsSettings ColoredShadowsSettings = new();
 
        [Header("Fog")]
        public Color FogColor = Color.gray;
        public float FogStartDistance = 25;
        public float FogEndDistance = 65;

        [Header("Post Processing")]
        public VolumeProfile PpProfile;

        [Space(10)]
        [Header("AR Combat Grid Visual Overrides")]
        [Tooltip("Оверрайд материалы для комбатной сетки. Нужно, чтобы чинить ситуации когда сетку не видно из за особенностей арта конкретной зоны")]
        public Material[] ArCombatGridOverrideMaterials;

#if UNITY_EDITOR
        public void OnValidate()
        {
            ValidateFog();
        }

        private const string FOG_ENABLED_ERROR =
            "Fog must be enabled, if you don't want to use it, just set its alpha to 0.";
        private const string FOG_WRONG_TYPE_ERROR =
            "Fog must be enabled, and set to Linear type.";
        public void ValidateFog()
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
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(SceneLightConfig))]
    [CanEditMultipleObjects]
    public partial class SceneLightConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
#endif
}