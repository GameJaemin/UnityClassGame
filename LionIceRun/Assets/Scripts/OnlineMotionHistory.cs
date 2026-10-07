using UnityEngine;

// 호스트가 처리한 입력 위치와 그때의 로컬 위치를 비교한다. 아직 처리되지 않은 이동은 보존한다.
public sealed class OnlineMotionHistory
{
    const int Capacity = 256;
    readonly uint[] sequences = new uint[Capacity];
    readonly Vector3[] positions = new Vector3[Capacity];
    readonly bool[] valid = new bool[Capacity];
    public void Clear() => System.Array.Clear(valid, 0, valid.Length);
    public void Record(uint sequence, Vector3 position)
    {
        int slot = (int)(sequence % Capacity);
        sequences[slot] = sequence; positions[slot] = position; valid[slot] = true;
    }
    public bool Correction(uint acknowledged, Vector3 authoritative, out Vector3 error)
    {
        int slot = (int)(acknowledged % Capacity);
        error = Vector3.zero;
        if (!valid[slot] || sequences[slot] != acknowledged) return false;
        error = authoritative - positions[slot];
        if (!float.IsFinite(error.x) || !float.IsFinite(error.y) || !float.IsFinite(error.z)) return false;
        for (int i = 0; i < Capacity; i++)
        {
            if (!valid[i]) continue;
            if (OnlineRules.Newer(acknowledged, sequences[i])) valid[i] = false;
            else positions[i] += error;
        }
        return true;
    }
}
