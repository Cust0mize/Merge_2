using System;
using UnityEngine;

namespace Owlcat.Code.TechArt.AnimationSystem.AnimationSolver
{
    [Obsolete]
    public class WeaponSocketBoneObject : MonoBehaviour
    {
        [SerializeReference]
        private WeaponSocketBone m_WeaponSocketBone;
        
        public WeaponSocketBone WeaponSocketBone
            => m_WeaponSocketBone;
    }
}