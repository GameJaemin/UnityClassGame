using System;

// 좌표·크기·이동 거리는 그리드 칸 단위. 피스톤과 막대의 높이만 미터 단위.
[Serializable]
public class MapGimmickData
{
    public string kind;
    public float x, z, width = 3, depth = 3;
    public float dx, dz, period = 8, phase, speed = 25, angle;
    public float hitSpeed = 10, liftSpeed = 3.2f, knockDownSeconds = 1.05f;
    public float tiltAngle = 18, activeDuration = 5;
    public static bool IsSurface(string kind) => kind == "move" || kind == "rotate" || kind == "conveyor" || kind == "seesaw" || kind == "blink";
    public static bool IsSupported(string kind) => IsSurface(kind) || kind == "piston" || kind == "sweeper" || kind == "launcher" || kind == "pendulum";
}
