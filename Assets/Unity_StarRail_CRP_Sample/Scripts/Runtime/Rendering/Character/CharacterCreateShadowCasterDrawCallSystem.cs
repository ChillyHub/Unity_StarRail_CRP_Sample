using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public class CharacterShadowCasterDrawCullChunk : CharacterDrawCallChunk
    {
        public Bounds bounds;
        public JobHandle currentJobHandle;
        
        public bool dirty = true;

        public static readonly int MaxCharacterCounts = 16;
        public static readonly int CharacterShadowSize = 4096;
        public static readonly int CharacterShadowTileSize = 1024;
    }
    
    public class CharacterCreateShadowCasterDrawCallSystem
    {
        private static class MaterialConstants
        {
            public static readonly int CharMainLightDirectionId = Shader.PropertyToID("_CharMainLightDirection");
            public static readonly int CharMainLightColorId = Shader.PropertyToID("_CharMainLightColor");
            public static readonly int HeadCenterId = Shader.PropertyToID("_HeadCenter");
            public static readonly int HeadForwardId = Shader.PropertyToID("_HeadForward");
            public static readonly int HeadRightId = Shader.PropertyToID("_HeadRight");
            public static readonly int HeadUpId = Shader.PropertyToID("_HeadUp");
            public static readonly int DayTimeId = Shader.PropertyToID("_DayTime");
        }
        
        private CharacterEntityManager _entityManager;
        private ProfilingSampler _sampler;
        
        public CharacterCreateShadowCasterDrawCallSystem(CharacterEntityManager entityManager)
        {
            _entityManager = entityManager;
            _sampler = new ProfilingSampler($"{nameof(CharacterCreateShadowCasterDrawCallSystem)}.Execute");
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
                    
                    Execute(_entityManager.entityChunks[i], _entityManager.shadowCasterDrawCallChunks[i], index++);
                }
            }
        }
        
        private void Execute(CharacterEntityChunk entityChunk, CharacterShadowCasterDrawCullChunk drawCallChunk, int validIndex)
        {
            if (drawCallChunk.dirty)
            {
                // Clear
                drawCallChunk.Dispose();

                GameObject character = entityChunk.character;
                drawCallChunk.bounds = new Bounds();

                // Alloc Data
                SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material[] materials = renderers[i].sharedMaterials;
                    for (int j = 0; j < materials.Length; j++)
                    {
                        int passIndex = materials[j].FindPass("Character Shadow Caster");

                        if (passIndex < 0)
                        {
                            continue;
                        }

                        drawCallChunk.drawCallCount++;
                    }

                    if (i == 0)
                    {
                        drawCallChunk.bounds = renderers[i].sharedMesh.bounds;
                    }
                    else
                    {
                        drawCallChunk.bounds.Encapsulate(renderers[i].sharedMesh.bounds);
                    }
                }
                
                drawCallChunk.materials = new Material[drawCallChunk.drawCallCount];
                drawCallChunk.subMeshIndices = new int[drawCallChunk.drawCallCount];
                drawCallChunk.passIndices = new int[drawCallChunk.drawCallCount];
                drawCallChunk.renderers = new Renderer[drawCallChunk.drawCallCount];

                // Fill Data
                int index = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material[] materials = renderers[i].sharedMaterials;
                    for (int j = 0; j < materials.Length; j++)
                    {
                        int passIndex = materials[j].FindPass("Character Shadow Caster");

                        if (passIndex < 0)
                        {
                            continue;
                        }

                        drawCallChunk.drawCallIndices.Add(index);
                        drawCallChunk.materials[index] = materials[j];
                        drawCallChunk.subMeshIndices[index] = j;
                        drawCallChunk.passIndices[index] = passIndex;
                        drawCallChunk.renderers[index] = renderers[i];

                        index++;
                    }
                }

                // Sort Data
                drawCallChunk.drawCallIndices.Sort((a, b) =>
                {
                    Material ma = drawCallChunk.materials[a];
                    Material mb = drawCallChunk.materials[b];
                    
                    if (ma != null && mb == null)
                    {
                        return -1;
                    }

                    if (ma == null && mb != null)
                    {
                        return 1;
                    }

                    if (ma.renderQueue < mb.renderQueue)
                    {
                        return -1;
                    }

                    if (ma.renderQueue > mb.renderQueue)
                    {
                        return 1;
                    }

                    return ma.GetHashCode().CompareTo(mb.GetHashCode());
                });

                drawCallChunk.dirty = false;
            }

            foreach (var renderer in drawCallChunk.renderers)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetVector(MaterialConstants.CharMainLightDirectionId, 
                    entityChunk.lightInfo.mainLightDirection);
                propertyBlock.SetVector(MaterialConstants.CharMainLightColorId, 
                    entityChunk.lightInfo.mainLightColor);
                propertyBlock.SetVector(MaterialConstants.HeadCenterId, entityChunk.lightInfo.headCenter);
                propertyBlock.SetVector(MaterialConstants.HeadForwardId, entityChunk.lightInfo.headForward);
                propertyBlock.SetVector(MaterialConstants.HeadRightId, entityChunk.lightInfo.headRight);
                propertyBlock.SetVector(MaterialConstants.HeadUpId, entityChunk.lightInfo.headUp);
                propertyBlock.SetFloat(MaterialConstants.DayTimeId, entityChunk.lightInfo.dayTime);

                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}