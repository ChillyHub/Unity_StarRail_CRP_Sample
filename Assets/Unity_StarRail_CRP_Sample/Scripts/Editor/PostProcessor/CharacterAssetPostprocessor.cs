using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Unity_StarRail_CRP_Sample.Editor
{
    public class CharacterAssetPostprocessor : AssetPostprocessor
    {
        public static bool NeedPostprocess(string path, CharacterPreprocessorConfig config)
        {
            return Regex.IsMatch(path, $"^{config.assetPath}");
        }
        
        private void OnPostprocessModel(GameObject g)
        {
            ModelImporter importer = assetImporter as ModelImporter;

            var skinnedMeshes = g.GetComponentsInChildren<SkinnedMeshRenderer>();

            CharacterPreprocessorConfig config = CharacterPreprocessorConfig.config;

            if (!NeedPostprocess(assetPath, config))
            {
                return;
            }
            
            CharacterNormalSmoothTool.SmoothNormals(skinnedMeshes, config.writeChannel);
        }
    }
}