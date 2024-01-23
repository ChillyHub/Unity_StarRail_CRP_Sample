using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public class TAACameraData
    {
        public Matrix4x4 jitter { get; private set; }
        public Matrix4x4 projection { get; private set; }
        public Matrix4x4 projectionNoJitter { get; private set; }
        public Matrix4x4 viewGpuProjectionNoJitter { get; private set; }

        public Matrix4x4 previousJitter { get; private set; }
        public Matrix4x4 previousProjection { get; private set; }
        public Matrix4x4 previousProjectionNoJitter { get; private set; }
        public Matrix4x4 previousViewGpuProjectionNoJitter { get; private set; }

        public Matrix4x4 nextJitter { get; private set; }
        public Matrix4x4 nextProjection { get; private set; }
        public Matrix4x4 nextProjectionNoJitter { get; private set; }

        public bool isTAAEnabled { get; private set; }
        
        public int lastFrameIndex { get; private set; }
        
        private Camera _camera;
        private float _lastFrameAspect;

        public TAACameraData()
        {
            Reset();
        }

        public void Update(ref CameraData cameraData, bool taaEnabled)
        {
            if (cameraData.camera.aspect != _lastFrameAspect)
            {
                Reset();

                _lastFrameAspect = cameraData.camera.aspect;
                cameraData.camera.ResetProjectionMatrix();
                cameraData.camera.nonJitteredProjectionMatrix = cameraData.camera.projectionMatrix;
            }
            
            isTAAEnabled = taaEnabled;

            if (taaEnabled && lastFrameIndex != Time.frameCount)
            {
                _camera = cameraData.camera;
                
                // Previous frame
                previousJitter = jitter;
                previousProjection = projection;
                previousProjectionNoJitter = projectionNoJitter;
                previousViewGpuProjectionNoJitter = viewGpuProjectionNoJitter;

                // Current frame 
                jitter = nextJitter;
                projection = cameraData.camera.projectionMatrix;
                projectionNoJitter = cameraData.camera.nonJitteredProjectionMatrix;
                viewGpuProjectionNoJitter = GL.GetGPUProjectionMatrix(projectionNoJitter, true) 
                                            * cameraData.GetViewMatrix();

                // Update next frame camera
                nextJitter = CalculateJitterMatrix(ref cameraData);
                nextProjection = nextJitter * projectionNoJitter;
                nextProjectionNoJitter = projectionNoJitter;

                cameraData.camera.projectionMatrix = nextProjection;
                cameraData.camera.nonJitteredProjectionMatrix = nextProjectionNoJitter;
                
                lastFrameIndex = Time.frameCount;
            }
        }

        private void Reset()
        {
            jitter = Matrix4x4.identity;
            projection = Matrix4x4.identity;
            projectionNoJitter = Matrix4x4.identity;

            previousJitter = Matrix4x4.identity;
            previousProjection = Matrix4x4.identity;
            previousProjectionNoJitter = Matrix4x4.identity;

            nextJitter = Matrix4x4.identity;
            nextProjection = Matrix4x4.identity;
            nextProjectionNoJitter = Matrix4x4.identity;

            isTAAEnabled = false;
            lastFrameIndex = -1;
        }

        private Matrix4x4 CalculateJitterMatrix(ref CameraData cameraData)
        {
            Matrix4x4 jitterMat = Matrix4x4.identity;
            
            if (isTAAEnabled)
            {
                int taaFrameIndex = Time.frameCount;

                float actualWidth = cameraData.cameraTargetDescriptor.width;
                float actualHeight = cameraData.cameraTargetDescriptor.height;

                var jitter = CalculateJitter(taaFrameIndex);

                float offsetX = jitter.x * (2.0f / actualWidth);
                float offsetY = jitter.y * (2.0f / actualHeight);

                jitterMat = Matrix4x4.Translate(new Vector3(offsetX, offsetY, 0.0f));
            }

            return jitterMat;
        }
        
        private Vector2 CalculateJitter(int frameIndex)
        {
            // Halton sequence (2, 3)
            float jitterX = GetHaltonSequence((frameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = GetHaltonSequence((frameIndex & 1023) + 1, 3) - 0.5f;

            return new Vector2(jitterX, jitterY);
        }
        
        private float GetHaltonSequence(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;

                index /= radix;
                fraction /= radix;
            }

            return result;
        }
    }
}