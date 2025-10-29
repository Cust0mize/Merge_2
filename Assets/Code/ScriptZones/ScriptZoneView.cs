#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using Code.GameCore.Mics;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Entities;
using UnityEngine;
using UnityEngine.Events;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.Utility.Attributes;
using Owlcat.Plugins.DotNetExtensions;
using Kingmaker.View.Mechanics.ScriptZones;
using Owlcat.EventBusSystem;
using Owlcat.Plugins.ServiceSingleton;
using Owlcat.ScriptZones.LevelDesign;

namespace Kingmaker.View.MapObjects.SriptZones
{
    [Blueprints.JsonSystem.Helpers.KnowledgeDatabaseID("166fbc22bc0f466428491ffb6056bb27")]
    public class ScriptZoneView : MapObjectView
       , ILocalEventHandler<EnteredScriptZoneEvent>
       , ISerializationCallbackReceiver
       , ILocalEventHandler<ExitedScriptZoneEvent>
       , IShapesSource
    {
        [Serializable]
        public class UnitEvent : UnityEvent<UnitEntity, ScriptZoneView>
        {
        }

    #region SerializeField

        [SerializeField]
        private BlueprintScriptZoneRef m_Blueprint;

        [SerializeField]
        [HideInInspector]
        internal Bounds m_Bounds = new Bounds(Vector3.zero, Vector3.one * 3);

#if UNITY_EDITOR
        [SerializeField]
        private Color m_GizmoColor = Color.green;
#endif

        [SerializeField]
        [HideInInspector]
        internal Bounds[] m_MoreBounds;

        [SerializeField]
        [Tooltip("When set, zone is auto-disbled when first unit enters it.")]
        private bool m_OnceOnly;

        [SerializeField]
        [Tooltip("Disables the script zone after trigger and re-enables it after the specified cooldown (in seconds)")]
        [HideIf(nameof(m_OnceOnly))]
        private float m_Cooldown;
        
        [SerializeField]
        [Tooltip("When set, zone ony triggers events for player-controllable charactes")]
        private bool m_PlayersOnly;

        [SerializeField]
        [ShowIf(nameof(m_PlayersOnly))]
        private bool m_IsMainCharacterOnly;

        [SerializeField]
        [Tooltip("When set, zone starts inactive. Set IsActive to true to start detecting units.")]
        private bool m_StartInactive;

        public UnitEvent OnUnitEntered;
        public UnitEvent OnUnitExited;

    #endregion

        public readonly List<IScriptZoneShape> Shapes = new List<IScriptZoneShape>();

        public override bool CreatesDataOnLoad 
            => true;

        private BlueprintScriptZone Blueprint
        {
            get
            {
                var bp = m_Blueprint?.Get();
                if (bp == null)
                {
                    bp = BlueprintRoot.Get<SystemMechanicsRoot>()?.DefaultScriptZone;
                    Debug.Log($"Using default script zone: {bp}");
                }
                return bp;
            }
        }

        public new ScriptZoneEntity Data
            => (ScriptZoneEntity)base.Data;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (Blueprint != null)
            {
                ApplyBlueprint(Blueprint);
            }

            Shapes.Clear();
            Shapes.AddRange(GetComponentsInChildren<IScriptZoneShape>());


            if (Shapes.Count <= 0)
            {
                gameObject.AddComponent<ScriptZoneBox>().Bounds = m_Bounds;
                foreach (Bounds b in m_MoreBounds.EmptyIfNull())
                    gameObject.AddComponent<ScriptZoneBox>().Bounds = b;

                Shapes.AddRange(GetComponentsInChildren<IScriptZoneShape>());
            }
            Data?.GetRequired<PartTriggerZoneShapesUpdater>().TryFindShapes(gameObject);
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            DrawShapesGizmos(m_GizmoColor, Shapes, gameObject);
        }

