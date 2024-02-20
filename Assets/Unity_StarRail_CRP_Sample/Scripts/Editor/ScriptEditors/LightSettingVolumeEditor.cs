using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Unity_StarRail_CRP_Sample.Editor
{
    [CustomEditor(typeof(LightSettingVolume))]
    public class LightSettingVolumeEditor : VolumeComponentEditor
    {
        SerializedDataParameter mainLightMode;
        SerializedDataParameter shadowLightMode;
        
        SerializedDataParameter mainLightRotation;
        SerializedDataParameter mainLightColor;
        SerializedDataParameter shadowLightRotation;
        
        SerializedDataParameter mainLight;
        SerializedDataParameter shadowLight;
        SerializedDataParameter mainLightIntensity;
        
        SerializedDataParameter mainVirtualLight;
        SerializedDataParameter shadowVirtualLight;
        SerializedDataParameter overrideMainLightColor;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<LightSettingVolume>(serializedObject);
            
            mainLightMode = Unpack(o.Find(x => x.mainLightMode));
            shadowLightMode = Unpack(o.Find(x => x.shadowLightMode));
            
            mainLightRotation = Unpack(o.Find(x => x.mainLightRotation));
            mainLightColor = Unpack(o.Find(x => x.mainLightColor));
            shadowLightRotation = Unpack(o.Find(x => x.shadowLightRotation));
            
            mainLight = Unpack(o.Find(x => x.mainLight));
            shadowLight = Unpack(o.Find(x => x.shadowLight));
            mainLightIntensity = Unpack(o.Find(x => x.mainLightIntensity));
            
            mainVirtualLight = Unpack(o.Find(x => x.mainVirtualLight));
            shadowVirtualLight = Unpack(o.Find(x => x.shadowVirtualLight));
            overrideMainLightColor = Unpack(o.Find(x => x.overrideMainLightColor));
        }
        
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Main Light");
            
            PropertyField(mainLightMode);

            if (mainLightMode.value.enumValueFlag == (int)LightSettingMode.VirtualFixed)
            {
                PropertyField(mainLightRotation);
                PropertyField(mainLightColor);
            }
            if (mainLightMode.value.enumValueFlag == (int)LightSettingMode.FromVirtualObject)
            {
                PropertyField(mainVirtualLight);
                PropertyField(overrideMainLightColor);
            }
            else if (mainLightMode.value.enumValueFlag == (int)LightSettingMode.FromLightObject)
            {
                PropertyField(mainLight);
                PropertyField(mainLightIntensity);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Shadow Light");

            PropertyField(shadowLightMode);
            
            if (shadowLightMode.value.enumValueFlag == (int)LightSettingMode.VirtualFixed)
            {
                PropertyField(shadowLightRotation);
            }
            if (shadowLightMode.value.enumValueFlag == (int)LightSettingMode.FromVirtualObject)
            {
                PropertyField(shadowVirtualLight);
            }
            else if (shadowLightMode.value.enumValueFlag == (int)LightSettingMode.FromLightObject)
            {
                PropertyField(shadowLight);
            }
        }
    }
    
    [VolumeParameterDrawer(typeof(CustomColorParameter))]
    public class CustomColorParameterDrawer : VolumeParameterDrawer
    {
        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var value = parameter.value;

            if (value.propertyType != SerializedPropertyType.Color)
                return false;

            var o = parameter.GetObjectRef<CustomColorParameter>();

            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, title, value);
            value.colorValue = EditorGUI.ColorField(rect, title, value.colorValue, o.showEyeDropper, o.showAlpha, o.hdr);
            EditorGUI.EndProperty();
            return true;
        }
    }
}