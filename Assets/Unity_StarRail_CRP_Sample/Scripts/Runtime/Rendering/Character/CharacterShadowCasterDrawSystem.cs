using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class CharacterShadowCasterDrawSystem : CharacterDrawSystem
    {
        private CharacterEntityManager _entityManager;
        
        public CharacterShadowCasterDrawSystem(CharacterEntityManager entityManager)
        {
            _entityManager = entityManager;
        }   
        
        public override void Execute(CommandBuffer cmd, int chunkIndex)
        {
            CharacterShadowCasterDrawCullChunk drawCallChunk = _entityManager.shadowCasterDrawCallChunks[chunkIndex];
            
            drawCallChunk.currentJobHandle.Complete();
            
            DrawRenderers(cmd, drawCallChunk);
        }

        public ShadowSliceData GetShadowSliceData(CharacterEntityChunk entityChunk, 
            CharacterShadowCasterDrawCullChunk drawCullChunk, int validIndex)
        {
            GameObject character = entityChunk.character;
            CharacterLightInfo lightInfo = entityChunk.lightInfo;
            
            Vector3 center = character.transform.position + drawCullChunk.bounds.center;
            float radius = drawCullChunk.bounds.extents.magnitude;

            Vector3 up = Vector3.up;

            if (lightInfo.shadowLightDirection == new Vector3(0.0f, 1.0f, 0.0f) ||
                lightInfo.shadowLightDirection == new Vector3(0.0f, -1.0f, 0.0f))
            {
                up = Vector3.forward;
            }

            // Create view projection matrix
            Matrix4x4 viewMatrix = Matrix4x4.LookAt(Vector3.zero, -lightInfo.shadowLightDirection, up);
            viewMatrix.m03 = center.x;
            viewMatrix.m13 = center.y;
            viewMatrix.m23 = center.z;

            viewMatrix = viewMatrix.inverse;
            viewMatrix.m20 = -viewMatrix.m20;
            viewMatrix.m21 = -viewMatrix.m21;
            viewMatrix.m22 = -viewMatrix.m22;
            viewMatrix.m23 = -viewMatrix.m23;

            Matrix4x4 projMatrix = Matrix4x4.Ortho(-radius, radius, -radius, radius, -radius, radius);

            ShadowSplitData splitData = new ShadowSplitData();
            splitData.cullingSphere = new Vector4(center.x, center.y, center.z, radius);
            splitData.cullingPlaneCount = 1;
            splitData.shadowCascadeBlendCullingFactor = 1.0f;

            ShadowSliceData shadowSliceData = new ShadowSliceData()
            {
                splitData = splitData,
                offsetX = (validIndex % 4) * CharacterShadowCasterDrawCullChunk.CharacterShadowTileSize,
                offsetY = (validIndex / 4) * CharacterShadowCasterDrawCullChunk.CharacterShadowTileSize,
                resolution = CharacterShadowCasterDrawCullChunk.CharacterShadowTileSize,
                projectionMatrix = projMatrix,
                viewMatrix = viewMatrix,
                shadowTransform = GetShadowTransform(projMatrix, viewMatrix)
            };

            ApplySliceTransform(ref shadowSliceData, 
                CharacterShadowCasterDrawCullChunk.CharacterShadowSize, 
                CharacterShadowCasterDrawCullChunk.CharacterShadowSize);

            return shadowSliceData;
        }

        private Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
        {
            Matrix4x4 worldToShadow = GL.GetGPUProjectionMatrix(proj, false) * view;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            // textureScaleAndBias maps texture space coordinates from [-1,1] to [0,1]

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }
        
        private void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }
    }
}