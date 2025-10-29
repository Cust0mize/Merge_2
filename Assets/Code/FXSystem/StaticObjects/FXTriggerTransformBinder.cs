using System;
using System.Collections;
using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.View.MapObjects.SriptZones;
using UnityEngine;
using UnityEngine.VFX;

namespace Kingmaker.Visual.FX
{
    [HelpURL("https://confluence.owlcat.games/pages/viewpage.action?pageId=141829426")]
    [RequireComponent(typeof(ScriptZoneView))]
    public class FXTriggerTransformBinder : MonoBehaviour
    {
        public VisualEffect VisualEffect;
        public List<string> TransformPropertiesToUpdate;
        
        protected ScriptZoneView m_ScriptZoneView;

        protected Dictionary<UnitEntity, string> _transformPropertyBindings = new();
        private Coroutine _runningCoroutine;

        private void Awake()
        {
            if (!ArePropertiesValid())
            {
                TransformPropertiesToUpdate = null;
                return;
            }
            
            m_ScriptZoneView = GetComponent<ScriptZoneView>();
            m_ScriptZoneView.OnUnitEntered.AddListener(OnPlayerEnter);
            m_ScriptZoneView.OnUnitExited.AddListener(OnPlayerExit);
        }

        private bool ArePropertiesValid()
        {
            if (VisualEffect == null || TransformPropertiesToUpdate == null || TransformPropertiesToUpdate.Count == 0)
            {
                return false;
            }

            foreach (var propertyName in TransformPropertiesToUpdate)
            {
                if (!VisualEffect.HasVector3($"{propertyName}_position"))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnPlayerEnter(UnitEntity unit, ScriptZoneView scriptZone)
        {
            HandleTriggerEnter(unit);
        }

        private void OnPlayerExit(UnitEntity unit, ScriptZoneView scriptZone)
        {
            HandleTriggerExit(unit);
        }

        protected virtual void HandleTriggerEnter(UnitEntity unit)
        {
            if (_transformPropertyBindings.ContainsKey(unit)) 
                return;

            var availableProperty = GetAvailableProperty();
            if (String.IsNullOrEmpty(availableProperty)) 
                return;

            _transformPropertyBindings[unit] = availableProperty;

            if (_runningCoroutine == null)
                _runningCoroutine = StartCoroutine(UpdateVFXPropertiesLoop());
        }

        protected virtual string GetAvailableProperty()
        {
            foreach (var property in TransformPropertiesToUpdate)
            {
                if (!_transformPropertyBindings.ContainsValue(property))
                    return property;
            }

            return null;
        }
        
        protected virtual void HandleTriggerExit(UnitEntity unit)
        {
            if (_transformPropertyBindings.Remove(unit) && _transformPropertyBindings.Count == 0)
            {
                StopCoroutine(_runningCoroutine);
                _runningCoroutine = null;
            }
        }

        protected IEnumerator UpdateVFXPropertiesLoop()
        {
            while (_transformPropertyBindings.Count > 0)
            {
                UpdateVFXProperties();
                yield return null;
            }

            _runningCoroutine = null;
        }

        protected virtual void UpdateVFXProperties()
        {
            foreach (var entry in _transformPropertyBindings)
            {
                VisualEffect.SetVector3($"{entry.Value}_position", entry.Key.Position);
            }
        }
    }
}
