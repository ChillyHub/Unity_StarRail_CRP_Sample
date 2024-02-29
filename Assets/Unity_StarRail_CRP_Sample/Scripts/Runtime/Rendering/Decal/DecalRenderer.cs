using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Unity_StarRail_CRP_Sample
{
    [ExecuteAlways]
#if UNITY_EDITOR
    [CanEditMultipleObjects]
#endif
    [AddComponentMenu("Rendering/CRP Decal Renderer")]
    public class DecalRenderer : DecalProjector
    {
        internal delegate void DecalRendererAction(DecalRenderer decalRenderer);
        internal static event DecalRendererAction onDecalRendererAdd;
        internal static event DecalRendererAction onDecalRendererRemove;
        internal static event DecalRendererAction onDecalRendererPropertyChange;
        internal static event Action onAllDecalRendererPropertyChange;
        internal static event DecalRendererAction onDecalRendererMaterialChange;

        protected override void InitMaterial()
        {
            if (material == null)
            {
#if UNITY_EDITOR
                material = CoreUtils.CreateEngineMaterial(Shader.Find("StarRail_CRP/Decal/DecalObject"));
#endif
            }
        }
        
        public override bool IsValid()
        {
            if (material == null)
                return false;

            if (material.FindPass("DecalGBuffer") != -1)
                return true;

            return false;
        }
        
        protected override void OnEnable()
        {
            InitMaterial();

            m_OldMaterial = material;

            onDecalRendererAdd?.Invoke(this);

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
                onDecalRendererRemove?.Invoke(this);
            }
            else
            {
                onDecalRendererAdd?.Invoke(this);
                onDecalRendererPropertyChange?.Invoke(this); // Scene culling mask may have changed.
            }
        }

#endif

        protected override void OnDisable()
        {
            onDecalRendererRemove?.Invoke(this);

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
                onDecalRendererMaterialChange?.Invoke(this);
                m_OldMaterial = material;
            }
            else
                onDecalRendererPropertyChange?.Invoke(this);
        }
    }
    
