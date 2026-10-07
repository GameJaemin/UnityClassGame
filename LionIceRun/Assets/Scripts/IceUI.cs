using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class IceUI
{
    public static readonly Color Ink = new(.16f,.23f,.28f), Panel = new(.98f,.98f,.94f), Card = new(.81f,.92f,.98f), Mint = new(1f,.76f,.24f), White = Ink, Muted = new(.35f,.43f,.47f);
    public static TMP_FontAsset Font;
    public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var found = parent.Find(name) as RectTransform;
        var rect = found != null ? found : (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        if (found == null) { rect.SetParent(parent,false); rect.gameObject.layer=parent.gameObject.layer; }
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one*.5f;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    public static void Place(Component value, Vector2 position, Vector2 size)
    {
        if (value == null || value.transform is not RectTransform rect) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one*.5f;
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    public static void Stretch(RectTransform rect, float x=0, float y=0)
    { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(x,y); rect.offsetMax=new Vector2(-x,-y); }
    public static IceUIShape Surface(GameObject go, Color color, float radius=24)
    {
        var image = go.GetComponent<Image>(); if (image != null) image.enabled = false;
        foreach (var effect in go.GetComponents<BaseMeshEffect>()) effect.enabled=false;
        GameObject target=go;
        // Unity Graphic은 같은 오브젝트에 둘 수 없어 기존 Image가 있으면 자식 메시를 쓴다.
        if (go.GetComponent<Graphic>() != null && go.GetComponent<IceUIShape>() == null)
        {
            var rect=Rect(go.transform,"IceSurface",Vector2.zero,Vector2.zero); Stretch(rect); rect.SetAsFirstSibling(); target=rect.gameObject;
        }
        var shape = target.GetComponent<IceUIShape>() ?? target.AddComponent<IceUIShape>();
        shape.color=color; shape.bottom=color; shape.radius=Mathf.Min(radius,6);
        shape.edge=new Color(Ink.r,Ink.g,Ink.b,color.a); shape.border=3; shape.Repaint(); return shape;
    }
    public static RectTransform PanelAt(Transform parent, string name, Vector2 position, Vector2 size, Color color, float radius=24)
    { var rect=Rect(parent,name,position,size); Surface(rect.gameObject,color,radius); return rect; }
    public static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize=24, Color? color=null, TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
    {
        var rect=Rect(parent,name,position,size); var text=rect.GetComponent<TMP_Text>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text=value; StyleText(text,fontSize,color ?? White,alignment); return text;
    }
    public static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions alignment=TextAlignmentOptions.Center)
    {
        if (text == null) return;
        if (Font != null) { text.font=Font; text.fontSharedMaterial=Font.material; }
        text.fontSize=size; text.enableAutoSizing=false; text.color=color; text.alignment=alignment;
        text.fontStyle=FontStyles.Normal; text.richText=false; text.raycastTarget=false; text.characterSpacing=0;
        UIConsistency.ApplyWebFont(text);
    }
    public static void StyleButton(Button button, Color color, bool primary=false)
    {
        if (button == null) return;
        button.targetGraphic=Surface(button.gameObject,color,18);
        if(button.targetGraphic.gameObject==button.gameObject)
        {
            button.targetGraphic.enabled=false;
            var face=Rect(button.transform,"ButtonFace",Vector2.zero,Vector2.zero); Stretch(face); face.SetAsFirstSibling();
            button.targetGraphic=Surface(face.gameObject,color,6);
        }
        var shadowRect=Rect(button.transform,"ButtonShadow",Vector2.zero,Vector2.zero);
        Stretch(shadowRect); shadowRect.anchoredPosition=new Vector2(0,-6); shadowRect.SetAsFirstSibling();
        var shadow=Surface(shadowRect.gameObject,new Color(Ink.r,Ink.g,Ink.b,.7f),6);
        shadow.border=0; shadow.raycastTarget=false; shadow.Repaint();
        var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1,1,.85f);
        colors.selectedColor=Color.white; colors.pressedColor=new Color(.85f,.85f,.85f); colors.disabledColor=new Color(.7f,.7f,.7f,.6f); colors.fadeDuration=.08f; button.colors=colors;
        var label=button.GetComponentInChildren<TMP_Text>(true); StyleText(label,primary?28:24,primary?Ink:White);
        var pop=button.GetComponent<UIButtonPop>() ?? button.gameObject.AddComponent<UIButtonPop>(); pop.hoverScale=1.025f; pop.pressScale=.985f;
    }
    public static Button ButtonAt(Transform parent,string name,string value,Vector2 position,Vector2 size,Color? color=null,bool primary=false)
    {
        var rect=Rect(parent,name,position,size); var button=rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
        Label(rect,"Label",value,Vector2.zero,size-Vector2.one*16,24,null,TextAlignmentOptions.Center);
        StyleButton(button,color ?? Card,primary); return button;
    }
    public static void StyleField(TMP_InputField field)
    {
        if (field == null) return;
        field.targetGraphic=Surface(field.gameObject,Color.white,6);
        field.richText=false; field.isRichTextEditingAllowed=false;
        field.textComponent.richText=false;
        field.customCaretColor=true; field.caretColor=Mint; field.caretWidth=2;
        field.selectionColor=new Color(.42f,.94f,.79f,.25f);
        StyleText(field.textComponent,25,White,TextAlignmentOptions.MidlineLeft);
        if (field.placeholder is TMP_Text placeholder) StyleText(placeholder,23,Muted,TextAlignmentOptions.MidlineLeft);
        if (field.textViewport != null) Stretch(field.textViewport,22,10);
        if (field.GetComponent<WebTextInput>() == null && Application.isPlaying) field.gameObject.AddComponent<WebTextInput>();
    }
    public static TMP_InputField FieldAt(Transform parent,string name,string hint,Vector2 position,Vector2 size,int limit)
    {
        var rect=Rect(parent,name,position,size); var field=rect.gameObject.AddComponent<TMP_InputField>();
        var viewport=Rect(rect,"Viewport",Vector2.zero,size); Stretch(viewport,22,10);
        viewport.gameObject.AddComponent<RectMask2D>();
        var label=Label(viewport,"Text","",Vector2.zero,size,24); Stretch(label.rectTransform);
        var placeholder=Label(viewport,"Placeholder",hint,Vector2.zero,size,24); Stretch(placeholder.rectTransform);
        field.textViewport=viewport; field.textComponent=(TextMeshProUGUI)label; field.placeholder=placeholder;
        field.characterLimit=limit; field.lineType=TMP_InputField.LineType.SingleLine; StyleField(field); return field;
    }
}
