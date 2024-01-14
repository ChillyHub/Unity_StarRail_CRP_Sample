using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("CRP/SSGI/Screen Space Reflection", typeof(UniversalRenderPipeline))]
    public class CRPScreenSpaceReflection : VolumeComponent, IPostProcessComponent
    {
        
        
        public bool IsActive() => false;

        public bool IsTileCompatible() => false;
    }
}