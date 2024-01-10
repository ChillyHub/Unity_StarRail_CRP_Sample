using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Unity_StarRail_CRP_Sample
{
    public enum DirectionMode
    {
        Fixed,
        FromVolume,
        FromDirectionLight,
        FromPointLight
    }
    
    [ExecuteAlways]
    public class CRPCharacterAdditionalRenderer : MonoBehaviour
    {
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
        
        public DirectionMode mainLightDirectionMode = DirectionMode.Fixed;
        public DirectionMode shadowLightDirectionMode = DirectionMode.Fixed;
        
        public Light mainDirectionLight;
        public Light shadowDirectionLight;
        
        public Light mainPointLight;
        public Light shadowPointLight;
        
        public Vector3 mainLightRotation = Vector3.zero;
        public Vector3 shadowLightDirection = Vector3.forward;
        
        [ColorUsage(true, true)]
        public Color mainLightColor = Color.white;

        public GameObject headBinding;

        public bool overrideKeywords = false; 
        
        public KeywordsValues keywordsValues = new KeywordsValues();
        
        private KeywordsValues _shaderKeywords = new KeywordsValues();

        private CharacterInfo _info;
        private MeshRenderer[] _meshRenderers;
        private SkinnedMeshRenderer[] _skinnedMeshRenderers;
        
        private bool _overrideKeywords = false;

        private void Init()
        {
            _info = new CharacterInfo
            {
                Position = transform.position
            };

            _meshRenderers = GetComponentsInChildren<MeshRenderer>();
            _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            _info.AABB = new Bounds();
            for (int i = 0; i < _meshRenderers.Length; i++)
            {
                if (i == 0)
                {
                    _info.AABB = _meshRenderers[i].bounds;
                }
                else
                {
                    _info.AABB.Encapsulate(_meshRenderers[i].bounds);
                }
            }

            for (int i = 0; i < _skinnedMeshRenderers.Length; i++)
            {
                if (i == 0 && _meshRenderers.Length == 0)
                {
                    _info.AABB = _skinnedMeshRenderers[i].sharedMesh.bounds;
                }
                else
                {
                    _info.AABB.Encapsulate(_skinnedMeshRenderers[i].sharedMesh.bounds);
                }
            }
            
            _info.AABB.center -= _info.Position;

            _info.Type = characterType;
            
            CharacterManager.instance.AddCharacterInfo(gameObject, _info);
        }

        private void Update()
        {
            if (!CharacterManager.instance.CharacterInfos.ContainsKey(gameObject))
            {
                return;
            }

            CharacterInfo info = CharacterManager.instance.CharacterInfos[gameObject];

            info.Position = transform.position;

            switch (mainLightDirectionMode)
            {
                case DirectionMode.Fixed:
                    Quaternion rotation = Quaternion.Euler(mainLightRotation);
                    info.MainLightDirection = rotation * Vector3.up;
                    info.MainLightColor = mainLightColor;
                    break;
                case DirectionMode.FromVolume:
                    var setting = VolumeManager.instance.stack.GetComponent<LightSettingVolume>();
                    if (setting != null)
                    {
                        info.MainLightDirection = setting.GetMainLightDirection(transform.position);
                        info.MainLightColor = setting.GetMainLightColor();
                    }
                    break;
                case DirectionMode.FromDirectionLight:
                    if (mainDirectionLight != null)
                    {
                        info.MainLightDirection = mainDirectionLight.transform.forward;
                        info.MainLightColor = mainDirectionLight.color;
                    }
                    break;
                case DirectionMode.FromPointLight:
                    if (mainPointLight != null)
                    {
                        info.MainLightDirection = (mainPointLight.transform.position - transform.position).normalized;
                        info.MainLightColor = mainPointLight.color;
                    }
                    break;
            }
            
            switch (shadowLightDirectionMode)
            {
                case DirectionMode.Fixed:
                    info.ShadowLightDirection = shadowLightDirection;
                    break;
                case DirectionMode.FromVolume:
                    var setting = VolumeManager.instance.stack.GetComponent<LightSettingVolume>();
                    if (setting != null)
                    {
                        info.ShadowLightDirection = setting.GetShadowLightDirection(transform.position);
                    }
                    break;
                case DirectionMode.FromDirectionLight:
                    if (shadowDirectionLight != null)
                    {
                        info.ShadowLightDirection = shadowDirectionLight.transform.forward;
                    }
                    break;
                case DirectionMode.FromPointLight:
                    if (shadowPointLight != null)
                    {
                        info.ShadowLightDirection = (shadowPointLight.transform.position - transform.position).normalized;
                    }
                    break;
            }

            CharacterManager.instance.CharacterInfos[gameObject] = info;
            
            SetMaterial();
        }
        
        private void SetMaterial()
        {
            for (int i = 0; i < _skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer meshRenderer = _skinnedMeshRenderers[i];
                foreach (var material in meshRenderer.sharedMaterials)
                {
                    if (headBinding != null)
                    {
                        material.SetVector(MaterialConstants.HeadCenterId, headBinding.transform.position);
                        material.SetVector(MaterialConstants.HeadForwardId, headBinding.transform.up);
                        material.SetVector(MaterialConstants.HeadRightId, -headBinding.transform.forward);
                        material.SetVector(MaterialConstants.HeadUpId, -headBinding.transform.right);
                    }
                    
                    material.SetVector(MaterialConstants.CharMainLightDirectionId, 
                        CharacterManager.instance.CharacterInfos[gameObject].MainLightDirection);
                    material.SetVector(MaterialConstants.CharMainLightColorId, 
                        CharacterManager.instance.CharacterInfos[gameObject].MainLightColor);
                    material.SetFloat(MaterialConstants.DayTimeId, 12.0f);

#if UNITY_EDITOR
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
#endif
                }
            }

            _overrideKeywords = overrideKeywords;
        }

        private void OnDestroy()
        {
            CharacterManager.instance.RemoveCharacterInfo(gameObject);
        }

        private void OnEnable()
        {
            Init();
        }
        
        private void OnDisable()
        {
            OnDestroy();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            OnDestroy();
            Init();
        }
#endif
        
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