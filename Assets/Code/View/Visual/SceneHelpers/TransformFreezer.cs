using UnityEditor;
using UnityEngine;

namespace Owlcat.Runtime.Visual.SceneHelpers
{
    [ExecuteInEditMode]
    public class TransformFreezer : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Start()
        {
            transform.hideFlags = HideFlags.NotEditable;
            hideFlags = HideFlags.NotEditable;
        }
        
        void OnDrawGizmosSelected()
        {
            if (Selection.activeGameObject == gameObject)
                Tools.current = Tool.None;
        }
#endif
    }
}