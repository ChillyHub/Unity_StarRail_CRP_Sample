using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    [ExecuteAlways]
#if UNITY_EDITOR
    [CanEditMultipleObjects]
#endif
    [AddComponentMenu("Light/CRP Decal Light")]
    public class DecalLight : DecalProjector
    {
        internal delegate void DecalLightAction(DecalProjector decalProjector);
        internal static event DecalLightAction onDecalLightAdd;
        internal static event DecalLightAction onDecalLightRemove;
        internal static event DecalLightAction onDecalLightPropertyChange;
        internal static event Action onAllDecalLightPropertyChange;
        internal static event DecalLightAction onDecalLightMaterialChange;

        protected override void InitMaterial()
        {
            if (material == null)
            {
#if UNITY_EDITOR
                material = CoreUtils.CreateEngineMaterial(Shader.Find("StarRail_CRP/Decal/DecalLight"));
#endif
            }
        }

        public override bool IsValid()
        {
            if (material == null)
                return false;

            bool isValid = true;

            if (material.FindPass("DecalStencilVolume") == -1)
                isValid = false;
            
            if (material.FindPass("DecalScreenSpaceShadow") == -1)
                isValid = false;
            
            if (material.FindPass("DecalStencilLighting") == -1)
                isValid = false;

            return isValid;
        }
        
        protected override void OnEnable()
        {
            InitMaterial();

            m_OldMaterial = material;

            onDecalLightAdd?.Invoke(this);

#if UNITY_EDITOR
            // Handle scene visibility
            UnityEditor.SceneVisibilityManager.visibilityChanged += UpdateDecalVisibility;
#endif
        }
        
#if UNITY_EDITOR
        void UpdateDecalVisibility()
        {
            // Fade out the decal when it is hidden by the scene visibility
            if (UnityEditor.SceneVisibilityManager.instance.IsHidden(gameObject))
            {
                onDecalLightRemove?.Invoke(this);
            }
            else
            {
                onDecalLightAdd?.Invoke(this);
                onDecalLightPropertyChange?.Invoke(this); // Scene culling mask may have changed.
            }
        }

#endif
        
        protected override void OnDisable()
        {
            onDecalLightRemove?.Invoke(this);

#if UNITY_EDITOR
            UnityEditor.SceneVisibilityManager.visibilityChanged -= UpdateDecalVisibility;
#endif
        }
        
        public override void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            if (material != m_OldMaterial)
            {
                onDecalLightMaterialChange?.Invoke(this);
                m_OldMaterial = material;
            }
            else
                onDecalLightPropertyChange?.Invoke(this);
        }
    }
}