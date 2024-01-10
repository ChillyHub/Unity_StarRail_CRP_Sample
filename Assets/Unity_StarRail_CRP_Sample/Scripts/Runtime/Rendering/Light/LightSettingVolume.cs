using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public enum LightSettingMode
    {
        VirtualFixed,
        FromLightObject
    }
    
    [Serializable]
    public class LightSettingVolume : VolumeComponent
    {
        public LightSettingModeParameter mainLightMode = new LightSettingModeParameter(LightSettingMode.VirtualFixed);
        public LightSettingModeParameter shadowLightMode = new LightSettingModeParameter(LightSettingMode.VirtualFixed);
        
        public Vector3Parameter mainLightRotation = new Vector3Parameter(Vector3.zero);
        public ColorParameter mainLightColor = new ColorParameter(Color.white, true, false, true);
        
        public Vector3Parameter shadowLightRotation = new Vector3Parameter(Vector3.zero);
        
        public LightObjectParameter mainLight = new LightObjectParameter(null);
        public LightObjectParameter shadowLight = new LightObjectParameter(null);
        
        public Vector3 GetMainLightDirection(Vector3 currPosition)
        {
            switch (mainLightMode.value)
            {
                case LightSettingMode.VirtualFixed:
                    Quaternion quaternion = Quaternion.Euler(mainLightRotation.value);
                    return quaternion * Vector3.up;
                case LightSettingMode.FromLightObject:
                    return mainLight.GetLightDirection(currPosition);
                default:
                    return Vector3.zero;
            }
        }
        
        public Vector3 GetShadowLightDirection(Vector3 currPosition)
        {
            switch (shadowLightMode.value)
            {
                case LightSettingMode.VirtualFixed:
                    Quaternion quaternion = Quaternion.Euler(shadowLightRotation.value);
                    return quaternion * Vector3.up;
                case LightSettingMode.FromLightObject:
                    return shadowLight.GetLightDirection(currPosition);
                default:
                    return Vector3.zero;
            }
        }
        
        public Color GetMainLightColor()
        {
            switch (mainLightMode.value)
            {
                case LightSettingMode.VirtualFixed:
                    return mainLightColor.value;
                case LightSettingMode.FromLightObject:
                    return mainLight.value?.color ?? Color.white;
                default:
                    return Color.white;
            }
        }
    }
    
    [Serializable]
    public class LightSettingModeParameter : VolumeParameter<LightSettingMode>
    {
        public LightSettingModeParameter(LightSettingMode value, bool overrideState = false) 
            : base(value, overrideState) { }
    }

    [Serializable]
    public class LightObjectParameter : VolumeParameter<Light>
    {
        public LightObjectParameter(Light value, bool overrideState = false) 
            : base(value, overrideState) { }

        public Vector3 GetLightDirection(Vector3 currPosition)
        {
            Light light1 = _lightsInfo.Light1;
            Light light2 = _lightsInfo.Light2;
            float t = _lightsInfo.T;

            if (light1 == null && light2 == null)
            {
                return Vector3.up;
            }

            if (light1 == null && light2.type == LightType.Directional)
            {
                return Vector3.Lerp(Vector3.up, light2.transform.forward, t).normalized;
            }

            if (light1 == null && (light2.type == LightType.Point || light2.type == LightType.Spot))
            {
                return Vector3.Lerp(Vector3.up, (light2.transform.position - currPosition).normalized, t).normalized;
            }

            if (light2 == null && light1.type == LightType.Directional)
            {
                return Vector3.Lerp(light1.transform.forward, Vector3.up, t).normalized;
            }
            
            if (light2 == null && (light1.type == LightType.Point || light1.type == LightType.Spot))
            {
                return Vector3.Lerp((light1.transform.position - currPosition).normalized, Vector3.up, t).normalized;
            }

            if (light1.type == LightType.Directional && light2.type == LightType.Directional)
            {
                return Vector3.Lerp(light1.transform.forward, light2.transform.forward, t).normalized;
            }

            if ((light1.type == LightType.Point || light1.type == LightType.Spot) &&
                (light2.type == LightType.Point || light2.type == LightType.Spot))
            {
                Vector3 pos1 = light1.transform.position;
                Vector3 pos2 = light2.transform.position;
                return (Vector3.Lerp(pos1, pos2, t) - currPosition).normalized;
            }

            if (light1.type == LightType.Directional && (light2.type == LightType.Point || light2.type == LightType.Spot))
            {
                return Vector3.Lerp(light1.transform.forward, (light2.transform.position - currPosition).normalized, t).normalized;
            }

            if (light2.type == LightType.Directional && (light1.type == LightType.Point || light1.type == LightType.Spot))
            {
                return Vector3.Lerp((light1.transform.position - currPosition).normalized, light2.transform.forward, t).normalized;
            }

            return Vector3.up;
        }
        
        public class LightsInfo
        {
            internal Light Light1;
            internal Light Light2;
            internal float T;
        }
        private LightsInfo _lightsInfo = new LightsInfo();

        public override void Interp(Light from, Light to, float t)
        {
            _lightsInfo = new LightsInfo()
            {
                Light1 = from,
                Light2 = to,
                T = t
            };
        }
    }
}
