using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Unity_StarRail_CRP_Sample.Editor
{
    [CreateAssetMenu(menuName = "PreprocessorConfig")]
    public class CharacterPreprocessorConfig : ScriptableObject
    {
        [Header("Assets Properties")] [SerializeField]
        private string characterModelAssetPath;
        
        [Header("Smooth Normals")]
        public WriteChannel writeChannel = WriteChannel.Tangent;

        public string assetPath
        {
            get
            {
                if (string.IsNullOrEmpty(characterModelAssetPath))
                {
                    characterModelAssetPath = $"Assets/{Application.productName}/Characters";
                }

                return characterModelAssetPath;
            }
        }
        
        public static CharacterPreprocessorConfig config
        {
            get
            {
                if (_config == null)
                {
                    string path = $"Assets/{Application.productName}/Settings/Character_Model_Postprocessor_Config.asset";
                    _config = AssetDatabase.LoadAssetAtPath<CharacterPreprocessorConfig>(path);

                    if (_config == null)
                    {
                        _config = CreateInstance<CharacterPreprocessorConfig>();
                        var directory = $"{Application.dataPath}/{Application.productName}/Settings";
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory($"{Application.dataPath}/{Application.productName}/Settings");
                        }
                        AssetDatabase.CreateAsset(_config, path);
                        AssetDatabase.Refresh();
                    }
                }
                return _config;
            }
        }
        private static CharacterPreprocessorConfig _config;
    }
}