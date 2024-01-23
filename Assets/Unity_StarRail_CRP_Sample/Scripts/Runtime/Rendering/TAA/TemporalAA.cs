using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("CRP/Anti-aliasing/Temporal Anti-aliasing", typeof(UniversalRenderPipeline))]
    public class TemporalAA : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new BoolParameter(false);
        
        public bool IsActive() => enabled.value;
        
        public bool IsTileCompatible() => false;
    }
}