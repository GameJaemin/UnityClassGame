using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterSelectionRunner : MonoBehaviour
{
    string output;
    int failures, checks;
    void Check(bool value, string message)
    {
        checks++; if (!value) failures++;
        File.AppendAllText(output, (value ? "PASS " : "FAIL ") + message + "\n");
    }
    IEnumerator Start()
    {
        output = Path.Combine(Application.dataPath, "../../character-selection-results.txt"); File.WriteAllText(output, "");
        yield return null; yield return null;
        var front = FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        front.OpenModes(); yield return null;
        var picker = front.characterSelection;
        Check(picker != null && picker.previous != null && picker.next != null, "모델 선택 버튼 참조");
        if (picker != null)
        {
            picker.Select(0);
            Canvas.ForceUpdateCanvases();
            var position = RectTransformUtility.WorldToScreenPoint(null, picker.next.transform.position);
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            foreach (var hit in hits) File.AppendAllText(output, "HIT " + hit.gameObject.name + "\n");
            bool reached = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == picker.next;
            Check(reached, "다음 버튼 실제 UI 레이캐스트 도달");
            if (reached) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            else picker.next.onClick.Invoke();
            Check(picker.Index == 1, "다음 버튼 북극곰 선택");
            picker.next.onClick.Invoke(); Check(picker.Index == 2, "다음 버튼 호랑이 선택");
            picker.next.onClick.Invoke(); Check(picker.Index == 0, "마지막 뒤 사자 순환");
            picker.previous.onClick.Invoke(); Check(picker.Index == 2, "이전 버튼 역순 순환");
            for (int i = 0; i < 3; i++) Check(picker.models[i].activeSelf == (i == 2), "선택 모델 활성 " + i);
            front.HidePanels(); front.OpenModes(); yield return null;
            picker.next.onClick.Invoke(); Check(picker.Index == 0, "화면 재진입 중복 리스너 없음");
            picker.previous.onClick.RemoveAllListeners(); picker.next.onClick.RemoveAllListeners();
            front.HidePanels(); front.OpenModes(); yield return null;
            picker.next.onClick.Invoke(); Check(picker.Index == 1, "코드 재로딩으로 연결 손실 후 화면 재진입 복구");
            picker.BindButtons(); picker.BindButtons();
            picker.next.onClick.Invoke(); Check(picker.Index == 2, "중복 복구 시 한 칸만 변경");
            picker.SetInteractable(false); picker.next.onClick.Invoke();
            Check(picker.Index == 2, "연결 중 선택 잠금"); picker.SetInteractable(true);
            var previous = picker.previous; picker.previous = null;
            UIConsistency.Apply(front.gameObject.scene, picker.caption.font);
            Check(front.skin.gameObject.activeSelf, "모델 선택 불완전 시 드롭다운 유지");
            picker.previous = previous; UIConsistency.Apply(front.gameObject.scene, picker.caption.font);
            Check(!front.skin.gameObject.activeSelf, "정상 모델 선택과 중복 드롭다운 숨김");
            front.soloButton.onClick.Invoke();
            Check(PlayerPrefs.GetInt("LionIceRun.Skin", -1) == picker.Index, "선택 스킨 저장");
            Check((int)front.session.manager.player.GetComponent<PlayerSkin>().skin == picker.Index, "솔로 플레이어에 선택 스킨 적용");
            front.session.roulette.ShowTitle(); front.OpenModes();
#if UNITY_EDITOR
            UnityEditor.SessionState.SetInt("CharacterSelectionChecks", checks);
            UnityEditor.SessionState.SetInt("CharacterSelectionFailures", failures);
            UnityEditor.SessionState.SetInt("CharacterSelectionIndex", picker.Index);
            UnityEditor.SessionState.SetBool("CharacterSelectionReload", true);
            UnityEditor.EditorUtility.RequestScriptReload();
            yield break;
#endif
        }
        Finish();
    }
    public void ResumeAfterReload()
    {
        output = Path.Combine(Application.dataPath, "../../character-selection-results.txt");
#if UNITY_EDITOR
        checks = UnityEditor.SessionState.GetInt("CharacterSelectionChecks", 0);
        failures = UnityEditor.SessionState.GetInt("CharacterSelectionFailures", 0);
        int before = UnityEditor.SessionState.GetInt("CharacterSelectionIndex", 0);
        var front = FindAnyObjectByType<OnlineFrontend>(FindObjectsInactive.Include);
        var picker = front.characterSelection;
        picker.next.onClick.Invoke();
        Check(picker.Index == (before + 1) % 3, "실제 Play 중 스크립트 재로딩 직후 버튼 동작");
        typeof(OnlineFrontend).GetMethod("SaveName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(front, null);
        Check(PlayerPrefs.GetInt("LionIceRun.Skin", -1) == picker.Index, "선택 스킨 저장");
#endif
        Finish();
    }
    void Finish()
    {
        File.AppendAllText(output, $"CHARACTER_SELECTION_RESULT: {checks} checks, {failures} failures\n");
        Debug.Log($"CHARACTER_SELECTION_RESULT: {checks} checks, {failures} failures");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
#endif
    }
}
