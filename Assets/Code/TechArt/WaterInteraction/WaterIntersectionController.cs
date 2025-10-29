using System.Collections.Generic;
using UnityEngine;

namespace Owlcat.Code.TechArt.WaterInteraction
{
    public class WaterIntersectionController : MonoBehaviour, IObjectIntersection
    {
        public ComputeShader computeShader;
        protected ComputeShader _computeShaderInst;
        public MeshRenderer MeshRenderer;
        protected MaterialPropertyBlock _propertyBlock;
        public Vector2 MeshBoundsXZ;
        public int TextureSize = 254;
        public float Dispersion = 0.98f;
        public float ExpansionRadius = 1f;
        public float ExpansionMult = 1.04f;
        public float SpeedOfTravel = 1f;
        public bool Rotated;
        public float StopUpdateAfterSeconds = 4f;
        public float UpdateThreshold = 0.025f;

        protected int _kernelHandle;
        protected RenderTexture NState, Nm1State, Np1State;
        protected float _timeOfLastUpdate;
        protected float _timeBeforeStopUpdate;

        protected struct ObjectIntersectorData
        {
            public Vector2 impactPosition;
            public float impactRadius;
        }

        //protected List<ObjectIntersectorData> _waterIntersectorsData;
        protected List<ObjectIntersector> _intersectors = new List<ObjectIntersector>();

        protected void OnEnable()
        {
            NState = InitTexture();
            Nm1State = InitTexture();
            Np1State = InitTexture();
            AssignPropertyBlock();

            UpdateKernelHandle();
        }

        protected virtual void AssignPropertyBlock()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _propertyBlock.SetTexture("_RipplesTexture", NState);
            MeshRenderer.SetPropertyBlock(_propertyBlock);
        }

        protected virtual void UpdateKernelHandle()
        {
            _computeShaderInst = Instantiate(computeShader);
            _kernelHandle = _computeShaderInst.FindKernel("WaterRipples");
        }

        protected virtual RenderTexture InitTexture()
        {
            var tex = new RenderTexture(TextureSize, TextureSize, 1,
                UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SNorm);
            tex.enableRandomWrite = true;
            tex.filterMode = FilterMode.Bilinear;
            tex.Create();
            return tex;
        }

        protected void Update()
        {
            if (_intersectors != null && _intersectors.Count > 0)
            {
                UpdateComputeShader();
            }
            else if (_timeBeforeStopUpdate > 0f)
            {
                _timeBeforeStopUpdate -= Time.deltaTime;
                UpdateComputeShader();
            }
        }

        protected virtual void UpdateComputeShader()
        {
            if (Time.time - _timeOfLastUpdate < UpdateThreshold)
                return;
            
            _timeOfLastUpdate = Time.time;
            
            Graphics.CopyTexture(NState, Nm1State);
            Graphics.CopyTexture(Np1State, NState);

            _computeShaderInst.SetTexture(_kernelHandle, "NState", NState);
            _computeShaderInst.SetTexture(_kernelHandle, "Nm1State", Nm1State);
            _computeShaderInst.SetTexture(_kernelHandle, "Np1State", Np1State);

            _computeShaderInst.SetFloat("dispersion", Dispersion);
            _computeShaderInst.SetFloat("speedOfTravel", SpeedOfTravel);
            _computeShaderInst.SetFloat("expansionRadius", ExpansionRadius);
            _computeShaderInst.SetFloat("expansionMult", ExpansionMult);
            _computeShaderInst.SetVector("resolution", new Vector2(TextureSize, TextureSize));

            float meshScale = Mathf.Max(MeshBoundsXZ.x, MeshBoundsXZ.y) * 2f;
            _computeShaderInst.SetFloat("meshScale", meshScale);

            var numImpactPositions = 0;
            ComputeBuffer impactPositionsBuffer = null;

            var waterIntersectorsData = new List<ObjectIntersectorData>();

            foreach (var item in _intersectors)
            {
                if (item.Transform == null)
                {
                    RemoveEmptyIntersectors();
                    return;
                }

                var localPosition = -MeshRenderer.transform.InverseTransformPoint(item.Transform.position);

                Vector2 impactPosition_;
                if (Rotated)
                {
                    impactPosition_ = new Vector2(
                        (localPosition.x + MeshBoundsXZ.x) / (MeshBoundsXZ.x * 2),
                        (localPosition.y + MeshBoundsXZ.y) / (MeshBoundsXZ.y * 2));
                }
                else
                {
                    impactPosition_ = new Vector2(
                        (localPosition.x + MeshBoundsXZ.x) / (MeshBoundsXZ.x * 2),
                        (localPosition.z + MeshBoundsXZ.y) / (MeshBoundsXZ.y * 2));
                }

                waterIntersectorsData.Add(new ObjectIntersectorData
                {
                    impactPosition = impactPosition_,
                    impactRadius = item.ImpactRadius / (Mathf.Abs(MeshBoundsXZ.x * 2)),
                });
            }

            if (waterIntersectorsData.Count > 0)
            {
                impactPositionsBuffer = new ComputeBuffer(waterIntersectorsData.Count, sizeof(float) * 3);
                impactPositionsBuffer.SetData(waterIntersectorsData);
                _computeShaderInst.SetBuffer(_kernelHandle, "impactPositionsBuffer", impactPositionsBuffer);
                numImpactPositions = waterIntersectorsData.Count;
            }

            _computeShaderInst.SetInt("numImpactPositions", numImpactPositions);
            _computeShaderInst.Dispatch(_kernelHandle, TextureSize / 8, TextureSize / 8, 1);

            if (impactPositionsBuffer != null)
            {
                impactPositionsBuffer.Dispose();
            }
        }

        protected virtual void RemoveEmptyIntersectors()
        {
            _intersectors.RemoveAll(intersector => intersector.Transform == null);
        }

        public void InIntersection(ObjectIntersector intersector)
        {
            OnIntersection(intersector);
        }

        protected virtual void OnIntersection(ObjectIntersector intersector)
        {
            if (!_intersectors.Contains(intersector))
            {
                _intersectors.Add(intersector);
            }
        }

        public void OutOfIntersection(ObjectIntersector intersector)
        {
            OnOutOfIntersection(intersector);
        }

        protected virtual void OnOutOfIntersection(ObjectIntersector intersector)
        {
            if (_intersectors != null && _intersectors.Contains(intersector))
            {
                _intersectors.Remove(intersector);

                if (_intersectors.Count == 0)
                    _timeBeforeStopUpdate = StopUpdateAfterSeconds;
            }
        }

        protected virtual void OnDestroy()
        {
            if (NState != null) NState.Release();
            if (Nm1State != null) Nm1State.Release();
            if (Np1State != null) Np1State.Release();
        }
    }
}