// AgentAnimations.cs
using UnityEngine;
using System.Collections.Generic;

// Dumb mirror, no opinions. Given a string key, loads (and caches) the
// matching RuntimeAnimatorController and swaps it onto the shared Animator.
// Doesn't know AgentAction exists — AgentActionState resolves that via
// AnimationLookup before ever calling in here.
public class AgentAnimations
{
    private const string IdleKey = "idle";

    private readonly Agent agent;
    private readonly Animator animator;

    private string currentKey = IdleKey;

    private readonly Dictionary<string, RuntimeAnimatorController> controllerCache
        = new Dictionary<string, RuntimeAnimatorController>();

    public string CurrentKey => currentKey;

    public AgentAnimations(Agent agent, Animator animator)
    {
        this.agent = agent;
        this.animator = animator;
    }

    public void Init()
    {
        currentKey = IdleKey;
        controllerCache.Clear();
        Play(IdleKey, forceRestart: true);
    }

    // Called by AgentActionState whenever a proposition wins the lock.
    public void Play(string key, bool forceRestart = false)
    {
        if (string.IsNullOrEmpty(key))
            key = IdleKey;

        if (currentKey == key && !forceRestart)
            return;

        RuntimeAnimatorController controller = GetController(key);

        // ✅ NEW: if the requested animation has no controller on disk, fall
        // back to idle instead of silently doing nothing — leaving whatever
        // was previously playing (e.g. a stale mid-attack pose) reads as a
        // bug, not a missing asset.
        if (controller == null && key != IdleKey)
        {
            Debug.LogWarning($"AgentAnimations: no controller found for {agent.type} / {key} — defaulting to idle");
            key = IdleKey;
            controller = GetController(key);
        }

        if (controller == null)
        {
            // idle itself is missing too — nothing sane to fall back to.
            // Log and bail rather than recurse.
            Debug.LogWarning($"AgentAnimations: no controller found for {agent.type} / {key}");
            return;
        }

        currentKey = key;
        ApplyController(controller, forceRestart);
    }

    // Longest clip on this key's controller — AgentActionState uses this to
    // size a OneShot proposition's lock duration off the real clip length.
    public float GetClipDuration(string key)
    {
        RuntimeAnimatorController controller = GetController(key);

        if (controller == null || controller.animationClips == null || controller.animationClips.Length == 0)
            return 0f;

        float longest = 0f;
        foreach (var clip in controller.animationClips)
        {
            if (clip != null && clip.length > longest)
                longest = clip.length;
        }

        return longest;
    }

    private RuntimeAnimatorController GetController(string key)
    {
        if (controllerCache.TryGetValue(key, out RuntimeAnimatorController cached))
            return cached;

        string path = $"Sprites/Agents/{agent.type}/{agent.type}_{key}";
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(path);
        controllerCache[key] = controller;
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
