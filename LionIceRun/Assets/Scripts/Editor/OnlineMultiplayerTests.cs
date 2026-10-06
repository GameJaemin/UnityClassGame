using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

// 네트워크 패키지의 실제 버퍼와 맵 데이터로 준비 규칙/패킷 형식을 검사한다.
public static class OnlineMultiplayerTests
{
    [MenuItem("LionIceRun/친구 멀티 규칙 검사")]
    public static void Run()
    {
        int checks = 0;
        void Check(bool passed, string name) { if (!passed) throw new Exception("온라인 검사 실패: " + name); checks++; }
        Check(OnlineRules.Code(" ab12 cd ") == "AB12CD", "코드 정규화");
        Check(OnlineRules.Nickname("<b>친구</b>\n").IndexOf('<') < 0, "닉네임 태그/제어 문자 제외");
        Check(OnlineRules.Nickname(new string('가', 30)).Length == 16, "닉네임 길이 제한");
        Check(OnlineRules.Newer(1, 0) && !OnlineRules.Newer(1, 1) && !OnlineRules.Newer(1, 2), "중복/오래된 입력 거부");
        Check(OnlineRules.Newer(0, uint.MaxValue), "입력 순번 순환");
        var map = MapData.Filled("검사 맵", 15, 15);
        string hash = OnlineRules.MapHash(map);
        map.name = "표시 이름 변경";
        Check(OnlineRules.MapHash(map) == hash, "표시 이름은 지형 해시에 영향 없음");
        map.Set(new Vector2Int(1, 1), MapData.Empty);
        Check(OnlineRules.MapHash(map) != hash, "지형 변경은 다른 해시");
        var a = new OnlineMember { maps = new[] { new OnlineMapStamp { file = "a.json", hash = "same" }, new OnlineMapStamp { file = "b.json", hash = "different" } } };
        var b = new OnlineMember { maps = new[] { new OnlineMapStamp { file = "a.json", hash = "same" }, new OnlineMapStamp { file = "b.json", hash = "changed" } } };
        Check(OnlineRules.CommonMaps(new[] { a, b }).SequenceEqual(new[] { "a.json" }), "공통 지형만 후보로 선택");
        Check(OnlineRules.CommonMaps(new[] { a, new OnlineMember() }).Count == 0, "맵 목록 없는 참가자는 시작 불가");
        Check(OnlineRules.TrySpawns(map, 4, out var spawns) && spawns.Distinct().Count() == 4, "네 참가자 시작 칸 보충");
        var isolated = MapData.Filled("고립", 5, 5);
        isolated.rows = new[] { "I...I", ".....", "..I..", ".....", "I...I" };
        Check(!OnlineRules.TrySpawns(isolated, 4, out _), "고립 섬에 임의 배치하지 않음");
        foreach (string file in MapData.ListFiles())
        {
            var saved = MapData.Load(file);
            Check(saved != null && OnlineRules.TrySpawns(saved, 4, out var cells) && cells.Distinct().Count() == 4,
                "실제 맵 네 명 배치: " + file);
        }
        using (var writer = new FastBufferWriter(1024, Allocator.Temp))
        {
            writer.WriteValueSafe(3); writer.WriteValueSafe(2u); writer.WriteValueSafe(4);
            for (ulong i = 1; i <= 4; i++)
            {
                writer.WriteValueSafe(i); writer.WriteValueSafe(true);
                writer.WriteValueSafe(new Vector3(i, 1, 2)); writer.WriteValueSafe(Quaternion.identity);
                writer.WriteValueSafe(Vector3.zero); writer.WriteValueSafe((byte)0); writer.WriteValueSafe(true);
                writer.WriteValueSafe(-1f); writer.WriteValueSafe(false); writer.WriteValueSafe(0f);
                writer.WriteValueSafe(false); writer.WriteValueSafe(0f); writer.WriteValueSafe(false);
            }
            Check(writer.Length < 1024, "네 명 물리 스냅샷은 비분할 전송 크기 이내");
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                reader.ReadValueSafe(out int round); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out int count);
                Check(round == 3 && sequence == 2 && count == 4, "실제 NGO 버퍼 헤더 복원");
            }
        }
        Debug.Log($"ONLINE_CHECKS_PASSED: {checks}개");
    }
}
