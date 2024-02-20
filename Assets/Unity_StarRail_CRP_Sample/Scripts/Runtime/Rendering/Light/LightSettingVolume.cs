using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Unity_StarRail_CRP_Sample
{
    public enum LightSettingMode
    {
        VirtualFixed,
        FromVirtualObject,
        FromLightObject
    }

    public struct LightSettingLerpInfo
    {
        public LightSettingMode Mode1;
        public LightSettingMode Mode2;
        public float T;
    }
    
    public struct RotationLerpInfo
    {
        public Vector3 Rotation1;
        public Vector3 Rotation2;
        public float T;
    }
    
    public struct ColorLerpInfo
    {
        public Color Color1;
        public Color Color2;
        public float T;
    }
    
    public struct ClampFloatLerpInfo
    {
        public float Value1;
        public float Value2;
        public float T;
    }
    
    public struct LightLerpInfo
    {
        public Light Light1;
        public Light Light2;
        public float T;
    }
    
    public struct GameObjectLerpInfo
    {
        public GameObject Light1;
        public GameObject Light2;
        public float T;
    }

    [Serializable]
    public class LightSettingVolume : VolumeComponent
    {
        public LightSettingModeParameter mainLightMode = new LightSettingModeParameter(LightSettingMode.VirtualFixed);
        public LightSettingModeParameter shadowLightMode = new LightSettingModeParameter(LightSettingMode.VirtualFixed);
        
        public RotationParameter mainLightRotation = new RotationParameter(Vector3.zero);
        public CustomColorParameter mainLightColor = new CustomColorParameter(Color.white, true, true, true);
        public RotationParameter shadowLightRotation = new RotationParameter(Vector3.zero);
        
        public LightObjectParameter mainLight = new LightObjectParameter(null);
        public LightObjectParameter shadowLight = new LightObjectParameter(null);
        public ClampedFloatParameter mainLightIntensity = new ClampedFloatParameter(1.0f, 0.0f, 10.0f);
        
        public GameObjectParameter mainVirtualLight = new GameObjectParameter(null);
        public GameObjectParameter shadowVirtualLight = new GameObjectParameter(null);
        public CustomColorParameter overrideMainLightColor = new CustomColorParameter(Color.white, true, true, true);

        public Vector3 GetMainLightDirection(Vector3 currPosition)
        {
            LightSettingLerpInfo mainLightSettingLerpInfo = mainLightMode.LightSettingLerpInfo;

            Vector3 direction1 = Vector3.up;
            Vector3 direction2 = Vector3.up;
            float t = mainLightSettingLerpInfo.T;

            switch (mainLightSettingLerpInfo.Mode1)
            {
                case LightSettingMode.VirtualFixed:
                    direction1 = mainLightRotation.Rotation1ToDirection();
                    break;
                case LightSettingMode.FromVirtualObject:
                    direction1 = mainVirtualLight.GetLight1Direction(currPosition);
                    break;
                case LightSettingMode.FromLightObject:
                    direction1 = mainLight.GetLight1Direction(currPosition);
                    break;
            }

            switch (mainLightSettingLerpInfo.Mode2)
            {
                case LightSettingMode.VirtualFixed:
                    direction2 = mainLightRotation.Rotation2ToDirection();
                    break;
                case LightSettingMode.FromVirtualObject:
                    direction2 = mainVirtualLight.GetLight2Direction(currPosition);
                    break;
                case LightSettingMode.FromLightObject:
                    direction2 = mainLight.GetLight2Direction(currPosition);
                    break;
            }
            
            //Debug.Log($"Direction: {Vector3.Lerp(direction1, direction2, t)}");

            return Vector3.Lerp(direction1, direction2, t);

            //switch (mainLightMode.value)
            //{
            //    case LightSettingMode.VirtualFixed:
            //        Quaternion quaternion = Quaternion.Euler(mainLightRotation.value);
            //        return quaternion * Vector3.up;
            //    case LightSettingMode.FromVirtualObject:
            //        return mainVirtualLight.GetLightDirection(currPosition);
            //    case LightSettingMode.FromLightObject:
            //        return mainLight.GetLightDirection(currPosition);
            //    default:
            //        return Vector3.zero;
            //}
        }
        
        public Vector3 GetShadowLightDirection(Vector3 currPosition)
        {
            LightSettingLerpInfo shadowLightSettingLerpInfo = shadowLightMode.LightSettingLerpInfo;
            
            Vector3 direction1 = Vector3.up;
            Vector3 direction2 = Vector3.up;
            float t = shadowLightSettingLerpInfo.T;
            
            switch (shadowLightSettingLerpInfo.Mode1)
            {
                case LightSettingMode.VirtualFixed:
                    direction1 = shadowLightRotation.Rotation1ToDirection();
                    break;
                case LightSettingMode.FromVirtualObject:
                    direction1 = shadowVirtualLight.GetLight1Direction(currPosition);
                    break;
                case LightSettingMode.FromLightObject:
                    direction1 = shadowLight.GetLight1Direction(currPosition);
                    break;
            }
            
            switch (shadowLightSettingLerpInfo.Mode2)
            {
                case LightSettingMode.VirtualFixed:
                    direction2 = shadowLightRotation.Rotation2ToDirection();
                    break;
                case LightSettingMode.FromVirtualObject:
                    direction2 = shadowVirtualLight.GetLight2Direction(currPosition);
                    break;
                case LightSettingMode.FromLightObject:
                    direction2 = shadowLight.GetLight2Direction(currPosition);
                    break;
            }
            
            //Debug.Log($"Direction1: {direction1}, Direction2: {direction2}, T: {t}");
            //Debug.Log($"Mode1: {shadowLightSettingLerpInfo.Mode1}, Mode2: {shadowLightSettingLerpInfo.Mode2}, T: {t}");
            
            return Vector3.Lerp(direction1, direction2, t);

            //switch (shadowLightMode.value)
            //{
            //    case LightSettingMode.VirtualFixed:
            //        Quaternion quaternion = Quaternion.Euler(shadowLightRotation.value);
            //        return quaternion * Vector3.up;
            //    case LightSettingMode.FromVirtualObject:
            //        return shadowVirtualLight.GetLightDirection(currPosition);
            //    case LightSettingMode.FromLightObject:
            //        return shadowLight.GetLightDirection(currPosition);
            //    default:
            //        return Vector3.zero;
            //}
        }
        
        public Color GetMainLightColor()
        {
            LightSettingLerpInfo mainLightSettingLerpInfo = mainLightMode.LightSettingLerpInfo;
            
            Color color1 = Color.white;
            Color color2 = Color.white;
            float t = mainLightSettingLerpInfo.T;
            
            switch (mainLightSettingLerpInfo.Mode1)
            {
                case LightSettingMode.VirtualFixed:
                    color1 = mainLightColor.ColorLerpInfo.Color1;
                    break;
                case LightSettingMode.FromVirtualObject:
                    color1 = overrideMainLightColor.ColorLerpInfo.Color1;
                    break;
                case LightSettingMode.FromLightObject:
                    color1 = mainLight.GetLight1Color();// * mainLightIntensity.value;
                    break;
            }
            
            switch (mainLightSettingLerpInfo.Mode2)
            {
                case LightSettingMode.VirtualFixed:
                    color2 = mainLightColor.ColorLerpInfo.Color2;
                    break;
                case LightSettingMode.FromVirtualObject:
                    color2 = overrideMainLightColor.ColorLerpInfo.Color2;
                    break;
                case LightSettingMode.FromLightObject:
                    color2 = mainLight.GetLight2Color();// * mainLightIntensity.value;
                    break;
            }
            
            return Color.Lerp(color1, color2, t);
            
            //switch (mainLightMode.value)
            //{
            //    case LightSettingMode.VirtualFixed:
            //        return mainLightColor.value;
            //    case LightSettingMode.FromVirtualObject:
            //        return overrideMainLightColor.value;
            //    case LightSettingMode.FromLightObject:
            //        return mainLight.GetLightColor() * mainLightIntensity.value;
            //    default:
            //        return Color.white;
            //}
        }
    }
    
    [Serializable]
    public class LightSettingModeParameter : VolumeParameter<LightSettingMode>
    {
        private LightSettingLerpInfo _lightSettingLerpInfo = new LightSettingLerpInfo();
        
        public LightSettingLerpInfo LightSettingLerpInfo => _lightSettingLerpInfo;
        
        public LightSettingModeParameter(LightSettingMode value, bool overrideState = false) 
            : base(value, overrideState) { }
        
        public override void Interp(LightSettingMode from, LightSettingMode to, float t)
        {
            _lightSettingLerpInfo = new LightSettingLerpInfo()
            {
                Mode1 = from,
                Mode2 = to,
                T = t
            };
        }
    }
    
    [Serializable]
    public class RotationParameter : VolumeParameter<Vector3>
    {
        private RotationLerpInfo _rotationLerpInfo = new RotationLerpInfo();
        
        public RotationLerpInfo RotationLerpInfo => _rotationLerpInfo;
        
        public RotationParameter(Vector3 value, bool overrideState = false) 
            : base(value, overrideState) { }
        
        public override void Interp(Vector3 from, Vector3 to, float t)
        {
            //Debug.Log($"Rotation1: {from}, Rotation2: {to}, T: {_rotationLerpInfo.T}");
            _rotationLerpInfo = new RotationLerpInfo()
            {
                Rotation1 = from,
                Rotation2 = to,
                T = t
            };
        }

        public override object Clone()
        {
            var t = new RotationParameter(GetValue<Vector3>(), overrideState)
            { 
                _rotationLerpInfo = RotationLerpInfo
            };

            return t;
        }

        public Vector3 Rotation1ToDirection()
        {
            //Debug.Log($"Rotation1: {_rotationLerpInfo.Rotation1}");
            Quaternion quaternion = Quaternion.Euler(_rotationLerpInfo.Rotation1);
            return quaternion * Vector3.up;
        }
        
        public Vector3 Rotation2ToDirection()
        {
            //Debug.Log($"Rotation2: {_rotationLerpInfo.Rotation2}");
            Quaternion quaternion = Quaternion.Euler(_rotationLerpInfo.Rotation2);
            return quaternion * Vector3.up;
        }
    }

    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class CustomColorParameter : ColorParameter
    {
        private ColorLerpInfo _colorLerpInfo = new ColorLerpInfo();
        
        public ColorLerpInfo ColorLerpInfo => _colorLerpInfo;
        
        public CustomColorParameter(Color value, bool overrideState = false, bool hdr = false, bool showAlpha = true) 
            : base(value, overrideState, hdr, showAlpha) { }
        
        public override void Interp(Color from, Color to, float t)
        {
            _colorLerpInfo = new ColorLerpInfo()
            {
                Color1 = from,
                Color2 = to,
                T = t
            };
            
            m_Value.r = from.r + (to.r - from.r) * t;
            m_Value.g = from.g + (to.g - from.g) * t;
            m_Value.b = from.b + (to.b - from.b) * t;
            m_Value.a = from.a + (to.a - from.a) * t;
        }
    }
    
    [Serializable]
    public class ClampFloatLerpParameter : ClampedFloatParameter
    {
        private ClampFloatLerpInfo _clampFloatLerpInfo = new ClampFloatLerpInfo();
        
        public ClampFloatLerpInfo ClampFloatLerpInfo => _clampFloatLerpInfo;
        
        public ClampFloatLerpParameter(float value, float min, float max, bool overrideState = false) 
            : base(value, min, max, overrideState) { }
    }

    [Serializable]
    public class LightObjectParameter : VolumeParameter<Light>
    {
        private LightLerpInfo _lightLerpInfo = new LightLerpInfo();
        
        public LightLerpInfo LightLerpInfo => _lightLerpInfo;
        
        public LightObjectParameter(Light value, bool overrideState = false) 
            : base(value, overrideState) { }

        //public Vector3 GetLightDirection(Vector3 currPosition, Vector3 fd1, Vector3 fd2)
        //{
        //    Light light1 = _lightLerpInfo.Light1;
        //    Light light2 = _lightLerpInfo.Light2;
        //    float t = _lightLerpInfo.T;
//
        //    if (light1 == null && light2 == null)
        //    {
        //        return Vector3.Lerp(fd1, fd2, t).normalized;
        //    }
//
        //    if (light1 == null && light2.type == LightType.Directional)
        //    {
        //        return Vector3.Lerp(fd1, light2.transform.forward, t).normalized;
        //    }
//
        //    if (light1 == null && (light2.type == LightType.Point || light2.type == LightType.Spot))
        //    {
        //        return Vector3.Lerp(fd1, (light2.transform.position - currPosition).normalized, t).normalized;
        //    }
//
        //    if (light2 == null && light1.type == LightType.Directional)
        //    {
        //        return Vector3.Lerp(light1.transform.forward, fd2, t).normalized;
        //    }
        //    
        //    if (light2 == null && (light1.type == LightType.Point || light1.type == LightType.Spot))
        //    {
        //        return Vector3.Lerp((light1.transform.position - currPosition).normalized, fd2, t).normalized;
        //    }
//
        //    if (light1.type == LightType.Directional && light2.type == LightType.Directional)
        //    {
        //        return Vector3.Lerp(light1.transform.forward, light2.transform.forward, t).normalized;
        //    }
//
        //    if ((light1.type == LightType.Point || light1.type == LightType.Spot) &&
        //        (light2.type == LightType.Point || light2.type == LightType.Spot))
        //    {
        //        Vector3 pos1 = light1.transform.position;
        //        Vector3 pos2 = light2.transform.position;
        //        return (Vector3.Lerp(pos1, pos2, t) - currPosition).normalized;
        //    }
//
        //    if (light1.type == LightType.Directional && (light2.type == LightType.Point || light2.type == LightType.Spot))
        //    {
        //        return Vector3.Lerp(light1.transform.forward, (light2.transform.position - currPosition).normalized, t).normalized;
        //    }
//
        //    if (light2.type == LightType.Directional && (light1.type == LightType.Point || light1.type == LightType.Spot))
        //    {
        //        return Vector3.Lerp((light1.transform.position - currPosition).normalized, light2.transform.forward, t).normalized;
        //    }
//
        //    return Vector3.up;
        //}
//
        //public Color GetLightColor()
        //{
        //    Light light1 = _lightLerpInfo.Light1;
        //    Light light2 = _lightLerpInfo.Light2;
        //    float t = _lightLerpInfo.T;
        //    
        //    if (light1 == null && light2 == null)
        //    {
        //        return Color.white;
        //    }
//
        //    if (light1 == null && light2 != null)
        //    {
        //        return light2.color;
        //    }
//
        //    if (light1 != null && light2 == null)
        //    {
        //        return light1.color;
        //    }
        //    
        //    return Color.Lerp(light1.color, light2.color, t);
        //}

        public override void Interp(Light from, Light to, float t)
        {
            _lightLerpInfo = new LightLerpInfo()
            {
                Light1 = from,
                Light2 = to,
                T = t
            };
        }
        
        public Vector3 GetLight1Direction(Vector3 currPosition)
        {
            Light light1 = _lightLerpInfo.Light1;
            
            if (light1 == null)
            {
                return Vector3.up;
            }

            if (light1.type == LightType.Directional)
            {
                return -light1.transform.forward;
            }

            return (light1.transform.position - currPosition).normalized;
        }
        
        public Vector3 GetLight2Direction(Vector3 currPosition)
        {
            Light light2 = _lightLerpInfo.Light2;
            
            if (light2 == null)
            {
                return Vector3.up;
            }

            if (light2.type == LightType.Directional)
            {
                return -light2.transform.forward;
            }

            return (light2.transform.position - currPosition).normalized;
        }
        
        public Color GetLight1Color()
        {
            Light light1 = _lightLerpInfo.Light1;
            
            if (light1 == null)
            {
                return Color.white;
            }

            return light1.color;
        }
        
        public Color GetLight2Color()
        {
            Light light2 = _lightLerpInfo.Light2;
            
            if (light2 == null)
            {
                return Color.white;
            }

            return light2.color;
        }
    }

    [Serializable]
    public class GameObjectParameter : VolumeParameter<GameObject>
    {
        private GameObjectLerpInfo _gameObjectLerpInfo = new GameObjectLerpInfo();
        
        public GameObjectLerpInfo GameObjectLerpInfo => _gameObjectLerpInfo;
        
        public GameObjectParameter(GameObject value, bool overrideState = false) 
            : base(value, overrideState) { }

        //public Vector3 GetLightDirection(Vector3 currPosition, Vector3 fd1, Vector3 fd2)
        //{
        //    GameObject light1 = _gameObjectLerpInfo.Light1;
        //    GameObject light2 = _gameObjectLerpInfo.Light2;
        //    float t = _gameObjectLerpInfo.T;
//
        //    if (light1 == null && light2 == null)
        //    {
        //        return Vector3.Lerp(fd1, fd2, t).normalized;
        //    }
//
        //    if (light1 == null)
        //    {
        //        return Vector3.Lerp(fd1, (light2.transform.position - currPosition).normalized, t).normalized;
        //    }
//
        //    if (light2 == null)
        //    {
        //        return Vector3.Lerp((light1.transform.position - currPosition).normalized, fd2, t).normalized;
        //    }
//
        //    Vector3 pos1 = light1.transform.position;
        //    Vector3 pos2 = light2.transform.position;
        //    
        //    return (Vector3.Lerp(pos1, pos2, t) - currPosition).normalized;
        //}
        
        public override void Interp(GameObject from, GameObject to, float t)
        {
            _gameObjectLerpInfo = new GameObjectLerpInfo()
            {
                Light1 = from,
                Light2 = to,
                T = t
            };
        }
        
        public Vector3 GetLight1Direction(Vector3 currPosition)
        {
            GameObject light1 = _gameObjectLerpInfo.Light1;
            
            if (light1 == null)
            {
                return Vector3.up;
            }

            return (light1.transform.position - currPosition).normalized;
        }
        
        public Vector3 GetLight2Direction(Vector3 currPosition)
        {
            GameObject light2 = _gameObjectLerpInfo.Light2;
            
            if (light2 == null)
            {
                return Vector3.up;
            }

            return (light2.transform.position - currPosition).normalized;
        }
    }
}
