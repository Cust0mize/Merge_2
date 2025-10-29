using System;
using Kingmaker.Visual.HitSystem;
using Owlcat.Runtime.Core.Registry;
using UnityEngine;

namespace Kingmaker.Visual.Sound
{
	public class SurfaceTypeObject : RegisteredBehaviour
	{
		public const float TileSize = 0.2f;

		[SerializeField, HideInInspector]
		private byte[] m_Data;

        [SerializeField, HideInInspector]
        private TextAsset m_SoundCacheFile;

        private byte[] m_RuntimeData;

		public Bounds Bounds;

		//WH-278674: do not overwrite bounds in Verify
		[SerializeField, Tooltip("Allows editing of Y extent")]
		private bool m_UnboundY = false;

		//WH-278674: additional check for valid sound surface source for multi-layered maps
		[SerializeField, Tooltip("Will return SoundSurface only for objects within bounds")]
		private bool m_UseOnlyInBounds = false;
        public bool UseOnlyInBounds
			=> m_UseOnlyInBounds;

		[SerializeField, Range(0, TileSize)]
        private float m_RaycastThickness = 0;

		public int Width { get; private set; }
        public int Length { get; private set; }

        public TextAsset SoundCacheFile
            => m_SoundCacheFile;

        public float RaycastThickness => m_RaycastThickness;
        
        //for footprints
        public static SurfaceType? GetSurfaceSoundTypeSwitch(Vector3 worldPos)
        {
	        foreach (var surface in ObjectRegistry<SurfaceTypeObject>.Instance)
	        {
		        if (surface.TryGetSurfaceType(worldPos, out byte surfaceType))
		        {
			        return (SurfaceType)surfaceType;
		        }
	        }

	        return null;
        }

        //TODO: NOTE! it will return TRUE and surface type Ground even if coords are out of texture bounds toward positive x and z!
        //but will return FALSE if out of bounds toward other directions! Looks like UB. Is it intentional?
		public bool TryGetSurfaceType(Vector3 worldPos, out byte surfaceType)
		{
			surfaceType = 0;

            if (m_RuntimeData == null)
			{
				return false;
			}

			if (!TryGetCoordinates(worldPos, out int x, out int z))
			{
				return false;
			}

			int index = GetIndex(x, z);
			surfaceType = Get(index);
			return true;
        }

        public byte Get(int index)
        {
            if (index < 0 || index >= m_RuntimeData.Length)
            {
                return 0;
            }

            return m_RuntimeData[index];
        }

        public bool TryGetCoordinates(Vector3 worldPos, out int x, out int z)
        {
	        x = (int)((worldPos.x - Bounds.min.x) / TileSize);
            z = (int)((worldPos.z - Bounds.min.z) / TileSize);

			if (m_UseOnlyInBounds && !Bounds.Contains(worldPos))
			{
				return false;
			}

			return x >= 0 && z >= 0;
        }

        public int GetIndex(int x, int z)
        {
            return x + Width * z;
        }

        protected override void OnEnabled()
        {
            UpdateValues();
        }

        public void UpdateValues()
        {
            Width = Mathf.CeilToInt(Bounds.size.x / TileSize);
            Length = Mathf.CeilToInt(Bounds.size.z / TileSize);
            
            if (m_SoundCacheFile)
            {
                m_RuntimeData = m_SoundCacheFile.bytes;
            }
            else
            {
                m_RuntimeData = m_Data;
            }
        }

        public void SetData(TextAsset soundCacheFile)
        {
            m_Data = null;
            m_SoundCacheFile = soundCacheFile;
            m_RuntimeData = SoundCacheFile.bytes;
        }
        
		private void OnValidate()
		{
			if (m_UnboundY)
			{
				return;
			}
			
			Bounds.extents = new Vector3(Bounds.extents.x, Math.Max(Bounds.extents.x, Bounds.extents.z), Bounds.extents.z);
		}
	}
}