using System;
using UnityEditor;
using UnityEngine;

// [MapFile] 문자열 칸을 StreamingAssets/Maps 맵 목록 드롭다운으로 보여 준다 (IceGrid의 Map File).
// 맨 위 "(없음)"을 고르면 가로 x 세로 꽉 찬 판.
[CustomPropertyDrawer(typeof(MapFileAttribute))]
public class MapFileDrawer : PropertyDrawer
{
    const string NoneLabel = "(없음: 가로 x 세로 꽉 찬 판)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string[] files = MapData.ListFiles();
        string current = property.stringValue ?? "";
        int index = Array.IndexOf(files, current);

        // 목록에 없는 파일 이름이 들어 있으면 (지워졌거나 오타) 그대로 보여 주고 표시
        bool missing = current.Length > 0 && index < 0;
        var options = new GUIContent[files.Length + 1 + (missing ? 1 : 0)];
        options[0] = new GUIContent(NoneLabel);
        for (int i = 0; i < files.Length; i++) options[i + 1] = new GUIContent(files[i]);
        if (missing) options[options.Length - 1] = new GUIContent($"{current} (파일 없음)");

        int selected = current.Length == 0 ? 0 : missing ? options.Length - 1 : index + 1;
        EditorGUI.BeginProperty(position, label, property);
        int picked = EditorGUI.Popup(position, label, selected, options);
        if (picked != selected)
            property.stringValue = picked == 0 ? "" : picked <= files.Length ? files[picked - 1] : current;
        EditorGUI.EndProperty();
    }
}
