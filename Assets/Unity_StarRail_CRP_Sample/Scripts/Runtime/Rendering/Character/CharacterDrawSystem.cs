using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public abstract class CharacterDrawSystem
    {
        public abstract void Execute(CommandBuffer cmd, int chunkIndex);

        protected void DrawMeshes(CommandBuffer cmd, CharacterDrawCallChunk drawCallChunk)
        {
            for (int i = 0; i < drawCallChunk.drawCallCount; i++)
            {
                cmd.DrawMesh(
                    drawCallChunk.GetMesh(i),
                    drawCallChunk.GetObjectToWorld(i), 
                    drawCallChunk.GetMaterial(i), 
                    drawCallChunk.GetSubMeshIndex(i),
                    drawCallChunk.GetPassIndex(i),
                    drawCallChunk.GetPropertyBlock(i));
            }
        }
        
        protected void DrawRenderers(CommandBuffer cmd, CharacterDrawCallChunk drawCallChunk)
        {
            for (int i = 0; i < drawCallChunk.drawCallCount; i++)
            {
                cmd.DrawRenderer(
                    drawCallChunk.GetRenderer(i), 
                    drawCallChunk.GetMaterial(i), 
                    drawCallChunk.GetSubMeshIndex(i),
                    drawCallChunk.GetPassIndex(i));
            }
        }
    }
}