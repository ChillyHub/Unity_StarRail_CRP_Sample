using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Rendering;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample.Editor
{
    public static class CreateDecalRenderer
    {
        [MenuItem("GameObject/Rendering/CRP Decal Renderer", priority = CoreUtils.Priorities.gameObjectMenuPriority)]
        public static void CreateDecal(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("Decal Object", menuCommand.context);
            go.AddComponent<DecalRenderer>();
            go.transform.RotateAround(go.transform.position, go.transform.right, 90);
        }
    }
    
    [CustomEditor(typeof(DecalRenderer))]
    [CanEditMultipleObjects]
    public class DecalRendererEditor : UnityEditor.Editor
    {
        const string k_EditShapePreservingUVTooltip = "Modifies the projector boundaries and crops/tiles the decal to fill them.";
        const string k_EditShapeWithoutPreservingUVTooltip = "Modifies the projector boundaries and stretches the decal to fill them.";
        const string k_EditUVTooltip = "Modify the UV and the pivot position without moving the projection box. It can alter Transform.";

        static readonly GUIContent k_ScaleMode = EditorGUIUtility.TrTextContent("Scale Mode", "Specifies the scaling mode to apply to decals that use this Decal Projector.");
        static readonly GUIContent k_WidthContent = EditorGUIUtility.TrTextContent("Width", "Sets the width of the projection plan.");
        static readonly GUIContent k_HeightContent = EditorGUIUtility.TrTextContent("Height", "Sets the height of the projection plan.");
        static readonly GUIContent k_ProjectionDepthContent = EditorGUIUtility.TrTextContent("Projection Depth", "Sets the projection depth of the projector.");
        static readonly GUIContent k_MaterialContent = EditorGUIUtility.TrTextContent("Material", "Specifies the Material this component projects as a decal.");
        static readonly GUIContent k_RenderingLayerMaskContent = EditorGUIUtility.TrTextContent("Rendering Layers", "Specify the rendering layer mask for this projector. Unity renders decals on all meshes where at least one Rendering Layer value matches.");
        static readonly GUIContent k_DistanceContent = EditorGUIUtility.TrTextContent("Draw Distance", "Sets the distance from the Camera at which URP stop rendering the decal.");
        static readonly GUIContent k_FadeScaleContent = EditorGUIUtility.TrTextContent("Start Fade", "Controls the distance from the Camera at which this component begins to fade the decal out.");
        static readonly GUIContent k_AngleFadeContent = EditorGUIUtility.TrTextContent("Angle Fade", "Controls the fade out range of the decal based on the angle between the Decal backward direction and the vertex normal of the receiving surface. Requires 'Decal Layers' to be enabled in the URP Asset and Frame Settings.");
        static readonly GUIContent k_UVScaleContent = EditorGUIUtility.TrTextContent("Tilling", "Sets the scale for the decal Material. Scales the decal along its UV axes.");
        static readonly GUIContent k_UVBiasContent = EditorGUIUtility.TrTextContent("Offset", "Sets the offset for the decal Material. Moves the decal along its UV axes.");
        static readonly GUIContent k_OpacityContent = EditorGUIUtility.TrTextContent("Opacity", "Controls the transparency of the decal.");
        static readonly GUIContent k_Offset = EditorGUIUtility.TrTextContent("Pivot", "Controls the position of the pivot point of the decal.");

        static readonly string k_BaseSceneEditingToolText = "<color=grey>Decal Scene Editing Mode:</color> ";
        static readonly string k_EditShapeWithoutPreservingUVName = k_BaseSceneEditingToolText + "Scale";
        static readonly string k_EditShapePreservingUVName = k_BaseSceneEditingToolText + "Crop";
        static readonly string k_EditUVAndPivotName = k_BaseSceneEditingToolText + "Pivot / UV";
        
        const float k_Limit = 100000f;
        const float k_LimitInv = 1f / k_Limit;

        static Color fullColor
        {
            get
            {
                Color c = s_LastColor;
                c.a = 1f;
                return c;
            }
        }
        static Color s_LastColor;
        static void UpdateColorsInHandlesIfRequired()
        {
            Color c = new Color(1f, 1f, 1f, 0.2f);
            if (c != s_LastColor)
            {
                if (s_BoxHandle != null && !s_BoxHandle.Equals(null))
                    s_BoxHandle = null;

                if (s_uvHandles != null && !s_uvHandles.Equals(null))
                    s_uvHandles.baseColor = c;

                s_LastColor = c;
            }
        }

        MaterialEditor m_MaterialEditor = null;
        SerializedProperty m_MaterialProperty;
        SerializedProperty m_DrawDistanceProperty;
        SerializedProperty m_FadeScaleProperty;
        SerializedProperty m_StartAngleFadeProperty;
        SerializedProperty m_EndAngleFadeProperty;
        SerializedProperty m_UVScaleProperty;
        SerializedProperty m_UVBiasProperty;
        SerializedProperty m_ScaleMode;
        SerializedProperty m_Size;
        SerializedProperty[] m_SizeValues;
        SerializedProperty m_Offset;
        SerializedProperty[] m_OffsetValues;
        SerializedProperty m_FadeFactor;
        SerializedProperty m_RenderingLayerMask;

        int layerMask => (target as Component).gameObject.layer;
        bool layerMaskHasMultipleValues
        {
            get
            {
                if (targets.Length < 2)
                    return false;
                int layerMask = (targets[0] as Component).gameObject.layer;
                for (int index = 1; index < targets.Length; ++index)
                {
                    if ((targets[index] as Component).gameObject.layer != layerMask)
                        return true;
                }
                return false;
            }
        }

        static HierarchicalBox s_BoxHandle;
        static HierarchicalBox boxHandle
        {
            get
            {
                if (s_BoxHandle == null || s_BoxHandle.Equals(null))
                {
                    Color c = fullColor;
                    s_BoxHandle = new HierarchicalBox(s_LastColor, new[] { c, c, c, c, c, c });
                    s_BoxHandle.SetBaseColor(s_LastColor);
                    s_BoxHandle.monoHandle = false;
                }
                return s_BoxHandle;
            }
        }

        static DisplacableRectHandles s_uvHandles;
        static DisplacableRectHandles uvHandles
        {
            get
            {
                if (s_uvHandles == null || s_uvHandles.Equals(null))
                    s_uvHandles = new DisplacableRectHandles(s_LastColor);
                return s_uvHandles;
            }
        }

        static readonly BoxBoundsHandle s_AreaLightHandle =
            new BoxBoundsHandle { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };

        const EditMode.SceneViewEditMode k_EditShapeWithoutPreservingUV = (EditMode.SceneViewEditMode)90;
        const EditMode.SceneViewEditMode k_EditShapePreservingUV = (EditMode.SceneViewEditMode)91;
        const EditMode.SceneViewEditMode k_EditUVAndPivot = (EditMode.SceneViewEditMode)92;
        static readonly EditMode.SceneViewEditMode[] k_EditVolumeModes = new EditMode.SceneViewEditMode[]
        {
            k_EditShapeWithoutPreservingUV,
            k_EditShapePreservingUV,
            k_EditUVAndPivot,
        };

        static Func<Vector3, Quaternion, Vector3> s_DrawPivotHandle;

        static GUIContent[] k_EditVolumeLabels = null;
        static GUIContent[] editVolumeLabels => k_EditVolumeLabels ?? (k_EditVolumeLabels = new GUIContent[]
        {
            EditorGUIUtility.TrIconContent("d_ScaleTool", k_EditShapeWithoutPreservingUVTooltip),
            EditorGUIUtility.TrIconContent("d_RectTool", k_EditShapePreservingUVTooltip),
            EditorGUIUtility.TrIconContent("d_MoveTool", k_EditUVTooltip),
        });

        static List<DecalRendererEditor> s_Instances = new List<DecalRendererEditor>();

        static DecalRendererEditor FindEditorFromSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            DecalRenderer[] selectionTargets = Selection.GetFiltered<DecalRenderer>(SelectionMode.Unfiltered);

            foreach (DecalRendererEditor editor in s_Instances)
            {
                if (selectionTargets.Length != editor.targets.Length)
                    continue;
                bool allOk = true;
                foreach (DecalRenderer selectionTarget in selectionTargets)
                {
                    if (!Array.Find(editor.targets, t => t == selectionTarget))
                    {
                        allOk = false;
                        break;
                    }
                }
                if (!allOk)
                    continue;
                return editor;
            }
            return null;
        }

        private void OnEnable()
        {
            s_Instances.Add(this);

            // Create an instance of the MaterialEditor
            UpdateMaterialEditor();

            // Fetch serialized properties
            m_MaterialProperty = serializedObject.FindProperty("m_Material");
            m_DrawDistanceProperty = serializedObject.FindProperty("m_DrawDistance");
            m_FadeScaleProperty = serializedObject.FindProperty("m_FadeScale");
            m_StartAngleFadeProperty = serializedObject.FindProperty("m_StartAngleFade");
            m_EndAngleFadeProperty = serializedObject.FindProperty("m_EndAngleFade");
            m_UVScaleProperty = serializedObject.FindProperty("m_UVScale");
            m_UVBiasProperty = serializedObject.FindProperty("m_UVBias");
            m_ScaleMode = serializedObject.FindProperty("m_ScaleMode");
            m_Size = serializedObject.FindProperty("m_Size");
            m_SizeValues = new[]
            {
                m_Size.FindPropertyRelative("x"),
                m_Size.FindPropertyRelative("y"),
                m_Size.FindPropertyRelative("z"),
            };
            m_Offset = serializedObject.FindProperty("m_Offset");
            m_OffsetValues = new[]
            {
                m_Offset.FindPropertyRelative("x"),
                m_Offset.FindPropertyRelative("y"),
                m_Offset.FindPropertyRelative("z"),
            };
            m_FadeFactor = serializedObject.FindProperty("m_FadeFactor");
            m_RenderingLayerMask = serializedObject.FindProperty("m_DecalLayerMask");

            ReinitSavedRatioSizePivotPosition();
        }

        private void OnDisable()
        {
            s_Instances.Remove(this);
        }

        private void OnDestroy() =>
            DestroyImmediate(m_MaterialEditor);

        public bool HasFrameBounds()
        {
            return true;
        }

        public Bounds OnGetFrameBounds()
        {
            DecalRenderer decalProjector = target as DecalRenderer;

            return new Bounds(decalProjector.transform.position, boxHandle.size);
        }

        public void UpdateMaterialEditor()
        {
            int validMaterialsCount = 0;
            for (int index = 0; index < targets.Length; ++index)
            {
                DecalRenderer decalProjector = (targets[index] as DecalRenderer);
                if ((decalProjector != null) && (decalProjector.material != null))
                    validMaterialsCount++;
            }
            // Update material editor with the new material
            UnityEngine.Object[] materials = new UnityEngine.Object[validMaterialsCount];
            validMaterialsCount = 0;
            for (int index = 0; index < targets.Length; ++index)
            {
                DecalRenderer decalProjector = (targets[index] as DecalRenderer);

                if ((decalProjector != null) && (decalProjector.material != null))
                    materials[validMaterialsCount++] = (targets[index] as DecalRenderer).material;
            }
            m_MaterialEditor = (MaterialEditor)CreateEditor(materials);
        }

        void OnSceneGUI()
        {
            //called on each targets
            DrawHandles();
        }

        void DrawBoxTransformationHandles(DecalRenderer decalRenderer)
        {
            Vector3 scale = decalRenderer.effectiveScale;
            using (new Handles.DrawingScope(fullColor, Matrix4x4.TRS(decalRenderer.transform.position, decalRenderer.transform.rotation, scale)))
            {
                Vector3 centerStart = decalRenderer.pivot;
                boxHandle.center = centerStart;
                boxHandle.size = decalRenderer.size;

                Vector3 boundsSizePreviousOS = boxHandle.size;
                Vector3 boundsMinPreviousOS = boxHandle.size * -0.5f + boxHandle.center;

                EditorGUI.BeginChangeCheck();
                boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    // Adjust decal transform if handle changed.
                    Undo.RecordObject(decalRenderer, "Decal Projector Change");

                    bool xChangeIsValid = scale.x != 0f;
                    bool yChangeIsValid = scale.y != 0f;
                    bool zChangeIsValid = scale.z != 0f;

                    // Preserve serialized state for axes with scale 0.
                    decalRenderer.size = new Vector3(
                        xChangeIsValid ? boxHandle.size.x : decalRenderer.size.x,
                        yChangeIsValid ? boxHandle.size.y : decalRenderer.size.y,
                        zChangeIsValid ? boxHandle.size.z : decalRenderer.size.z);
                    decalRenderer.pivot = new Vector3(
                        xChangeIsValid ? boxHandle.center.x : decalRenderer.pivot.x,
                        yChangeIsValid ? boxHandle.center.y : decalRenderer.pivot.y,
                        zChangeIsValid ? boxHandle.center.z : decalRenderer.pivot.z);

                    Vector3 boundsSizeCurrentOS = boxHandle.size;
                    Vector3 boundsMinCurrentOS = boxHandle.size * -0.5f + boxHandle.center;

                    if (EditMode.editMode == k_EditShapePreservingUV)
                    {
                        // Treat decal projector bounds as a crop tool, rather than a scale tool.
                        // Compute a new uv scale and bias terms to pin decal projection pixels in world space, irrespective of projector bounds.
                        // Preserve serialized state for axes with scale 0.
                        Vector2 uvScale = decalRenderer.uvScale;
                        Vector2 uvBias = decalRenderer.uvBias;
                        if (xChangeIsValid)
                        {
                            uvScale.x *= Mathf.Max(k_LimitInv, boundsSizeCurrentOS.x) / Mathf.Max(k_LimitInv, boundsSizePreviousOS.x);
                            uvBias.x += (boundsMinCurrentOS.x - boundsMinPreviousOS.x) / Mathf.Max(k_LimitInv, boundsSizeCurrentOS.x) * uvScale.x;
                        }
                        if (yChangeIsValid)
                        {
                            uvScale.y *= Mathf.Max(k_LimitInv, boundsSizeCurrentOS.y) / Mathf.Max(k_LimitInv, boundsSizePreviousOS.y);
                            uvBias.y += (boundsMinCurrentOS.y - boundsMinPreviousOS.y) / Mathf.Max(k_LimitInv, boundsSizeCurrentOS.y) * uvScale.y;
                        }
                        decalRenderer.uvScale = uvScale;
                        decalRenderer.uvBias = uvBias;
                    }

                    if (PrefabUtility.IsPartOfNonAssetPrefabInstance(decalRenderer))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(decalRenderer);
                    }
                }
            }
        }

        void DrawPivotHandles(DecalRenderer decalRenderer)
        {
            Vector3 scale = decalRenderer.effectiveScale;
            Vector3 scaledPivot = Vector3.Scale(decalRenderer.pivot, scale);
            Vector3 scaledSize = Vector3.Scale(decalRenderer.size, scale);

            using (new Handles.DrawingScope(fullColor, Matrix4x4.TRS(Vector3.zero, decalRenderer.transform.rotation, Vector3.one)))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = ProjectedTransform.DrawHandles(decalRenderer.transform.position, .5f * scaledSize.z - scaledPivot.z, decalRenderer.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { decalRenderer, decalRenderer.transform }, "Decal Projector Change");

                    scaledPivot += Quaternion.Inverse(decalRenderer.transform.rotation) * (decalRenderer.transform.position - newPosition);
                    decalRenderer.pivot = new Vector3(
                        scale.x != 0f ? scaledPivot.x / scale.x : decalRenderer.pivot.x,
                        scale.y != 0f ? scaledPivot.y / scale.y : decalRenderer.pivot.y,
                        scale.z != 0f ? scaledPivot.z / scale.z : decalRenderer.pivot.z);
                    decalRenderer.transform.position = newPosition;

                    ReinitSavedRatioSizePivotPosition();
                }
            }
        }

        void DrawUVHandles(DecalRenderer decalRenderer)
        {
            Vector3 scale = decalRenderer.effectiveScale;
            Vector3 scaledPivot = Vector3.Scale(decalRenderer.pivot, scale);
            Vector3 scaledSize = Vector3.Scale(decalRenderer.size, scale);

            using (new Handles.DrawingScope(Matrix4x4.TRS(decalRenderer.transform.position + decalRenderer.transform.rotation * (scaledPivot - .5f * scaledSize), decalRenderer.transform.rotation, scale)))
            {
                Vector2 uvScale = decalRenderer.uvScale;
                Vector2 uvBias = decalRenderer.uvBias;

                Vector2 uvSize = new Vector2(
                    (uvScale.x > k_Limit || uvScale.x < -k_Limit) ? 0f : decalRenderer.size.x / uvScale.x,
                    (uvScale.y > k_Limit || uvScale.y < -k_Limit) ? 0f : decalRenderer.size.y / uvScale.y
                );
                Vector2 uvCenter = uvSize * .5f - new Vector2(uvBias.x * uvSize.x, uvBias.y * uvSize.y);

                uvHandles.center = uvCenter;
                uvHandles.size = uvSize;

                EditorGUI.BeginChangeCheck();
                uvHandles.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(decalRenderer, "Decal Projector Change");

                    for (int channel = 0; channel < 2; channel++)
                    {
                        // Preserve serialized state for axes with the scaled size 0.
                        if (scaledSize[channel] != 0f)
                        {
                            float handleSize = uvHandles.size[channel];
                            float minusNewUVStart = .5f * handleSize - uvHandles.center[channel];
                            float decalSize = decalRenderer.size[channel];
                            float limit = k_LimitInv * decalSize;
                            if (handleSize > limit || handleSize < -limit)
                            {
                                uvScale[channel] = decalSize / handleSize;
                                uvBias[channel] = minusNewUVStart / handleSize;
                            }
                            else
                            {
                                // TODO: Decide if uvHandles.size should ever have negative value. It can't currently.
                                uvScale[channel] = k_Limit * Mathf.Sign(handleSize);
                                uvBias[channel] = k_Limit * minusNewUVStart / decalSize;
                            }
                        }
                    }

                    decalRenderer.uvScale = uvScale;
                    decalRenderer.uvBias = uvBias;
                }
            }
        }

        void DrawHandles()
        {
            DecalRenderer decalRenderer = target as DecalRenderer;

            if (EditMode.editMode == k_EditShapePreservingUV || EditMode.editMode == k_EditShapeWithoutPreservingUV)
                DrawBoxTransformationHandles(decalRenderer);
            else if (EditMode.editMode == k_EditUVAndPivot)
            {
                DrawPivotHandles(decalRenderer);
                DrawUVHandles(decalRenderer);
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawGizmosSelected(DecalRenderer decalRenderer, GizmoType gizmoType)
        {
            UpdateColorsInHandlesIfRequired();

            const float k_DotLength = 5f;

            // Draw them with scale applied to size and pivot instead of the matrix to keep the proportions of the arrow and lines.
            using (new Handles.DrawingScope(fullColor, Matrix4x4.TRS(decalRenderer.transform.position, decalRenderer.transform.rotation, Vector3.one)))
            {
                Vector3 scale = decalRenderer.effectiveScale;
                Vector3 scaledPivot = Vector3.Scale(decalRenderer.pivot, scale);
                Vector3 scaledSize = Vector3.Scale(decalRenderer.size, scale);

                boxHandle.center = scaledPivot;
                boxHandle.size = scaledSize;
                bool isVolumeEditMode = EditMode.editMode == k_EditShapePreservingUV || EditMode.editMode == k_EditShapeWithoutPreservingUV;
                bool isPivotEditMode = EditMode.editMode == k_EditUVAndPivot;
                boxHandle.DrawHull(isVolumeEditMode);

                Vector3 pivot = Vector3.zero;
                Vector3 projectedPivot = new Vector3(0, 0, scaledPivot.z - .5f * scaledSize.z);

                if (isPivotEditMode)
                {
                    Handles.DrawDottedLines(new[] { projectedPivot, pivot }, k_DotLength);
                }
                else
                {
                    float arrowSize = scaledSize.z * 0.25f;
                    Handles.ArrowHandleCap(0, projectedPivot, Quaternion.identity, arrowSize, EventType.Repaint);
                }

                //draw UV and bolder edges
                using (new Handles.DrawingScope(Matrix4x4.TRS(decalRenderer.transform.position + decalRenderer.transform.rotation * new Vector3(scaledPivot.x, scaledPivot.y, scaledPivot.z - .5f * scaledSize.z), decalRenderer.transform.rotation, Vector3.one)))
                {
                    Vector2 UVSize = new Vector2(
                        (decalRenderer.uvScale.x > k_Limit || decalRenderer.uvScale.x < -k_Limit) ? 0f : scaledSize.x / decalRenderer.uvScale.x,
                        (decalRenderer.uvScale.y > k_Limit || decalRenderer.uvScale.y < -k_Limit) ? 0f : scaledSize.y / decalRenderer.uvScale.y
                    );
                    Vector2 UVCenter = UVSize * .5f - new Vector2(decalRenderer.uvBias.x * UVSize.x, decalRenderer.uvBias.y * UVSize.y) - (Vector2)scaledSize * .5f;

                    uvHandles.center = UVCenter;
                    uvHandles.size = UVSize;
                    uvHandles.DrawRect(dottedLine: true, screenSpaceSize: k_DotLength);

                    uvHandles.center = default;
                    uvHandles.size = scaledSize;
                    uvHandles.DrawRect(dottedLine: false, thickness: 3f);
                }
            }
        }

        static Func<Bounds> GetBoundsGetter(DecalProjector decalProjector)
        {
            return () =>
            {
                var bounds = new Bounds();
                var decalTransform = decalProjector.transform;
                bounds.Encapsulate(decalTransform.position);
                return bounds;
            };
        }

        // Temporarily save ratio between  size and pivot position while editing in inspector.
        // null or NaN is used to say that there is no saved ratio.
        // Aim is to keep proportion while sliding the value to 0 in Inspector and then go back to something else.
        // Current solution only works for the life of this editor, but is enough in most cases.
        // Which means if you go to there, selection something else and go back on it, pivot position is thus null.
        Dictionary<DecalRenderer, Vector3> ratioSizePivotPositionSaved = null;

        void ReinitSavedRatioSizePivotPosition()
        {
            ratioSizePivotPositionSaved = null;
        }

        void UpdateSize(int axe, float newSize)
        {
            void UpdateSizeOfOneTarget(DecalRenderer currentTarget)
            {
                //lazy init on demand as targets array cannot be accessed from OnSceneGUI so in edit mode.
                if (ratioSizePivotPositionSaved == null)
                {
                    ratioSizePivotPositionSaved = new Dictionary<DecalRenderer, Vector3>();
                    foreach (DecalRenderer projector in targets)
                        ratioSizePivotPositionSaved[projector] = new Vector3(float.NaN, float.NaN, float.NaN);
                }

                // Save old ratio if not registered
                // Either or are NaN or no one, check only first
                Vector3 saved = ratioSizePivotPositionSaved[currentTarget];
                if (float.IsNaN(saved[axe]))
                {
                    float oldSize = currentTarget.size[axe];
                    saved[axe] = Mathf.Abs(oldSize) <= Mathf.Epsilon ? 0f : currentTarget.pivot[axe] / oldSize;
                    ratioSizePivotPositionSaved[currentTarget] = saved;
                }

                currentTarget.refSize[axe] = newSize;
                currentTarget.refOffset[axe] = saved[axe] * newSize;

                // refresh DecalProjector to update projection
                currentTarget.OnValidate();
            }

            // Manually register Undo as we work directly on the target
            Undo.RecordObjects(targets, "Change DecalProjector Size or Depth");

            // Apply any change on target first
            serializedObject.ApplyModifiedProperties();

            // update each target
            foreach (DecalRenderer decalRenderer in targets)
            {
                UpdateSizeOfOneTarget(decalRenderer);

                // Fix for UUM-29105 (Changes made to Decal Project Prefab in the Inspector are not saved)
                // This editor doesn't use serializedObject to modify the target objects, explicitly mark the prefab
                // asset dirty to ensure the new data is saved.
                if (PrefabUtility.IsPartOfPrefabAsset(decalRenderer))
                    EditorUtility.SetDirty(decalRenderer);
            }

            // update again serialize object to register change in targets
            serializedObject.Update();

            // change was not tracked by SerializeReference so force repaint the scene views and game views
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            // strange: we need to force it throu serialization to update multiple differente value state (value are right but still detected as different)
            if (m_SizeValues[axe].hasMultipleDifferentValues)
                m_SizeValues[axe].floatValue = newSize;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool materialChanged = false;
            bool isDefaultMaterial = false;
            bool isValidDecalMaterial = true;

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditMode.DoInspectorToolbar(k_EditVolumeModes, editVolumeLabels, GetBoundsGetter(target as DecalProjector), this);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Info box for tools
                GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
                style.richText = true;
                GUILayout.BeginVertical(EditorStyles.helpBox);
                string helpText = k_BaseSceneEditingToolText;
                if (EditMode.editMode == k_EditShapeWithoutPreservingUV && EditMode.IsOwner(this))
                    helpText = k_EditShapeWithoutPreservingUVName;
                if (EditMode.editMode == k_EditShapePreservingUV && EditMode.IsOwner(this))
                    helpText = k_EditShapePreservingUVName;
                if (EditMode.editMode == k_EditUVAndPivot && EditMode.IsOwner(this))
                    helpText = k_EditUVAndPivotName;
                GUILayout.Label(helpText, style);
                GUILayout.EndVertical();
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(m_ScaleMode, k_ScaleMode);

                bool negativeScale = false;
                foreach (var target in targets)
                {
                    var decalRenderer = target as DecalRenderer;

                    float combinedScale = decalRenderer.transform.lossyScale.x * decalRenderer.transform.lossyScale.y * decalRenderer.transform.lossyScale.z;
                    negativeScale |= combinedScale < 0 && decalRenderer.scaleMode == DecalScaleMode.InheritFromHierarchy;
                }
                if (negativeScale)
                {
                    EditorGUILayout.HelpBox("Does not work with negative odd scaling (When there are odd number of scale components)", MessageType.Warning);
                }

                var widthRect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(widthRect, k_WidthContent, m_SizeValues[0]);
                EditorGUI.BeginChangeCheck();
                float newSizeX = EditorGUI.FloatField(widthRect, k_WidthContent, m_SizeValues[0].floatValue);
                if (EditorGUI.EndChangeCheck())
                    UpdateSize(0, Mathf.Max(0, newSizeX));
                EditorGUI.EndProperty();

                var heightRect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(heightRect, k_HeightContent, m_SizeValues[1]);
                EditorGUI.BeginChangeCheck();
                float newSizeY = EditorGUI.FloatField(heightRect, k_HeightContent, m_SizeValues[1].floatValue);
                if (EditorGUI.EndChangeCheck())
                    UpdateSize(1, Mathf.Max(0, newSizeY));
                EditorGUI.EndProperty();

                var projectionRect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(projectionRect, k_ProjectionDepthContent, m_SizeValues[2]);
                EditorGUI.BeginChangeCheck();
                float newSizeZ = EditorGUI.FloatField(projectionRect, k_ProjectionDepthContent, m_SizeValues[2].floatValue);
                if (EditorGUI.EndChangeCheck())
                    UpdateSize(2, Mathf.Max(0, newSizeZ));
                EditorGUI.EndProperty();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_Offset, k_Offset);
                if (EditorGUI.EndChangeCheck())
                    ReinitSavedRatioSizePivotPosition();

                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_MaterialProperty, k_MaterialContent);
                materialChanged = EditorGUI.EndChangeCheck();

                //DrawRenderingLayerMask(m_RenderingLayerMask, k_RenderingLayerMaskContent);

                foreach (var target in targets)
                {
                    var decalRenderer = target as DecalRenderer;
                    var mat = decalRenderer.material;

                    isDefaultMaterial |= decalRenderer.material == DecalRenderer.defaultMaterial;
                    isValidDecalMaterial &= decalRenderer.IsValid();
                }

                if (m_MaterialEditor && !isValidDecalMaterial)
                {
                    CoreEditorUtils.DrawFixMeBox("Decal only work with Decal Material. Use default material or create from decal shader graph sub target.", () =>
                    {
                        m_MaterialProperty.objectReferenceValue = DecalRenderer.defaultMaterial;
                        materialChanged = true;
                    });
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_UVScaleProperty, k_UVScaleContent);
                EditorGUILayout.PropertyField(m_UVBiasProperty, k_UVBiasContent);
                EditorGUILayout.PropertyField(m_FadeFactor, k_OpacityContent);
                EditorGUI.indentLevel--;

                bool angleFadeSupport = false;
                foreach (var decalRenderer in targets)
                {
                    var mat = (decalRenderer as DecalRenderer).material;
                    if (mat == null)
                        continue;
                    angleFadeSupport = mat.HasProperty("_DecalAngleFadeSupported");
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_DrawDistanceProperty, k_DistanceContent);
                if (EditorGUI.EndChangeCheck() && m_DrawDistanceProperty.floatValue < 0f)
                    m_DrawDistanceProperty.floatValue = 0f;

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_FadeScaleProperty, k_FadeScaleContent);
                EditorGUI.indentLevel--;

                using (new EditorGUI.DisabledScope(!angleFadeSupport))
                {
                    float angleFadeMinValue = m_StartAngleFadeProperty.floatValue;
                    float angleFadeMaxValue = m_EndAngleFadeProperty.floatValue;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(k_AngleFadeContent, ref angleFadeMinValue, ref angleFadeMaxValue, 0.0f, 180.0f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_StartAngleFadeProperty.floatValue = angleFadeMinValue;
                        m_EndAngleFadeProperty.floatValue = angleFadeMaxValue;
                    }
                }

                if (!angleFadeSupport && isValidDecalMaterial)
                {
                    EditorGUILayout.HelpBox($"Decal Angle Fade is not enabled in Shader. In ShaderGraph enable Angle Fade option.", MessageType.Info);
                }

                EditorGUILayout.Space();
            }
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            if (materialChanged)
                UpdateMaterialEditor();

            if (layerMaskHasMultipleValues || layerMask != (target as Component).gameObject.layer)
            {
                foreach (var decalRenderer in targets)
                {
                    (decalRenderer as DecalRenderer).OnValidate();
                }
            }

            if (m_MaterialEditor != null)
            {
                // We need to prevent the user to edit default decal materials
                if (isValidDecalMaterial)
                {
                    //using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
                    //{
                    //    // Draw the material's foldout and the material shader field
                    //    // Required to call m_MaterialEditor.OnInspectorGUI ();
                    //    m_MaterialEditor.DrawHeader();
//
                    //    // Draw the material properties
                    //    // Works only if the foldout of m_MaterialEditor.DrawHeader () is open
                    //    m_MaterialEditor.OnInspectorGUI();
                    //}
                    
                    // Draw the material's foldout and the material shader field
                    // Required to call m_MaterialEditor.OnInspectorGUI ();
                    m_MaterialEditor.DrawHeader();

                    // Draw the material properties
                    // Works only if the foldout of m_MaterialEditor.DrawHeader () is open
                    m_MaterialEditor.OnInspectorGUI();
                }
            }
        }

        [Shortcut("CRP/DecalRenderer: Handle changing size stretching UV", typeof(SceneView), KeyCode.Keypad1, ShortcutModifiers.Action)]
        static void EnterEditModeWithoutPreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalRenderer activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalRenderer>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            EditMode.ChangeEditMode(k_EditShapeWithoutPreservingUV, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("CRP/DecalRenderer: Handle changing size cropping UV", typeof(SceneView), KeyCode.Keypad2, ShortcutModifiers.Action)]
        static void EnterEditModePreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalRenderer activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalRenderer>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            EditMode.ChangeEditMode(k_EditShapePreservingUV, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("CRP/DecalRenderer: Handle changing pivot position and UVs", typeof(SceneView), KeyCode.Keypad3, ShortcutModifiers.Action)]
        static void EnterEditModePivotPreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalRenderer activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalRenderer>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            EditMode.ChangeEditMode(k_EditUVAndPivot, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("CRP/DecalRenderer: Handle swap between cropping and stretching UV", typeof(SceneView), KeyCode.Keypad4, ShortcutModifiers.Action)]
        static void SwappingEditUVMode(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalRenderer activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalRenderer>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            EditMode.SceneViewEditMode targetMode = EditMode.SceneViewEditMode.None;
            switch (EditMode.editMode)
            {
                case k_EditShapePreservingUV:
                case k_EditUVAndPivot:
                    targetMode = k_EditShapeWithoutPreservingUV;
                    break;
                case k_EditShapeWithoutPreservingUV:
                    targetMode = k_EditShapePreservingUV;
                    break;
            }
            if (targetMode != EditMode.SceneViewEditMode.None)
                EditMode.ChangeEditMode(targetMode, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("CRP/DecalRenderer: Stop Editing", typeof(SceneView), KeyCode.Keypad0, ShortcutModifiers.Action)]
        static void ExitEditMode(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalRenderer activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalRenderer>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            EditMode.QuitEditMode();
        }
        
        private static void DrawRenderingLayerMask(SerializedProperty property, GUIContent style)
        {
            Rect controlRect = EditorGUILayout.GetControlRect(true);
            int renderingLayer = property.intValue;

            string[] renderingLayerMaskNames = new string[1];// UniversalRenderPipelineGlobalSettings.instance.renderingLayerMaskNames;
            int maskCount = (int)Mathf.Log(renderingLayer, 2) + 1;
            if (renderingLayerMaskNames.Length < maskCount && maskCount <= 32)
            {
                var newRenderingLayerMaskNames = new string[maskCount];
                for (int i = 0; i < maskCount; ++i)
                {
                    newRenderingLayerMaskNames[i] = i < renderingLayerMaskNames.Length ? renderingLayerMaskNames[i] : $"Unused Layer {i}";
                }
                renderingLayerMaskNames = newRenderingLayerMaskNames;

                EditorGUILayout.HelpBox($"One or more of the Rendering Layers is not defined in the Universal Global Settings asset.", MessageType.Warning);
            }

            EditorGUI.BeginProperty(controlRect, style, property);

            EditorGUI.BeginChangeCheck();
            renderingLayer = EditorGUI.MaskField(controlRect, style, renderingLayer, renderingLayerMaskNames);

            if (EditorGUI.EndChangeCheck())
                property.uintValue = (uint)renderingLayer;

            EditorGUI.EndProperty();
        }
    }
}