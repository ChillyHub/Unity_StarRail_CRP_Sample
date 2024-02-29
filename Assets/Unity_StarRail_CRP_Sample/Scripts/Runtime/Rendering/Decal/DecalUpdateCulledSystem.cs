using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public class DecalUpdateCulledSystem
    {
        private DecalEntityManager _entityManager;
        private ProfilingSampler _sampler;

        public DecalUpdateCulledSystem(DecalEntityManager entityManager)
        {
            _entityManager = entityManager;
            _sampler = new ProfilingSampler("DecalUpdateCulledSystem.Execute");
        }

        public void Execute()
        {
            using (new ProfilingScope(null, _sampler))
            {
                for (int i = 0; i < _entityManager.chunkCount; ++i)
                    Execute(_entityManager.culledChunks[i], _entityManager.culledChunks[i].count);
            }
        }

        private void Execute(DecalCulledChunk culledChunk, int count)
        {
            if (count == 0)
                return;

            culledChunk.currentJobHandle.Complete();

            CullingGroup cullingGroup = culledChunk.cullingGroups;
            culledChunk.visibleDecalCount = cullingGroup.QueryIndices(true, culledChunk.visibleDecalIndexArray, 0);
            culledChunk.visibleDecalIndices.CopyFrom(culledChunk.visibleDecalIndexArray);
        }
    }
}