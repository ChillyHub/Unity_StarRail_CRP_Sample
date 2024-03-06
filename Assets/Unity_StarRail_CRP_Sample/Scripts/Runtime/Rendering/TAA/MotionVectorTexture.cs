using UnityEngine;
using UnityEngine.Rendering;

namespace Unity_StarRail_CRP_Sample
{
    public class MotionVectorTexture
    {
        public static class TextureName
        {
            public static readonly string MotionVectorTexture = "_MotionVectorTexture";
        }
        
        public ref RTHandle Texture => ref _texture[0];

        private readonly RTHandle[] _texture = new RTHandle[1];
        
        public MotionVectorTexture()
        {
            _texture[0] = RTHandles.Alloc(1, 1);
        }
        
        public void Release()
        {
            RTHandles.Release(_texture[0]);
            _texture[0] = null;
        }
    }
}