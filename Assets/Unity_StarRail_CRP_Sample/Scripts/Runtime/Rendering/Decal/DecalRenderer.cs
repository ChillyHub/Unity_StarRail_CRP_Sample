using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity_StarRail_CRP_Sample
{
    /// <summary>The scaling mode to apply to decals that use the Decal Projector.</summary>
    public enum DecalScaleMode
    {
        /// <summary>Ignores the transformation hierarchy and uses the scale values in the Decal Projector component directly.</summary>
        ScaleInvariant,
        /// <summary>Multiplies the lossy scale of the Transform with the Decal Projector's own scale then applies this to the decal.</summary>
        [InspectorName("Inherit from Hierarchy")]
        InheritFromHierarchy,
    }
    
    [ExecuteAlways]
#if UNITY_EDITOR
    [CanEditMultipleObjects]
#endif
    public class DecalRenderer : MonoBehaviour
    {
        internal delegate void DecalRendererAction(DecalRenderer decalRenderer);
        
        internal static event DecalRendererAction onDecalAdd;
        internal static event DecalRendererAction onDecalRemove;
        internal static event DecalRendererAction onDecalPropertyChange;
        internal static event Action onAllDecalPropertyChange;
        internal static event DecalRendererAction onDecalMaterialChange;
        internal static Material defaultMaterial { get; set; }
        internal static bool isSupported => onDecalAdd != null;

        internal DecalEntity decalEntity { get; set; }

        [SerializeField]
        private Material _material = null;
        public Material Material
        {
            get
            {
                return _material;
            }
            set
            {
                _material = value;
                OnValidate();
            }
        }

        [SerializeField]
        private float _drawDistance = 1000.0f;
        public float DrawDistance
        {
            get
            {
                return _drawDistance;
            }
            set
            {
                _drawDistance = Mathf.Max(0f, value);
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 1)]
        private float m_FadeScale = 0.9f;
        public float fadeScale
        {
            get
            {
                return m_FadeScale;
            }
            set
            {
                m_FadeScale = Mathf.Clamp01(value);
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 180)]
        private float m_StartAngleFade = 180.0f;
        public float startAngleFade
        {
            get
            {
                return m_StartAngleFade;
            }
            set
            {
                m_StartAngleFade = Mathf.Clamp(value, 0.0f, 180.0f);
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 180)]
        private float m_EndAngleFade = 180.0f;
        public float endAngleFade
        {
            get
            {
                return m_EndAngleFade;
            }
            set
            {
                m_EndAngleFade = Mathf.Clamp(value, m_StartAngleFade, 180.0f);
                OnValidate();
            }
        }

        [SerializeField]
        private Vector2 m_UVScale = new Vector2(1, 1);
        public Vector2 uvScale
        {
            get
            {
                return m_UVScale;
            }
            set
            {
                m_UVScale = value;
                OnValidate();
            }
        }

        [SerializeField]
        private Vector2 m_UVBias = new Vector2(0, 0);
        public Vector2 uvBias
        {
            get
            {
                return m_UVBias;
            }
            set
            {
                m_UVBias = value;
                OnValidate();
            }
        }

        [SerializeField]
        uint m_DecalLayerMask = 1;
        public uint renderingLayerMask
        {
            get => m_DecalLayerMask;
            set => m_DecalLayerMask = value;
        }

        [SerializeField]
        private DecalScaleMode m_ScaleMode = DecalScaleMode.ScaleInvariant;
        public DecalScaleMode scaleMode
        {
            get => m_ScaleMode;
            set
            {
                m_ScaleMode = value;
                OnValidate();
            }
        }

        [SerializeField]
        internal Vector3 m_Offset = new Vector3(0, 0, 0.5f);
        public Vector3 pivot
        {
            get
            {
                return m_Offset;
            }
            set
            {
                m_Offset = value;
                OnValidate();
            }
        }

        [SerializeField]
        internal Vector3 m_Size = new Vector3(1, 1, 1);
        public Vector3 size
        {
            get
            {
                return m_Size;
            }
            set
            {
                m_Size = value;
                OnValidate();
            }
        }

        [SerializeField]
        [Range(0, 1)]
        private float m_FadeFactor = 1.0f;
        public float fadeFactor
        {
            get
            {
                return m_FadeFactor;
            }
            set
            {
                m_FadeFactor = Mathf.Clamp01(value);
                OnValidate();
            }
        }

        private Material m_OldMaterial = null;

        /// <summary>A scale that should be used for rendering and handles.</summary>
        internal Vector3 effectiveScale => m_ScaleMode == DecalScaleMode.InheritFromHierarchy ? transform.lossyScale : Vector3.one;
        /// <summary>current size in a way the DecalSystem will be able to use it</summary>
        internal Vector3 decalSize => new Vector3(m_Size.x, m_Size.z, m_Size.y);
        /// <summary>current size in a way the DecalSystem will be able to use it</summary>
        internal Vector3 decalOffset => new Vector3(m_Offset.x, -m_Offset.z, m_Offset.y);
        /// <summary>current uv parameters in a way the DecalSystem will be able to use it</summary>
        internal Vector4 uvScaleBias => new Vector4(m_UVScale.x, m_UVScale.y, m_UVBias.x, m_UVBias.y);

        void InitMaterial()
        {
            if (_material == null)
            {
#if UNITY_EDITOR
                _material = defaultMaterial;
#endif
            }
        }

        void OnEnable()
        {
            InitMaterial();

            m_OldMaterial = _material;

            onDecalAdd?.Invoke(this);

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
                onDecalRemove?.Invoke(this);
            }
            else
            {
                onDecalAdd?.Invoke(this);
                onDecalPropertyChange?.Invoke(this); // Scene culling mask may have changed.
            }
        }

#endif

        void OnDisable()
        {
            onDecalRemove?.Invoke(this);

#if UNITY_EDITOR
            UnityEditor.SceneVisibilityManager.visibilityChanged -= UpdateDecalVisibility;
#endif
        }

        internal void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            if (_material != m_OldMaterial)
            {
                onDecalMaterialChange?.Invoke(this);
                m_OldMaterial = _material;
            }
            else
                onDecalPropertyChange?.Invoke(this);
        }

        /// <summary>
        /// Checks if material is valid for rendering decals.
        /// </summary>
        /// <returns>True if material is valid.</returns>
        public bool IsValid()
        {
            if (Material == null)
                return false;

            //if (Material.FindPass(DecalShaderPassNames.DBufferProjector) != -1)
            //    return true;
//
            //if (Material.FindPass(DecalShaderPassNames.DecalProjectorForwardEmissive) != -1)
            //    return true;
//
            //if (Material.FindPass(DecalShaderPassNames.DecalScreenSpaceProjector) != -1)
            //    return true;
//
            //if (Material.FindPass(DecalShaderPassNames.DecalGBufferProjector) != -1)
            //    return true;

            return false;
        }

        internal static void UpdateAllDecalProperties()
        {
            onAllDecalPropertyChange?.Invoke();
        }
    }
}