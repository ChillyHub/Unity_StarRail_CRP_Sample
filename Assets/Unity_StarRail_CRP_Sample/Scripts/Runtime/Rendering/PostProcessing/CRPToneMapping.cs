using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Unity_StarRail_CRP_Sample
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("CRP/Post-processing/Tone Mapping", typeof(UniversalRenderPipeline))]
    public class CRPToneMapping : VolumeComponent, IPostProcessComponent
    {
        [InspectorName("Tone Mapping Mode")]
        public CustomTonemappingModeParameter mode = new(CustomTonemappingMode.None);

        [Header("ACES Parameters")]
        [DisplayInfo(name = "Param A"), AdditionalProperty] public FloatParameter ACESParamA = new(2.80f);
        [DisplayInfo(name = "Param B"), AdditionalProperty] public FloatParameter ACESParamB = new(0.40f);
        [DisplayInfo(name = "Param C"), AdditionalProperty] public FloatParameter ACESParamC = new(2.10f);
        [DisplayInfo(name = "Param D"), AdditionalProperty] public FloatParameter ACESParamD = new(0.50f);
        [DisplayInfo(name = "Param E"), AdditionalProperty] public FloatParameter ACESParamE = new(1.50f);

        public bool IsActive() => mode.value != CustomTonemappingMode.None;
        
        public bool IsTileCompatible() => true;
    }
    
    public enum CustomTonemappingMode
    {
        None = 0,
        ACES = 1,
    }

    [Serializable]
    public sealed class CustomTonemappingModeParameter : VolumeParameter<CustomTonemappingMode>
    {
        public CustomTonemappingModeParameter(CustomTonemappingMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}