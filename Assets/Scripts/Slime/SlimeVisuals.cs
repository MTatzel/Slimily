using UnityEngine;

public class SlimeVisuals : MonoBehaviour
{
    private PlatformerSlimeController controller;
    private Transform visualTransform;
    private BoxCollider2D boxCollider;
    private PlatformerPhysicsData data;
    private SpriteRenderer visualRenderer;

    private Color defaultSpriteColor = Color.white;
    private Vector3 defaultVisualScale = Vector3.one;
    private Vector2 currentScaleDeform = Vector2.one;
    private float currentBalloonScale = 1f;

    public void Initialize(
        PlatformerSlimeController slimeController,
        Transform targetVisualTransform,
        BoxCollider2D collider,
        PlatformerPhysicsData physicsData)
    {
        controller = slimeController;
        visualTransform = targetVisualTransform;
        boxCollider = collider;
        data = physicsData;

        if (visualTransform != null)
        {
            defaultVisualScale = visualTransform.localScale;
            visualRenderer = visualTransform.GetComponentInChildren<SpriteRenderer>();
        }
        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        if (visualRenderer != null)
        {
            defaultSpriteColor = visualRenderer.color;
        }
    }

    public void ResetVisuals()
    {
        if (visualRenderer != null)
        {
            visualRenderer.color = defaultSpriteColor;
        }
        if (visualTransform != null)
        {
            visualTransform.localScale = defaultVisualScale;
        }
        currentScaleDeform = Vector2.one;
        currentBalloonScale = 1f;
    }

    public void ApplySquashAndStretch(float scaleX, float scaleY)
    {
        if (data != null && !data.enableSquashAndStretch) return;
        currentScaleDeform = new Vector2(scaleX, scaleY);
    }

    public void UpdateVisuals(float invincibilityTimer)
    {
        transform.rotation = Quaternion.identity;

        UpdateInvincibilityBlink(invincibilityTimer);

        if (visualTransform == null || controller == null || boxCollider == null) return;

        // 1. Ziel-Normale bestimmen (Beim Schweben in der Luft immer aufrecht!)
        Vector2 desiredVisualNormal = controller.TargetSurfaceNormal;
        bool isFloating = controller.Abilities != null && controller.Abilities.IsBalloonFloating;

        if (isFloating)
        {
            desiredVisualNormal = Vector2.up;
        }
        else if (controller.IsOnOuterCorner)
        {
            desiredVisualNormal = controller.EffectiveSurfaceNormal;
        }
        else if (controller.IsInInnerCornerPocket && controller.InnerCornerAdjacentNormal.sqrMagnitude > 0.1f)
        {
            desiredVisualNormal = (controller.CurrentSurfaceNormal + controller.InnerCornerAdjacentNormal * 0.75f).normalized;
        }

        float targetAngle = Mathf.Atan2(desiredVisualNormal.y, desiredVisualNormal.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);

        float rotSpeed = (data != null && data.alignRotationSpeed > 0f) ? data.alignRotationSpeed : 680f;
        visualTransform.rotation = Quaternion.RotateTowards(visualTransform.rotation, targetRot, rotSpeed * Time.deltaTime);

        // 2. Lokaler Offset zur Fläche
        float halfColHeight = boxCollider.size.y * 0.5f;
        Vector2 targetOffset = (Vector2)boxCollider.offset - desiredVisualNormal * (halfColHeight - 0.26f);
        visualTransform.localPosition = Vector2.MoveTowards(visualTransform.localPosition, targetOffset, 20f * Time.deltaTime);

        // 3. Squash & Stretch Rückfederung
        float recovery = (data != null && data.squashRecoverySpeed > 0f) ? data.squashRecoverySpeed : 12f;
        currentScaleDeform = Vector2.MoveTowards(currentScaleDeform, Vector2.one, recovery * Time.deltaTime);

        // 4. Ballon-Aufblasen beim Schweben (inkl. leichtem Pulsieren)
        float targetBalloon = 1f;
        if (isFloating)
        {
            float baseInflate = (data != null && data.balloonScaleMultiplier > 1f) ? data.balloonScaleMultiplier : 1.35f;
            float wobble = Mathf.Sin(Time.time * 14f) * 0.035f;
            targetBalloon = baseInflate + wobble;
        }

        float inflateSpeed = isFloating ? 10f : 14f;
        currentBalloonScale = Mathf.MoveTowards(currentBalloonScale, targetBalloon, inflateSpeed * Time.deltaTime);

        visualTransform.localScale = new Vector3(
            defaultVisualScale.x * currentScaleDeform.x * currentBalloonScale,
            defaultVisualScale.y * currentScaleDeform.y * currentBalloonScale,
            defaultVisualScale.z
        );
    }

    private void UpdateInvincibilityBlink(float invincibilityTimer)
    {
        if (visualRenderer == null) return;

        if (invincibilityTimer <= 0f)
        {
            visualRenderer.color = defaultSpriteColor;
        }
        else
        {
            float blink = Mathf.PingPong(Time.time * 18f, 1f);
            visualRenderer.color = Color.Lerp(defaultSpriteColor, new Color(1f, 0.2f, 0.2f, 0.45f), blink);
        }
    }
}