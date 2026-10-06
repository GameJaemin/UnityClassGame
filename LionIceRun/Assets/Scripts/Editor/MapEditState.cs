using UnityEngine;

// 맵툴 창이 편집 중인 맵. Undo(Ctrl+Z)에 맡기려고 ScriptableObject에 담는다 (에셋으로 저장하지 않음).
// Unity가 스크립트 파일로 타입을 찾을 수 있게 파일 이름과 클래스 이름을 같게 둔다.
public class MapEditState : ScriptableObject
{
    public MapData map;
}
