using UnityEngine;

namespace Unity_StarRail_CRP_Sample.Object
{
    public class RotateMover : MonoBehaviour
    {
        public float moveSpeed = 1.0f;
        
        private void Update()
        {
            Matrix4x4 rotateMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, moveSpeed, 0));
            Vector4 oldPosition = new Vector4(transform.position.x, transform.position.y, transform.position.z, 1);
            transform.position = (Vector3)(rotateMatrix * oldPosition);
        }
    }
}