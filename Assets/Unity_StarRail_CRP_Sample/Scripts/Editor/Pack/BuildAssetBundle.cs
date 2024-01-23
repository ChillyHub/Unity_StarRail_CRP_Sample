using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity_StarRail_CRP_Sample.Editor
{
    public class BuildAssetBundle : ScriptableObject
    {
        [MenuItem("AssetBundle/BuildAllAssetBundles")]
        public static void BuildAllAB() 
        {
            string strABOutPAthDir = string.Empty;

            strABOutPAthDir = "Assets/Unity_StarRail_CRP_Sample/StreamingAssets";
            
            if (Directory.Exists(strABOutPAthDir) == false)
            {
                Directory.CreateDirectory(strABOutPAthDir);
            }
            
            BuildPipeline.BuildAssetBundles(strABOutPAthDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        }
    }
}