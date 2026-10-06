using System.Collections.Generic;
using UnityEngine;

// 예고 도형과 시련 모델은 호스트에서 만들고, 참가자 화면에는 충돌 없는 복제만 만든다.
public class OnlineWorldVisual : MonoBehaviour
{
    public static readonly List<OnlineWorldVisual> All = new List<OnlineWorldVisual>();
    static int nextId;
    public int id, kind;
    public static void Track(GameObject go, int kind)
    {
        if (!OnlineGameSession.InRoom || !OnlineGameSession.Authority) return;
        var visual = go.AddComponent<OnlineWorldVisual>();
        visual.id = ++nextId;
        visual.kind = kind;
    }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDestroy() => All.Remove(this);
    public OnlineVisualState Capture()
    {
        var renderer = GetComponentInChildren<Renderer>();
        var particles = GetComponent<ParticleSystem>();
        var animator = GetComponent<Animator>();
        var state = animator != null && animator.runtimeAnimatorController != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
        return new OnlineVisualState
        {
            id = id, kind = kind, position = transform.position, rotation = transform.rotation,
            scale = transform.lossyScale, visible = particles != null ? particles.isPlaying : renderer == null || renderer.enabled,
            animation = state.fullPathHash, animationTime = state.normalizedTime
        };
    }
}
