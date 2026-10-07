using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Web의 IME는 브라우저 입력창에서 조합하고 완성된 문자열만 TMP에 전달한다.
public class WebTextInput : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public static bool Active { get; private set; }
    TMP_InputField field;
    bool opened;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void IceTextOpen(string receiver, string value, string hint, int limit, float x, float y, float w, float h);
    [DllImport("__Internal")] static extern void IceTextClose();
#endif
    void Awake()
    {
        field = GetComponent<TMP_InputField>();
        field.richText = false; field.isRichTextEditingAllowed = false;
        field.characterValidation = TMP_InputField.CharacterValidation.None;
        field.textComponent.richText = false;
        // SendMessage 대상 이름은 입력창마다 고유해야 한다.
#if UNITY_WEBGL && !UNITY_EDITOR
        gameObject.name = gameObject.name + "_Text_" + GetInstanceID();
#endif
    }
    public void OnSelect(BaseEventData data)
    {
        if (Keyboard.current != null) Keyboard.current.SetIMEEnabled(true);
#if UNITY_WEBGL && !UNITY_EDITOR
        if (opened || !field.interactable) return;
        var corners = new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners);
        var canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        field.readOnly = true; opened = Active = true;
        WebGLInput.captureAllKeyboardInput = false;
        IceTextOpen(gameObject.name, field.text, (field.placeholder as TMP_Text)?.text ?? "", field.characterLimit,
            min.x / Screen.width, 1 - max.y / Screen.height, (max.x - min.x) / Screen.width, (max.y - min.y) / Screen.height);
#endif
    }
    public void OnDeselect(BaseEventData data)
    {
        if (Keyboard.current != null) Keyboard.current.SetIMEEnabled(false);
        Close();
    }
    public void OnNativeText(string value) { if (opened) field.text = value; }
    public void OnNativeSubmit(string value)
    {
        if (!opened) return;
        field.text = value; Close();
        field.DeactivateInputField(); field.onSubmit.Invoke(value);
    }
    public void OnNativeBlur(string value)
    {
        if (!opened) return;
        field.text = value; Close(); field.DeactivateInputField(); field.onEndEdit.Invoke(value);
    }
    void Close()
    {
        if (!opened) return;
        opened = Active = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        IceTextClose(); WebGLInput.captureAllKeyboardInput = true; field.readOnly = false;
#endif
    }
    void OnDisable() => Close();
    void OnDestroy() => Close();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntime() => Active = false;
}
