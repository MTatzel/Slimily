using UnityEngine;

namespace Slimily
{
    // Diese Zeile sorgt dafür, dass Unity automatisch die nötigen Renderer hinzufügt
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class JellyMesh : MonoBehaviour
    {
        [Header("Mesh Settings")]
        public float radius = 0.5f;
        public int segments = 30; // Je höher, desto runder der Slime

        [Header("Soft Body Physics")]
        public float stiffness = 150f;
        public float damping = 10f;

        [Header("Slime Shape")]
        public float widthMultiplier = 1.2f;   // Macht den Slime breiter
        public float heightMultiplier = 0.85f; // Macht ihn flacher (Gewicht)
        public float flattenBottom = 0.6f;     // Drückt den Boden extra platt

        private Mesh mesh;
        private Vector3[] baseVertices;
        private Vector3[] currentVertices;
        private Vector3[] velocities;
        private Vector3 lastPosition;

        void Start()
        {
            mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;
            
            GenerateBaseMesh();
            lastPosition = transform.position;
        }

        void GenerateBaseMesh()
        {
            int vertexCount = segments + 1; 
            baseVertices = new Vector3[vertexCount];
            currentVertices = new Vector3[vertexCount];
            velocities = new Vector3[vertexCount];

            baseVertices[0] = Vector3.zero;
            float angleStep = (Mathf.PI * 2f) / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                
                // Grundlegende Breite und Höhe (Oval-Form durch das Gewicht)
                float x = Mathf.Cos(angle) * radius * widthMultiplier;
                float y = Mathf.Sin(angle) * radius * heightMultiplier;

                // Den Boden zusätzlich abflachen (alles was unter der Mitte liegt)
                if (y < 0)
                {
                    y *= flattenBottom;
                }

                baseVertices[i + 1] = new Vector3(x, y, 0);
            }

            baseVertices.CopyTo(currentVertices, 0);

            int[] triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1; 
                triangles[i * 3 + 2] = i + 1; 
            }

            mesh.vertices = currentVertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        void Update()
        {
            // Wie weit hat sich das Hauptobjekt in diesem Frame bewegt?
            Vector3 positionDelta = transform.position - lastPosition;
            lastPosition = transform.position;

            // Das Zentrum bleibt immer starr in der Mitte
            currentVertices[0] = baseVertices[0];

            // Die Randpunkte wabbeln hinterher
            for (int i = 1; i < currentVertices.Length; i++)
            {
                // Optische Trägheit: Punkte entgegengesetzt zur Bewegung verschieben
                currentVertices[i] -= positionDelta;

                // Federkraft: Zieht den Punkt zurück zu seiner Basis-Position
                Vector3 displacement = currentVertices[i] - baseVertices[i];
                Vector3 force = -stiffness * displacement - damping * velocities[i];

                velocities[i] += force * Time.deltaTime;
                currentVertices[i] += velocities[i] * Time.deltaTime;
            }

            // Das Netz im Spiel aktualisieren
            mesh.vertices = currentVertices;
            mesh.RecalculateBounds();
        }
    }
}
