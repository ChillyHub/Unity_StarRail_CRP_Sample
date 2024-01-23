using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("CRP/SSGI/Screen Space Reflection", typeof(UniversalRenderPipeline))]
    public class CRPScreenSpaceReflection : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enable = new BoolParameter(false);
        public ClampedIntParameter maxIterCount = new ClampedIntParameter(32, 0, 128);
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.1f, 0.0f, 1.0f);
        
        public bool IsActive() => enable.value && maxIterCount.value > 0;

        public bool IsTileCompatible() => false;
    }
}