using System;
using UnityEngine;

namespace Unity_StarRail_CRP_Sample
{
    public enum DirectionMode
    {
        Fixed,
        FromVolume,
        FromDirectionLight,
        FromPointLight
    }

    public enum CharacterType
    {
        Player,
        Character,
        NPC
    }
    
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterAdditionalRenderer : MonoBehaviour
    {
        internal delegate void CharacterAction(CharacterAdditionalRenderer character);
        
        internal static event CharacterAction OnCharacterAdd;
        internal static event CharacterAction OnCharacterRemove;

        private static class MaterialConstants
        {
            public static readonly int CharMainLightDirectionId = Shader.PropertyToID("_CharMainLightDirection");
            public static readonly int CharMainLightColorId = Shader.PropertyToID("_CharMainLightColor");
            public static readonly int HeadCenterId = Shader.PropertyToID("_HeadCenter");
            public static readonly int HeadForwardId = Shader.PropertyToID("_HeadForward");
            public static readonly int HeadRightId = Shader.PropertyToID("_HeadRight");
            public static readonly int HeadUpId = Shader.PropertyToID("_HeadUp");
            public static readonly int DayTimeId = Shader.PropertyToID("_DayTime");
        }
        
        private static class MaterialKeywords
        {
            public static readonly string _ENABLE_GI = "_ENABLE_GI";
            public static readonly string _ENABLE_ADDITIONAL_LIGHT = "_ENABLE_ADDITIONAL_LIGHT";
            public static readonly string _ENABLE_DIFFUSE = "_ENABLE_DIFFUSE";
            public static readonly string _ENABLE_SPECULAR = "_ENABLE_SPECULAR";
            public static readonly string _ENABLE_EMISSION = "_ENABLE_EMISSION";
            public static readonly string _ENABLE_RIM = "_ENABLE_RIM";
            public static readonly string _ENABLE_OUTLINE = "_ENABLE_OUTLINE";
            public static readonly string _WITH_STOCKING = "_WITH_STOCKING";
        }
        
        [Serializable]
        public class KeywordsValues
        {
            public bool enableGI = true;
            public bool enableAdditionalLight = true;
            public bool enableDiffuse = true;
            public bool enableSpecular = true;
            public bool enableEmission = true;
            public bool enableRim = true;
            public bool enableOutline = true;
            public bool enableStocking = true;
        }
        
        public CharacterType characterType = CharacterType.Player;
        
        public DirectionMode mainLightDirectionMode = DirectionMode.FromVolume;
        public DirectionMode shadowLightDirectionMode = DirectionMode.FromVolume;
        
        public Light mainDirectionLight;
        public Light shadowDirectionLight;
        
        public Light mainPointLight;
        public Light shadowPointLight;
        
        public Vector3 mainLightRotation = Vector3.zero;
        public Vector3 shadowLightDirection = Vector3.forward;
        
        public bool overrideColor = false;
        
        [ColorUsage(true, true)]
        public Color mainLightColor = Color.white;
        [ColorUsage(true, true)]
        public Color mainLightOverrideColor = Color.white;

        public GameObject headBinding;

        public bool overrideKeywords = false; 
        
        public KeywordsValues keywordsValues = new KeywordsValues();
        
        private KeywordsValues _shaderKeywords = new KeywordsValues();
        
        private CharacterType _prevCharacterType = CharacterType.Player;
        private bool _overrideKeywords = false;

        private SkinnedMeshRenderer[] _skinnedMeshRenderers;
        internal CharacterEntity CharacterEntity { get; set; }

        private void OnEnable()
        {
            _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            OnCharacterAdd?.Invoke(this);
        }

        private void OnDisable()
        {
            OnCharacterRemove?.Invoke(this);
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (characterType != _prevCharacterType)
            {
                _prevCharacterType = characterType;
                
                OnCharacterRemove?.Invoke(this);
                OnCharacterAdd?.Invoke(this);
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            for (int i = 0; i < _skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer meshRenderer = _skinnedMeshRenderers[i];

                foreach (var material in meshRenderer.sharedMaterials)
                {
                    if (overrideKeywords)
                    {
                        SetKeywords(material);
                    }
                    else if (_overrideKeywords == true)
                    {
                        ResolveKeywords(material);
                    }
                    else
                    {
                        SaveKeywords(material);
                    }
                }
            }

            _overrideKeywords = overrideKeywords;
#endif
        }
        
        private void SetKeywords(Material material)
        {
            SetKeyword(material, MaterialKeywords._ENABLE_GI, keywordsValues.enableGI);
            SetKeyword(material, MaterialKeywords._ENABLE_ADDITIONAL_LIGHT, keywordsValues.enableAdditionalLight);
            SetKeyword(material, MaterialKeywords._ENABLE_DIFFUSE, keywordsValues.enableDiffuse);
            SetKeyword(material, MaterialKeywords._ENABLE_SPECULAR, keywordsValues.enableSpecular);
            SetKeyword(material, MaterialKeywords._ENABLE_EMISSION, keywordsValues.enableEmission);
            SetKeyword(material, MaterialKeywords._ENABLE_RIM, keywordsValues.enableRim);
            SetKeyword(material, MaterialKeywords._ENABLE_OUTLINE, keywordsValues.enableOutline);
            SetKeyword(material, MaterialKeywords._WITH_STOCKING, keywordsValues.enableStocking);
        }

        private void ResolveKeywords(Material material)
        {
            SetKeyword(material, MaterialKeywords._ENABLE_GI, _shaderKeywords.enableGI);
            SetKeyword(material, MaterialKeywords._ENABLE_ADDITIONAL_LIGHT, _shaderKeywords.enableAdditionalLight);
            SetKeyword(material, MaterialKeywords._ENABLE_DIFFUSE, _shaderKeywords.enableDiffuse);
            SetKeyword(material, MaterialKeywords._ENABLE_SPECULAR, _shaderKeywords.enableSpecular);
            SetKeyword(material, MaterialKeywords._ENABLE_EMISSION, _shaderKeywords.enableEmission);
            SetKeyword(material, MaterialKeywords._ENABLE_RIM, _shaderKeywords.enableRim);
            SetKeyword(material, MaterialKeywords._ENABLE_OUTLINE, _shaderKeywords.enableOutline);
            SetKeyword(material, MaterialKeywords._WITH_STOCKING, _shaderKeywords.enableStocking);
        }

        private void SaveKeywords(Material material)
        {
            _shaderKeywords.enableGI = GetKeyword(material, MaterialKeywords._ENABLE_GI);
            _shaderKeywords.enableAdditionalLight = GetKeyword(material, MaterialKeywords._ENABLE_ADDITIONAL_LIGHT);
            _shaderKeywords.enableDiffuse = GetKeyword(material, MaterialKeywords._ENABLE_DIFFUSE);
            _shaderKeywords.enableSpecular = GetKeyword(material, MaterialKeywords._ENABLE_SPECULAR);
            _shaderKeywords.enableEmission = GetKeyword(material, MaterialKeywords._ENABLE_EMISSION);
            _shaderKeywords.enableRim = GetKeyword(material, MaterialKeywords._ENABLE_RIM);
            _shaderKeywords.enableOutline = GetKeyword(material, MaterialKeywords._ENABLE_OUTLINE);
            _shaderKeywords.enableStocking = GetKeyword(material, MaterialKeywords._WITH_STOCKING);
        }

        private void SetKeyword(Material material, string keyword, bool val)
        {
            if (val)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private bool GetKeyword(Material material, string keyword)
        {
            return material.IsKeywordEnabled(keyword);
        }
    }
}