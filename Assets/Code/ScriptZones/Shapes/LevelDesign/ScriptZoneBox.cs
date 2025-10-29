using System.Collections.Generic;
using Kingmaker.Utility.CodeTimer;
using Kingmaker.View.MapObjects.SriptZones;
using Pathfinding;
using UnityEngine;

namespace Kingmaker.View.Mechanics.ScriptZones
{
	[Blueprints.JsonSystem.Helpers.KnowledgeDatabaseID("84a8060c827087c4dbb941d7ba2f1e9a")]
	public class ScriptZoneBox : ScriptZoneShape
	{
		private Bounds m_PrevBounds;
		private Vector3 m_PrevPosition;
		private Vector3 m_PrevLocalScale;
		private List<Vector3> m_NodePositions;
		
		public Bounds Bounds = new Bounds(Vector3.zero, Vector3.one * 3);
		
		public override bool Contains(Vector3 point, IntRect size = default)
		{
			using (ProfileScope.New("ScriptZoneBox.Contains"))
			{
				var lp = transform.InverseTransformPoint(point);
				return Bounds.Contains(lp);
			}
		}
		
		public override Bounds GetBounds()
		{
			var lossyScale = transform.lossyScale;
			float sideX = Bounds.size.x * lossyScale.x;
			float sideZ = Bounds.size.z * lossyScale.z;
			float bigBoundSide = Mathf.Sqrt(sideX * sideX + sideZ * sideZ);
			
			return new Bounds(transform.position, 
				new Vector3(bigBoundSide, 
					Bounds.size.y * lossyScale.y,
					bigBoundSide));
		}
		
		public override void DrawGizmos()
		{
			Gizmos.DrawWireCube(Bounds.center, Bounds.size);
		}
		
	    public override Vector3 Center()
	    {
	        return transform.TransformPoint(Bounds.center);
	    }
	}
}
