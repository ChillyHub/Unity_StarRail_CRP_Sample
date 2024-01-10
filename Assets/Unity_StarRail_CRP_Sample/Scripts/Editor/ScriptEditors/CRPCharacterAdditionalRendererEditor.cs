using UnityEngine;
using UnityEditor;

namespace Unity_StarRail_CRP_Sample.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CRPCharacterAdditionalRenderer))]
    public class CRPCharacterAdditionalRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty _characterTypeProp;
        private SerializedProperty _mainLightDirectionModeProp;
        private SerializedProperty _shadowLightDirectionModeProp;
        private SerializedProperty _mainDirectionLightProp;
        private SerializedProperty _shadowDirectionLightProp;
        private SerializedProperty _mainPointLightProp;
        private SerializedProperty _shadowPointLightProp;
        private SerializedProperty _mainLightRotationProp;
        private SerializedProperty _shadowLightDirectionProp;
        private SerializedProperty _mainLightColorProp;
        private SerializedProperty _headBindingProp;
        private SerializedProperty _overrideKeywordsProp;
        private SerializedProperty _keywordsValuesProp;
        private SerializedProperty _enableGI ;
        private SerializedProperty _enableAdditionalLight;
        private SerializedProperty _enableDiffuse;
        private SerializedProperty _enableSpecular;
        private SerializedProperty _enableEmission;
        private SerializedProperty _enableRim;
        private SerializedProperty _enableOutline;
        private SerializedProperty _enableStocking;

        private void Init()
        {
            _characterTypeProp = serializedObject.FindProperty("characterType");
            _mainLightDirectionModeProp = serializedObject.FindProperty("mainLightDirectionMode");
            _shadowLightDirectionModeProp = serializedObject.FindProperty("shadowLightDirectionMode");
            _mainDirectionLightProp = serializedObject.FindProperty("mainDirectionLight");
            _shadowDirectionLightProp = serializedObject.FindProperty("shadowDirectionLight");
            _mainPointLightProp = serializedObject.FindProperty("mainPointLight");
            _shadowPointLightProp = serializedObject.FindProperty("shadowPointLight");
            _mainLightRotationProp = serializedObject.FindProperty("mainLightRotation");
            _shadowLightDirectionProp = serializedObject.FindProperty("shadowLightDirection");
            _mainLightColorProp = serializedObject.FindProperty("mainLightColor");
            _headBindingProp = serializedObject.FindProperty("headBinding");
            _overrideKeywordsProp = serializedObject.FindProperty("overrideKeywords");
            _keywordsValuesProp = serializedObject.FindProperty("keywordsValues");
            
            _enableGI = _keywordsValuesProp.FindPropertyRelative("enableGI");
            _enableAdditionalLight = _keywordsValuesProp.FindPropertyRelative("enableAdditionalLight");
            _enableDiffuse = _keywordsValuesProp.FindPropertyRelative("enableDiffuse");
            _enableSpecular = _keywordsValuesProp.FindPropertyRelative("enableSpecular");
            _enableEmission = _keywordsValuesProp.FindPropertyRelative("enableEmission");
            _enableRim = _keywordsValuesProp.FindPropertyRelative("enableRim");
            _enableOutline = _keywordsValuesProp.FindPropertyRelative("enableOutline");
            _enableStocking = _keywordsValuesProp.FindPropertyRelative("enableStocking");
        }
        
        public override void OnInspectorGUI()
        {
            if (_characterTypeProp == null)
                Init();

            EditorGUILayout.PropertyField(_characterTypeProp);
            
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_mainLightDirectionModeProp);
            if (_mainLightDirectionModeProp.enumValueFlag == (int)DirectionMode.Fixed)
            {
                EditorGUILayout.PropertyField(_mainLightRotationProp);
                EditorGUILayout.PropertyField(_mainLightColorProp);
            }
            else if (_mainLightDirectionModeProp.enumValueFlag == (int)DirectionMode.FromDirectionLight)
            {
                EditorGUILayout.PropertyField(_mainDirectionLightProp);
            }
            else if (_mainLightDirectionModeProp.enumValueFlag == (int)DirectionMode.FromPointLight)
            {
                EditorGUILayout.PropertyField(_mainPointLightProp);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.PropertyField(_shadowLightDirectionModeProp);
            if (_shadowLightDirectionModeProp.enumValueFlag == (int)DirectionMode.Fixed)
            {
                EditorGUILayout.PropertyField(_shadowLightDirectionProp);
            }
            else if (_shadowLightDirectionModeProp.enumValueFlag == (int)DirectionMode.FromDirectionLight)
            {
                EditorGUILayout.PropertyField(_shadowDirectionLightProp);
            }
            else if (_shadowLightDirectionModeProp.enumValueFlag == (int)DirectionMode.FromPointLight)
            {
                EditorGUILayout.PropertyField(_shadowPointLightProp);
            }
            
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_headBindingProp);

            EditorGUILayout.PropertyField(_overrideKeywordsProp);
            if (_overrideKeywordsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(_enableGI);
                EditorGUILayout.PropertyField(_enableAdditionalLight);
                EditorGUILayout.PropertyField(_enableDiffuse);
                EditorGUILayout.PropertyField(_enableSpecular);
                EditorGUILayout.PropertyField(_enableEmission);
                EditorGUILayout.PropertyField(_enableRim);
                EditorGUILayout.PropertyField(_enableOutline);
                EditorGUILayout.PropertyField(_enableStocking);

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}