//    /// <summary>The scaling mode to apply to decals that use the Decal Projector.</summary>
//    public enum DecalScaleMode
//    {
//        /// <summary>Ignores the transformation hierarchy and uses the scale values in the Decal Projector component directly.</summary>
//        ScaleInvariant,
//        /// <summary>Multiplies the lossy scale of the Transform with the Decal Projector's own scale then applies this to the decal.</summary>
//        [InspectorName("Inherit from Hierarchy")]
//        InheritFromHierarchy,
//    }
//    
//    [ExecuteAlways]
//#if UNITY_EDITOR
//    [CanEditMultipleObjects]
//#endif
//    [AddComponentMenu("Rendering/CRP Decal Renderer")]
//    public class DecalRenderer : MonoBehaviour
//    {
//        internal delegate void DecalRendererAction(DecalRenderer decalRenderer);
//        
//        internal static event DecalRendererAction onDecalAdd;
//        internal static event DecalRendererAction onDecalRemove;
//        internal static event DecalRendererAction onDecalPropertyChange;
//        internal static event Action onAllDecalPropertyChange;
//        internal static event DecalRendererAction onDecalMaterialChange;
//        public static Material defaultMaterial { get; set; }
//        public static bool isSupported => onDecalAdd != null;
//
//        internal DecalEntity decalEntity { get; set; }
//
//        [SerializeField]
//        private Material m_Material = null;
//        public Material material
//        {
//            get
//            {
//                return m_Material;
//            }
//            set
//            {
//                m_Material = value;
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        private float m_DrawDistance = 1000.0f;
//        public float drawDistance
//        {
//            get
//            {
//                return m_DrawDistance;
//            }
//            set
//            {
//                m_DrawDistance = Mathf.Max(0f, value);
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        [Range(0, 1)]
//        private float m_FadeScale = 0.9f;
//        public float fadeScale
//        {
//            get
//            {
//                return m_FadeScale;
//            }
//            set
//            {
//                m_FadeScale = Mathf.Clamp01(value);
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        [Range(0, 180)]
//        private float m_StartAngleFade = 180.0f;
//        public float startAngleFade
//        {
//            get
//            {
//                return m_StartAngleFade;
//            }
//            set
//            {
//                m_StartAngleFade = Mathf.Clamp(value, 0.0f, 180.0f);
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        [Range(0, 180)]
//        private float m_EndAngleFade = 180.0f;
//        public float endAngleFade
//        {
//            get
//            {
//                return m_EndAngleFade;
//            }
//            set
//            {
//                m_EndAngleFade = Mathf.Clamp(value, m_StartAngleFade, 180.0f);
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        private Vector2 m_UVScale = new Vector2(1, 1);
//        public Vector2 uvScale
//        {
//            get
//            {
//                return m_UVScale;
//            }
//            set
//            {
//                m_UVScale = value;
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        private Vector2 m_UVBias = new Vector2(0, 0);
//        public Vector2 uvBias
//        {
//            get
//            {
//                return m_UVBias;
//            }
//            set
//            {
//                m_UVBias = value;
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        uint m_DecalLayerMask = 1;
//        public uint renderingLayerMask
//        {
//            get => m_DecalLayerMask;
//            set => m_DecalLayerMask = value;
//        }
//
//        [SerializeField]
//        private DecalScaleMode m_ScaleMode = DecalScaleMode.ScaleInvariant;
//        public DecalScaleMode scaleMode
//        {
//            get => m_ScaleMode;
//            set
//            {
//                m_ScaleMode = value;
//                OnValidate();
//            }
//        }
//
//        [SerializeField]
//        internal Vector3 m_Offset = new Vector3(0, 0, 0.5f);
//        public Vector3 pivot
//        {
//            get
//            {
//                return m_Offset;
//            }
//            set
//            {
//                m_Offset = value;
//                OnValidate();
//            }
//        }
//        
//        public ref Vector3 refOffset => ref m_Offset;
//
//        [SerializeField]
//        internal Vector3 m_Size = new Vector3(1, 1, 1);
//        public Vector3 size
//        {
//            get
//            {
//                return m_Size;
//            }
//            set
//            {
//                m_Size = value;
//                OnValidate();
//            }
//        }
//        
//        public ref Vector3 refSize => ref m_Size;
//
//        [SerializeField]
//        [Range(0, 1)]
//        private float m_FadeFactor = 1.0f;
//        public float fadeFactor
//        {
//            get
//            {
//                return m_FadeFactor;
//            }
//            set
//            {
//                m_FadeFactor = Mathf.Clamp01(value);
//                OnValidate();
//            }
//        }
//
//        private Material m_OldMaterial = null;
//
//        /// <summary>A scale that should be used for rendering and handles.</summary>
//        public Vector3 effectiveScale => m_ScaleMode == DecalScaleMode.InheritFromHierarchy ? transform.lossyScale : Vector3.one;
//        /// <summary>current size in a way the DecalSystem will be able to use it</summary>
//        public Vector3 decalSize => new Vector3(m_Size.x, m_Size.z, m_Size.y);
//        /// <summary>current size in a way the DecalSystem will be able to use it</summary>
//        public Vector3 decalOffset => new Vector3(m_Offset.x, -m_Offset.z, m_Offset.y);
//        /// <summary>current uv parameters in a way the DecalSystem will be able to use it</summary>
//        public Vector4 uvScaleBias => new Vector4(m_UVScale.x, m_UVScale.y, m_UVBias.x, m_UVBias.y);
//
//        void InitMaterial()
//        {
//            if (m_Material == null)
//            {
//#if UNITY_EDITOR
//                m_Material = CoreUtils.CreateEngineMaterial(Shader.Find("StarRail_CRP/Decal/DecalObject"));
//#endif
//            }
//        }
//
//        void OnEnable()
//        {
//            InitMaterial();
//
//            m_OldMaterial = m_Material;
//
//            onDecalAdd?.Invoke(this);
//
//#if UNITY_EDITOR
//            // Handle scene visibility
//            UnityEditor.SceneVisibilityManager.visibilityChanged += UpdateDecalVisibility;
//#endif
//        }
//
//#if UNITY_EDITOR
//        void UpdateDecalVisibility()
//        {
//            // Fade out the decal when it is hidden by the scene visibility
//            if (UnityEditor.SceneVisibilityManager.instance.IsHidden(gameObject))
//            {
//                onDecalRemove?.Invoke(this);
//            }
//            else
//            {
//                onDecalAdd?.Invoke(this);
//                onDecalPropertyChange?.Invoke(this); // Scene culling mask may have changed.
//            }
//        }
//
//#endif
//
//        void OnDisable()
//        {
//            onDecalRemove?.Invoke(this);
//
//#if UNITY_EDITOR
//            UnityEditor.SceneVisibilityManager.visibilityChanged -= UpdateDecalVisibility;
//#endif
//        }
//
//        public void OnValidate()
//        {
//            if (!isActiveAndEnabled)
//                return;
//
//            if (m_Material != m_OldMaterial)
//            {
//                onDecalMaterialChange?.Invoke(this);
//                m_OldMaterial = m_Material;
//            }
//            else
//                onDecalPropertyChange?.Invoke(this);
//        }
//
//        /// <summary>
//        /// Checks if material is valid for rendering decals.
//        /// </summary>
//        /// <returns>True if material is valid.</returns>
//        public bool IsValid()
//        {
//            if (material == null)
//                return false;
//
//            if (material.FindPass("DecalGBuffer") != -1)
//                return true;
//
//            return false;
//        }
//
//        internal static void UpdateAllDecalProperties()
//        {
//            onAllDecalPropertyChange?.Invoke();
//        }
//    }
}