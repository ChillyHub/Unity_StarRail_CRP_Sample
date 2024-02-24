using UnityEngine;

namespace Unity_StarRail_CRP_Sample.Object
{
    public class SinMover : MonoBehaviour
    {
        public float moveLength = 1.0f;
        public float moveSpeed = 1.0f;
        
        private Vector3 _originPosition;
        
        private void Start()
        {
            _originPosition = transform.position;
        }
        
        private void Update()
        {
            float move = Mathf.Sin(Time.time * moveSpeed) * moveLength;
            transform.position = _originPosition + Vector3.up * move;
        }
    }
}