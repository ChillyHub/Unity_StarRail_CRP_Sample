using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public class CharacterScreenShadowDrawCullChunk : CharacterDrawCallChunk
    {
        public Bounds bounds;
        
        public bool dirty = true;
    }
    
    public class CharacterCreateScreenShadowDrawCallSystem
    {
        private static class ScreenSpaceShadowConstant
        {
            public static readonly int IndexId = Shader.PropertyToID("_Index");
        }
        
        private CharacterEntityManager _entityManager;
        private ProfilingSampler _sampler;

        private Mesh _boxMesh;
        private Material _material;
        
        public CharacterCreateScreenShadowDrawCallSystem(CharacterEntityManager entityManager)
        {
            _entityManager = entityManager;
            _sampler = new ProfilingSampler($"{nameof(CharacterCreateScreenShadowDrawCallSystem)}.Execute");
        }
        
        public void Execute()
        {
            using (new ProfilingScope(null, _sampler))
            {
                int index = 0;
                for (int i = 0; i < _entityManager.chunkCount; i++)
                {
                    if (!_entityManager.IsValid(_entityManager.entityChunks[i].entity))
                    {
                        continue;
                    }
                    
                    Execute(_entityManager.entityChunks[i], _entityManager.screenShadowDrawCallChunks[i], index++);
                }
            }
        }
        
        private void Execute(CharacterEntityChunk entityChunk, CharacterScreenShadowDrawCullChunk drawCallChunk, int index)
        {
            if (drawCallChunk.dirty)
            {
                // Clear
                drawCallChunk.Dispose();

                GameObject character = entityChunk.character;
                CharacterLightInfo lightInfo = entityChunk.lightInfo;
                drawCallChunk.bounds = new Bounds();

                SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (i == 0)
                    {
                        drawCallChunk.bounds = renderers[i].sharedMesh.bounds;
                    }
                    else
                    {
                        drawCallChunk.bounds.Encapsulate(renderers[i].sharedMesh.bounds);
                    }
                }

                Vector3 center = character.transform.position + drawCallChunk.bounds.center;
                float radius = drawCallChunk.bounds.extents.magnitude;

                InitMaterial();
                InitMesh();

                int stencilPass = (int)ScreenSpaceShadowsPass.MaterialPass.CharacterStencilVolume;
                int shadowPass = (int)ScreenSpaceShadowsPass.MaterialPass.CharacterShadow;

                Quaternion rotate = Quaternion.LookRotation(lightInfo.shadowLightDirection);
                Vector3 scale = new Vector3(radius, radius, radius * 10.0f);
                Matrix4x4 objectToWorld = Matrix4x4.TRS(center, rotate, scale);

                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetFloat(ScreenSpaceShadowConstant.IndexId, index);

                // Alloc Data
                drawCallChunk.meshes = new Mesh[2];
                drawCallChunk.subMeshIndices = new int[2];
                drawCallChunk.materials = new Material[2];
                drawCallChunk.propertyBlocks = new MaterialPropertyBlock[2];
                drawCallChunk.passIndices = new int[2];
                drawCallChunk.objectToWorlds = new Matrix4x4[2];

                // Fill Data
                drawCallChunk.drawCallCount = 2;
                
                drawCallChunk.drawCallIndices.Add(0);
                drawCallChunk.meshes[0] = _boxMesh;
                drawCallChunk.subMeshIndices[0] = 0;
                drawCallChunk.materials[0] = _material;
                drawCallChunk.propertyBlocks[0] = propertyBlock;
                drawCallChunk.passIndices[0] = stencilPass;
                drawCallChunk.objectToWorlds[0] = objectToWorld;
                
                drawCallChunk.drawCallIndices.Add(1);
                drawCallChunk.meshes[1] = _boxMesh;
                drawCallChunk.subMeshIndices[1] = 0;
                drawCallChunk.materials[1] = _material;
                drawCallChunk.propertyBlocks[1] = propertyBlock;
                drawCallChunk.passIndices[1] = shadowPass;
                drawCallChunk.objectToWorlds[1] = objectToWorld;

                drawCallChunk.dirty = false;
            }
            else
            {
                GameObject character = entityChunk.character;
                CharacterLightInfo lightInfo = entityChunk.lightInfo;
                
                Vector3 center = character.transform.position + drawCallChunk.bounds.center;
                float radius = drawCallChunk.bounds.extents.magnitude;
                
                Quaternion rotate = Quaternion.LookRotation(lightInfo.shadowLightDirection);
                Vector3 scale = new Vector3(radius, radius, radius * 10.0f);
                Matrix4x4 objectToWorld = Matrix4x4.TRS(center, rotate, scale);
                
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetFloat(ScreenSpaceShadowConstant.IndexId, index);
                
                drawCallChunk.propertyBlocks[0] = propertyBlock;
                drawCallChunk.propertyBlocks[1] = propertyBlock;
                
                drawCallChunk.objectToWorlds[0] = objectToWorld;
                drawCallChunk.objectToWorlds[1] = objectToWorld;
            }
        }
        
        private void InitMaterial()
        {
            if (_material == null)
            {
                const string shaderName = "Hidden/StarRail_CRP/Shadow/ScreenSpaceShadows";
                Shader shader = Shader.Find(shaderName);

                if (shader == null)
                {
                    Debug.LogWarning($"Can't find shader: {shaderName}");
                }
                else
                {
                    _material = CoreUtils.CreateEngineMaterial(shader);
                }
            }
        }

        private void InitMesh()
        {
            if (_boxMesh == null)
            {
                _boxMesh = CreateBoxMesh();
            }
        }
        
        private static Mesh CreateBoxMesh()
        {
            Vector3[] positions =
            {
                new Vector3(-1.0f, -1.0f, -1.0f),
                new Vector3(-1.0f, -1.0f, +1.0f),
                new Vector3(-1.0f, +1.0f, -1.0f),
                new Vector3(-1.0f, +1.0f, +1.0f),
                new Vector3(+1.0f, -1.0f, -1.0f),
                new Vector3(+1.0f, -1.0f, +1.0f),
                new Vector3(+1.0f, +1.0f, -1.0f),
                new Vector3(+1.0f, +1.0f, +1.0f)
            };

            int[] indices =
            {
                0, 1, 2, 2, 1, 3,
                4, 6, 5, 5, 6, 7,
                0, 4, 1, 1, 4, 5,
                2, 3, 6, 6, 3, 7,
                0, 2, 4, 4, 2, 6,
                1, 5, 3, 3, 5, 7
            };

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;

            return mesh;
        }
    }
}