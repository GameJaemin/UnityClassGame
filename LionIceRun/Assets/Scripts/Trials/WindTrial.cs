using System.Collections;
using UnityEngine;

// 시련: 찬바람.
//  예고: warnTime 동안 "찬바람! →" 안내(화면 기준 화살표) + 눈보라 줄기가 바람 방향으로 흐르기 시작
//  효과: duration 동안 맵 가로·세로 중 한 방향으로 모든 캐릭터를 밀어냄 (공중에 뜨면 더 크게 밀림)
public class WindTrial : TrialBase
{
    [Header("찬바람")]
    [Min(0.3f)] public float warnTime = 1.5f;
    [Min(0.5f)] public float duration = 3f;
    [Tooltip("미는 힘 (가속도). 땅에서 버티는 힘(가속 25)보다 작게")]
    [Min(0f)] public float pushAcceleration = 22f;
    public float strength { get => pushAcceleration; set => pushAcceleration = value; }

    ParticleSystem streaks;

    void Reset()
    {
        startTime = 30f;
        intervalRange = new Vector2(12f, 16f);
        announcement = "찬바람 주의!";
        announcementColor = new Color(0.6f, 0.9f, 1f);
    }

    protected override IEnumerator Run(float elapsed)
    {
        Vector3[] directions = { grid.transform.right, -grid.transform.right, grid.transform.forward, -grid.transform.forward };
        Vector3 direction = directions[Random.Range(0, directions.Length)];
        Toast($"찬바람! {ScreenArrow(direction)}", announcementColor);

        ParticleSystem system = Streaks();
        system.transform.rotation = Quaternion.LookRotation(direction);
        var main = system.main;
        main.startSpeed = 14f;
        system.Play();

        yield return new WaitForSeconds(warnTime);

        for (float t = 0f; t < duration; t += Time.fixedDeltaTime)
        {
            foreach (PlayerController target in Targets()) target.AddPush(direction * strength);
            yield return new WaitForFixedUpdate();
        }
        system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // 바람 방향을 화면 기준 화살표로
    static string ScreenArrow(Vector3 world)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return "";
        Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        float angle = Mathf.Atan2(Vector3.Dot(world, forward), Vector3.Dot(world, right)) * Mathf.Rad2Deg;
        string[] arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };
        int index = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
        return arrows[index];
    }

    // 맵을 덮는 하얀 눈보라 줄기 (한 번 만들어 재사용)
    public void ApplyNetworkWind(bool active, Quaternion rotation)
    {
        if (!active && streaks == null) return;
        ParticleSystem system = Streaks();
        system.transform.rotation = rotation;
        var main = system.main;
        main.startSpeed = 14f;
        if (active && !system.isPlaying) system.Play();
        else if (!active && system.isPlaying) system.Stop();
    }

    ParticleSystem Streaks()
    {
        if (streaks != null) return streaks;
        var go = new GameObject("WindStreaks");
        OnlineWorldVisual.Track(go, 5);
        go.transform.position = grid.transform.position + Vector3.up * 1.2f;
        streaks = go.AddComponent<ParticleSystem>();
        streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = streaks.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        var emission = streaks.emission;
        emission.rateOverTime = 120f;

        // 바람이 불어오는 쪽 가장자리에서 넓게 나와 맵을 가로지름
        var shape = streaks.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        float span = Mathf.Max(grid.Size.x, grid.Size.y) + 6f;
        shape.scale = new Vector3(span, 2.5f, 1f);
        shape.position = new Vector3(0f, 0f, -span * 0.5f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4f;
        renderer.velocityScale = 0.1f;
        if (grid.particleMaterial != null) renderer.sharedMaterial = grid.particleMaterial;
        return streaks;
    }
}
