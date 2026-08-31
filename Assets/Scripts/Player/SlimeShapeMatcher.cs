using UnityEngine;

namespace Slimily
{
    public class SlimeShapeMatcher : MonoBehaviour
    {
        [Header("Soft Body Settings")]
        public int nodeCount = 8;
        public float radius = 0.5f;
        public float stiffness = 150f;
        public float damping = 10f;
        public LayerMask collisionLayer;

        [Header("Visuals")]
        public Transform[] boneTransforms;

        private Vector2[] nodePositions;
        private Vector2[] nodeVelocities;
        private Vector2[] targetPositions;

        void Start()
        {
            nodePositions = new Vector2[nodeCount];
            nodeVelocities = new Vector2[nodeCount];
            targetPositions = new Vector2[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                float angle = i * Mathf.PI * 2 / nodeCount;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                nodePositions[i] = (Vector2)transform.position + dir * radius;
            }
        }

        void Update()
        {
            UpdateTargetPositions();
            UpdateNodes(Time.deltaTime);
            UpdateBones();
        }

        private void UpdateTargetPositions()
        {
            for (int i = 0; i < nodeCount; i++)
            {
                float angle = i * Mathf.PI * 2 / nodeCount;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                targetPositions[i] = (Vector2)transform.position + dir * radius;
            }
        }

        private void UpdateNodes(float deltaTime)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                Vector2 displacement = nodePositions[i] - targetPositions[i];
                Vector2 springForce = -stiffness * displacement;
                Vector2 dampingForce = -damping * nodeVelocities[i];
                
                Vector2 acceleration = springForce + dampingForce;
                nodeVelocities[i] += acceleration * deltaTime;
                Vector2 nextPosition = nodePositions[i] + nodeVelocities[i] * deltaTime;

                Vector2 directionToNode = nextPosition - (Vector2)transform.position;
                float distanceToNode = directionToNode.magnitude;
                
                RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToNode.normalized, distanceToNode, collisionLayer);
                
                if (hit.collider != null)
                {
                    nodePositions[i] = hit.point;
                    nodeVelocities[i] = Vector2.zero;
                }
                else
                {
                    nodePositions[i] = nextPosition;
                }
            }
        }

        private void UpdateBones()
        {
            if (boneTransforms == null || boneTransforms.Length != nodeCount) return;

            for (int i = 0; i < nodeCount; i++)
            {
                boneTransforms[i].position = nodePositions[i];
            }
        }
    }
}