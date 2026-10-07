using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 저장된 조작은 솔로/온라인에 공통 적용한다. 입력 대기나 글 작성은 경기 입력만 막는다.
public static class GamePreferences
{
    public static readonly string[] Actions = { "Up", "Left", "Down", "Right", "Jump", "Dive", "Push", "Emote", "Chat" };
    public static readonly string[] Labels = { "앞으로", "왼쪽", "뒤로", "오른쪽", "점프", "다이브", "밀치기", "감정표현", "채팅" };
    static readonly string[] Defaults = { "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d", "<Keyboard>/space", "<Keyboard>/leftShift", "<Mouse>/leftButton", "<Keyboard>/t", "<Keyboard>/enter" };
    public static event Action Changed;
    public static bool ModalInput;
    public static bool Anonymous => PlayerPrefs.GetInt("LionIceRun.Anonymous", 0) != 0;
    public static bool ChatVisible => PlayerPrefs.GetInt("LionIceRun.ChatVisible", 1) != 0;
    public static int MapSelectionSpeed => Mathf.Clamp(PlayerPrefs.GetInt("LionIceRun.MapSelectionSpeed", 0), 0, 2);
    public static string MapSelectionLabel => new[] { "빠르게", "보통", "여유롭게" }[MapSelectionSpeed];
    public static float MapSelectionSeconds => Mathf.Clamp(PlayerPrefs.GetFloat("LionIceRun.MapSelectionSeconds", PlayerPrefs.HasKey("LionIceRun.MapSelectionSpeed") ? new[] { 2f, 4f, 7f }[MapSelectionSpeed] : 5f),1f,15f);
    public static float MapSpinSeconds => MapSelectionSeconds*.3f;
    public static float MapBrakeSeconds => MapSelectionSeconds*.55f;
    public static float MapConfirmSeconds => MapSelectionSeconds*.15f;
    public static string Nickname => OnlineRules.OptionalNickname(PlayerPrefs.GetString("LionIceRun.Nickname", ""));
    public static string Animal(int skin) => new[] { "사자", "북극곰", "호랑이" }[Mathf.Clamp(skin, 0, 2)];
    public static string PublicName(int skin)
    {
        string nickname = Nickname;
        return Anonymous || nickname.Length == 0 ? Animal(skin) : nickname;
    }
    public static string Path(int index) => PlayerPrefs.GetString("LionIceRun.Key." + Actions[index], Defaults[index]);
    public static string KeyLabel(int index)
    {
        string path = Path(index);
        if (path == "<Keyboard>/leftShift" || path == "<Keyboard>/rightShift") return "Shift";
        if (path == "<Keyboard>/space") return "Space";
        if (path == "<Keyboard>/enter") return "Enter";
        if (path == "<Mouse>/leftButton") return "마우스 왼쪽";
        if (path == "<Mouse>/rightButton") return "마우스 오른쪽";
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
    }
    public static bool InputBlocked
    {
        get
        {
            if (ModalInput || WebTextInput.Active || (IceExperienceUI.Instance != null && IceExperienceUI.Instance.ChatOpen)) return true;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponentInParent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        }
    }
    public static bool SetBinding(int index, string path)
    {
        if (index < 0 || index >= Actions.Length || string.IsNullOrEmpty(path) || path == "<Keyboard>/escape") return false;
        if (!path.StartsWith("<Keyboard>/") && path != "<Mouse>/leftButton" && path != "<Mouse>/rightButton") return false;
        for (int i = 0; i < Actions.Length; i++) if (i != index && Path(i) == path) return false;
        PlayerPrefs.SetString("LionIceRun.Key." + Actions[index], path); Apply(); return true;
    }
    public static void SetAnonymous(bool value) { PlayerPrefs.SetInt("LionIceRun.Anonymous", value ? 1 : 0); Apply(); }
    public static void SetChatVisible(bool value) { PlayerPrefs.SetInt("LionIceRun.ChatVisible", value ? 1 : 0); Apply(); }
    public static void SetMapSelectionSpeed(int value) { PlayerPrefs.SetInt("LionIceRun.MapSelectionSpeed", Mathf.Clamp(value, 0, 2)); PlayerPrefs.DeleteKey("LionIceRun.MapSelectionSeconds"); Apply(); }
    public static void SetMapSelectionSeconds(float value)
    {
        if(float.IsNaN(value) || float.IsInfinity(value)) return;
        PlayerPrefs.SetFloat("LionIceRun.MapSelectionSeconds",Mathf.Clamp(value,1f,15f)); Apply();
    }
    public static void ResetBindings()
    { foreach (string action in Actions) PlayerPrefs.DeleteKey("LionIceRun.Key." + action); Apply(); }
    public static void Apply()
    {
        var asset = InputSystem.actions;
        if (asset != null)
        {
            var move = asset.FindAction("Player/Move");
            if (move != null)
                for (int i = 0; i < move.bindings.Count; i++)
                {
                    var b = move.bindings[i];
                    if (!b.isPartOfComposite || !b.path.StartsWith("<Keyboard>/")) continue;
                    int index = b.name.ToLowerInvariant() switch { "up" => 0, "left" => 1, "down" => 2, "right" => 3, _ => -1 };
                    if (index >= 0) move.ApplyBindingOverride(i, Path(index));
                }
            var jump = asset.FindAction("Player/Jump");
            if (jump != null) for (int i = 0; i < jump.bindings.Count; i++)
                if (jump.bindings[i].path.StartsWith("<Keyboard>/")) jump.ApplyBindingOverride(i, Path(4));
        }
        PlayerPrefs.Save(); Changed?.Invoke();
    }
    public static InputAction CreateButton(string name, int index, string gamepad = null)
    {
        var action = new InputAction(name, InputActionType.Button, Path(index));
        if (gamepad != null) action.AddBinding(gamepad);
        return action;
    }
    public static void RefreshButton(InputAction action, int index) { action?.ApplyBindingOverride(0, Path(index)); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntime() { ModalInput = false; Changed = null; }
}
