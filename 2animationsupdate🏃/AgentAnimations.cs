using UnityEngine;
using System.Collections.Generic;

public enum AnimationState
{
    idle,
    walking,
    attack
}

public class AgentAnimations
{
    private readonly Agent agent;
    private readonly Animator animator;

    private AnimationState currentState = AnimationState.idle;

    private readonly Dictionary<AnimationState, RuntimeAnimatorController> controllerCache
        = new Dictionary<AnimationState, RuntimeAnimatorController>();

    public AnimationState CurrentState => currentState;

    public AgentAnimations(Agent agent, Animator animator)
    {
        this.agent = agent;
        this.animator = animator;
    }

    public void Init()
    {
        currentState = AnimationState.idle;
        controllerCache.Clear();

        SetState(AnimationState.idle, forceRestart: true);
    }

    // Called explicitly by AgentVisuals whenever something needs a switch
    // (death, attack trigger, etc) — nothing here runs automatically per frame.
    public void SetState(AnimationState newState, bool forceRestart = false)
    {
        if (currentState == newState && !forceRestart)
            return;

        currentState = newState;

        RuntimeAnimatorController controller = GetController(newState);

        if (controller == null)
        {
            Debug.LogWarning($"AgentAnimations: no controller found for {agent.type} / {newState}");
            return;
        }

        ApplyController(controller, forceRestart);
    }

    private RuntimeAnimatorController GetController(AnimationState state)
    {
        if (controllerCache.TryGetValue(state, out RuntimeAnimatorController cached))
            return cached;

        string path = $"Sprites/Agents/{agent.type}/{agent.type}{state}";
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(path);

        controllerCache[state] = controller;
        return controller;
    }

    private void ApplyController(RuntimeAnimatorController controller, bool forceRestart)
    {
        if (animator == null || controller == null)
            return;

        if (animator.runtimeAnimatorController == controller && !forceRestart)
            return;

        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        animator.Play(0, 0, 0f);
        animator.Update(0f);
    }
}
