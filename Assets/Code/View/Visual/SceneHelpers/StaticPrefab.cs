using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Owlcat.Runtime.Visual.SceneHelpers
{
	public partial class StaticPrefab : MonoBehaviour
	{
		
#if EDITOR_FIELDS
		
		public GameObject MechanicsRoot;
		public GameObject CollidersRoot;
		public GameObject FowBlockersRoot;
		public GameObject SoundRoot;
#endif
		
		public GameObject FxRoot;
		public GameObject LightsRoot;
		public GameObject VisualRoot;
		public List<ShadowProxy> ShadowProxies;
		public List<SurfaceHitObject> SurfaceHitObjects;

#if UNITY_EDITOR
		
		[Serializable]
		public struct OccludedRenderersData
		{
			public Renderer rend;
			public bool NoOccludedClipping;
		}
		
		[SerializeField, NonReorderable]
		private List<OccludedRenderersData> NoOccludedObjectClipRenderers;
		[SerializeField]
		private int m_OcclusionGeometryGroupId;

		private bool m_ContainsInvalidBoxColliders = false;

		public ref List<OccludedRenderersData> GetRefNoOccludedObjectClipRenderers()
		{
			return ref NoOccludedObjectClipRenderers;
		}

		private void OnValidate()
		{
			//TODO: optimize and/or save from StaticPrefabValidator
			
			var boxColliders = gameObject.GetComponentsInChildren<BoxCollider>(false);

			if (boxColliders.Length > 0)
			{
				m_ContainsInvalidBoxColliders = false;
				//precise, but costly method
				foreach (var boxCollider in boxColliders)
				{
					var globalScale = boxCollider.transform.lossyScale;
					if (globalScale.y < 0)
					{
						m_ContainsInvalidBoxColliders = true;
						return;
					}
				}
			}
		}

		private const string m_NegativeColliderMessage = "NEGATIVE Y BOXCOLLIDER!";
		
		private void OnDrawGizmos()
		{
			if (!m_ContainsInvalidBoxColliders)
			{
				return;
			}
			
			var boxColliders = gameObject.GetComponentsInChildren<BoxCollider>(false);

			if (boxColliders.Length > 0)
			{
				//precise, but costly method
				foreach (var boxCollider in boxColliders)
				{
					var globalScale = boxCollider.transform.lossyScale;
					//we decided that only Y is the problem because of navigation bugs. Artists asks to leave the ability to invert X and Z in static prefabs.
					if (globalScale.y > 0)
					{
						continue;
					}

					if (!Application.isPlaying)
					{
						Transform colliderTransform = boxCollider.transform;
						Vector3 colliderLossyScale = colliderTransform.lossyScale;
						var position = boxCollider.transform.position;
						GUIStyle labelStyle = new GUIStyle
						{
							fontSize = 20,
							alignment = TextAnchor.MiddleCenter
						};

						Camera cam = SceneView.currentDrawingSceneView.camera;
						float distanceToCam = Vector3.Distance(cam.transform.position, boxCollider.transform.position);
						labelStyle.fontSize = 25 - (int)distanceToCam;
						labelStyle.fontSize = Mathf.Clamp(labelStyle.fontSize, 8, 25);
						labelStyle.normal.textColor = Color.white;
						Handles.Label(position + Vector3.up * 0.395f, m_NegativeColliderMessage, labelStyle);
						labelStyle.normal.textColor = Color.red;
						Handles.Label(position + Vector3.up * 0.4f, m_NegativeColliderMessage, labelStyle);

						Gizmos.color = Color.red;
						Gizmos.DrawSphere(position, 0.2f);
						Vector3 colliderSize = boxCollider.size;
						Vector3 totalColliderSize = new Vector3(colliderLossyScale.x * colliderSize.x,
							colliderLossyScale.y * colliderSize.y, colliderLossyScale.z * colliderSize.z);
						Gizmos.DrawWireCube(boxCollider.bounds.center, totalColliderSize);
					}
				}
			}
		}
		
		public void CollectNoOccludedObjectClipRenderers(bool enabled = false)
		{
			Renderer[] renderers = VisualRoot.GetComponentsInChildren<Renderer>();
			var tempArr = renderers.ToList();
				
			///тут добавляем рендеры от FX
			if(FxRoot){
				Renderer[] renderers1 = FxRoot.GetComponentsInChildren<Renderer>();
				var tempArr2 = renderers1.ToList();
					
				foreach (var item in tempArr2)
				{
					tempArr.Add(item);	
				}
			}
			renderers = tempArr.ToArray();
			///
			Undo.RecordObject(this, "Changing target");
				
			NoOccludedObjectClipRenderers.Clear();
			foreach (var rend in renderers)
			{
				if (rend == null)
					continue;
				OccludedRenderersData rendData = new OccludedRenderersData
				{
					rend = rend, NoOccludedClipping = enabled
				};
				
				NoOccludedObjectClipRenderers.Add(rendData);
				EditorUtility.SetDirty(this);
			}
		}
#endif

	}
}

