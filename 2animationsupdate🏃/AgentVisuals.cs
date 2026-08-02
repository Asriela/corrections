using UnityEngine;

public class AgentVisuals
{
    private Agent agent;
    private AgentAnimations animations;

    private Color visualTint = Color.white;

    private const float deadRotationZ = 90f;

    public Color VisualTint => visualTint;
    public AnimationState CurrentAnimationState => animations.CurrentState;

    public AgentVisuals(Agent agent, Animator animator)
    {
        this.agent = agent;
        animations = new AgentAnimations(agent, animator);
    }

    public void Init()
    {
        animations.Init();

        ApplyVisuals();
        RefreshDeathVisuals();
        UpdateShadowSprite();
    }

    public void UpdateFacingFromDirection(Vector3 direction)
    {
        if (agent.spriteRenderer == null || agent.isDead)
            return;

        direction.z = 0f;

        if (Mathf.Abs(direction.x) > agent.flipThreshold)
            agent.spriteRenderer.flipX = direction.x < 0f;
    }

    public void SetSpriteAlpha(float alpha)
    {
        if (agent.spriteRenderer == null)
            return;

        Color c = agent.spriteRenderer.color;
        c.a = Mathf.Clamp01(alpha);
        agent.spriteRenderer.color = c;
    }

    public float GetSpriteAlpha()
    {
        if (agent.spriteRenderer == null)
            return 1f;

        return agent.spriteRenderer.color.a;
    }

    public void ApplyVisuals()
    {
        if (agent == null || agent.spriteRenderer == null)
            return;

        UpdateShadowSprite();
    }

    public void RefreshDeathVisuals()
    {
        if (agent.spriteRenderer != null)
        {
            agent.spriteRenderer.flipX = false;
            agent.spriteRenderer.transform.localRotation = agent.isDead
                ? Quaternion.Euler(0f, 0f, deadRotationZ)
                : Quaternion.identity;
        }

        if (agent.isDead)
        {
            agent.spriteRenderer.sortingLayerName = "Dead";
            if (agent.shadowRenderer != null)
                agent.shadowRenderer.enabled = false;
        }
        else
        {
            agent.spriteRenderer.sortingLayerName = "People";
            if (agent.shadowRenderer != null)
                agent.shadowRenderer.enabled = true;
        }
    }

    private void UpdateShadowSprite()
    {
        if (agent.shadowRenderer == null)
            return;

        if (agent.isDead)
        {
            agent.shadowRenderer.enabled = false;
            return;
        }

        agent.shadowRenderer.enabled = true;

        agent.shadowRenderer.sprite = GetShadowSprite();
    }

    private Sprite GetShadowSprite()
    {
        Sprite shadowSprite = Help.GetSprite($"Sprites/Agents/{agent.type}Shadow");

        if (shadowSprite == null)
        {
            shadowSprite = Help.GetSprite("Sprites/Agents/agentShadow");
        }

        return shadowSprite;
    }

    public void UpdateSorting()
    {
        if (agent.spriteRenderer == null)
            return;

        float adjustedY = agent.transform.position.y - agent.sortingOffset;
        agent.spriteRenderer.sortingOrder = Mathf.RoundToInt(-adjustedY * 100);
    }
}