        public static void DrawShapesGizmos(Color color, List<IScriptZoneShape> runtimeShapes,  GameObject anyGameObject)
        {
            var activeColor = color;
            var inactiveColor = new Color(color.r, color.g, color.b, color.a / 3);
            var oldmatrix = Handles.matrix;
            var oldmatrixGizmo = Gizmos.matrix;
            var shapes = Application.isPlaying ? runtimeShapes.ToArray() : anyGameObject.GetComponentsInChildren<IScriptZoneShape>();
            foreach (IScriptZoneShape shape in shapes)
            {
                var behaviour = (MonoBehaviour)shape;
                Gizmos.matrix = behaviour.transform.localToWorldMatrix;
                Handles.matrix = behaviour.transform.localToWorldMatrix;
                if (Selection.Contains(anyGameObject) || Selection.Contains(behaviour.gameObject))
                {
                    Gizmos.color = activeColor;
                    Handles.color = activeColor;
                }
                else
                {
                    Gizmos.color = inactiveColor;
                    Handles.color = inactiveColor;
                }
                shape.DrawGizmos();
            }
            Handles.matrix = oldmatrix;
            Gizmos.matrix = oldmatrixGizmo;
        }
#endif

        protected override void OnDidAttachToData()
        {
            if (Blueprint == null)
            {
                return;
            }

            Data.OnceOnly = m_OnceOnly;
            Data.Cooldown = m_Cooldown;
            Data.Position = transform.position;

            if (Data.TryGet<PartTriggerZone>(out var partTriggerZone))
            {
                if (m_IsMainCharacterOnly)
                    partTriggerZone.EntityFilter = PartTriggerZone.Filter.MainCharacter;
                else
                    partTriggerZone.EntityFilter = m_PlayersOnly 
                        ? PartTriggerZone.Filter.Party : PartTriggerZone.Filter.All;
            }
        }

        public override Entity CreateEntityData(bool load)
        {
            if (Blueprint == null)
            {
                return null;
            }
            var entity = new ScriptZoneEntity(
                UniqueId,
                IsInGameBySettings,
                Blueprint)
            {
                OnceOnly = m_OnceOnly,
                Cooldown = m_Cooldown,
                Position = transform.position,
            };
            var part = entity.GetOrCreate<PartTriggerZone>();
            if (m_IsMainCharacterOnly)
                part.EntityFilter = PartTriggerZone.Filter.MainCharacter;
            else
                part.EntityFilter = m_PlayersOnly 
                    ? PartTriggerZone.Filter.Party : PartTriggerZone.Filter.All;
            if (m_StartInactive)
                entity.Deactivate();
            
            entity.GetOrCreate<PartTriggerZoneShapesUpdater>();
            
            return Entity.Initialize(entity, Entity.EntityContainerType.None);
        }

        public override bool SupportBlueprint(BlueprintMapObject blueprint)
        {
            return base.SupportBlueprint(blueprint) && blueprint is BlueprintScriptZone;
        }

        public override void ApplyBlueprint(BlueprintMapObject blueprint)
        {
            base.ApplyBlueprint(blueprint);
            m_Blueprint = blueprint.ToReference<BlueprintScriptZoneRef>();
        }

        public void OnBeforeSerialize()
        {
            if (Application.isPlaying)
            {
                return;
            }

            try
            {
                if (Blueprint != null)
                {
                    ApplyBlueprint(Blueprint);
                }
            }
            catch (NullReferenceException) // can happen if BP server cleaned it's cache
            {
                
            }
        }

        public void OnAfterDeserialize()
        {
            
        }

        public void OnLocalHandleEvent(IEventSource source, in EnteredScriptZoneEvent evt)
        {
            OnUnitEntered.Invoke(evt.Entity as UnitEntity, this);
        }

        public void OnLocalHandleEvent(IEventSource source, in ExitedScriptZoneEvent evt)
        {
            OnUnitExited.Invoke(evt.Entity as UnitEntity, this);
        }

        public List<IScriptZoneShape> GetShapes() => Shapes;
    }
}
