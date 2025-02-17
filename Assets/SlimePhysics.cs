using System.Collections.Generic;
using UnityEngine;

public class SlimePhysics : MonoBehaviour
{
    public int numBodies = 16; // Anzahl der K�rper
    public float radius = 1f; // Radius des �u�eren Kreises
    public float springStrength = 10f; // Erh�hte Federst�rke f�r besseren Zusammenhalt
    public float damping = 1f; // Erh�hte D�mpfung f�r stabileres Verhalten
    public float breakForce = Mathf.Infinity; // Unbegrenzte Haltekraft der Federn
    private List<GameObject> bodies = new List<GameObject>(); // Liste aller K�rper

    void Start()
    {
        CreateSlime();
    }

    void CreateSlime()
    {
        // Erstelle die K�rper im Kreis
        for (int i = 0; i < numBodies; i++)
        {
            float angle = i * Mathf.PI * 2 / numBodies;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            // Erstellt einen neuen Kreis-Body direkt im Code
            GameObject body = new GameObject("Body_" + i);
            body.transform.position = pos;
            body.transform.parent = transform;

            // F�gt SpriteRenderer mit einer einfachen Kreis-Textur hinzu
            SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("Circle"); ;            // Standard-Kreis-Sprite
            sr.color = Color.green; // Falls kein eigenes Sprite genutzt wird
            sr.sortingOrder = 10; // Stellt sicher, dass es sichtbar ist

            // F�gt Rigidbody2D hinzu
            Rigidbody2D rb = body.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1; // Schwerkraft aktiv
            rb.mass = 1f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Verhindert unkontrollierte Drehungen

            // F�gt CircleCollider2D hinzu
            CircleCollider2D collider = body.AddComponent<CircleCollider2D>();
            collider.radius = 0.125f; // Passend zur kleinen K�rpergr��e

            bodies.Add(body);
        }

        // Verbinde alle K�rper mit st�rkeren und haltbareren Federn
        for (int i = 0; i < numBodies; i++)
        {
            Rigidbody2D rbA = bodies[i].GetComponent<Rigidbody2D>();

            for (int j = 0; j < numBodies; j++)
            {
                if (i != j) // Verhindert Selbstverbindungen
                {
                    Rigidbody2D rbB = bodies[j].GetComponent<Rigidbody2D>();
                    CreateSpringJoint(rbA, rbB);
                }
            }
        }
    }

    void CreateSpringJoint(Rigidbody2D rbA, Rigidbody2D rbB)
    {
        SpringJoint2D joint = rbA.gameObject.AddComponent<SpringJoint2D>();
        joint.connectedBody = rbB;
        joint.distance = Vector2.Distance(rbA.position, rbB.position) * 0.9f; // Etwas k�rzer f�r mehr Zusammenhalt
        joint.frequency = springStrength;
        joint.dampingRatio = damping;
        joint.autoConfigureDistance = false;
        joint.breakForce = breakForce; // Sicherstellen, dass die Joints nicht rei�en
    }
}
