using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public class CharacterScreenShadowDrawSystem : CharacterDrawSystem
    {
        private CharacterEntityManager _entityManager;
        
        public CharacterScreenShadowDrawSystem(CharacterEntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public override void Execute(CommandBuffer cmd, int chunkIndex)
        {
            CharacterScreenShadowDrawCullChunk drawCallChunk = _entityManager.screenShadowDrawCallChunks[chunkIndex];

            DrawMeshes(cmd, drawCallChunk);
        }
    }
}