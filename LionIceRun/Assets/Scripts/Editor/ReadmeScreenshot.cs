using System.IO;
using UnityEditor;
using UnityEngine;

// README용 게임 화면 스크린샷.
//  - Play 중 Game 뷰를 찍어 저장소 루트의 docs/screenshots/<이름>.png로 저장한다 (같은 이름은 덮어씀)
//  - README.md가 이 파일 이름들을 그대로 쓴다
// 메뉴: LionIceRun > README 스크린샷 > ... (단축키 F9~F12)
public static class ReadmeScreenshot
{
    // Application.dataPath = .../LionIceRun/Assets → 두 단계 위가 저장소 루트
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/screenshots"));

    [MenuItem("LionIceRun/README 스크린샷/1 대기 화면 _F9")]
    static void Title() => Capture("title");

    [MenuItem("LionIceRun/README 스크린샷/2 플레이 중 _F10")]
    static void Play() => Capture("play");

    [MenuItem("LionIceRun/README 스크린샷/3 시련 _F11")]
    static void Trial() => Capture("trial");

    [MenuItem("LionIceRun/README 스크린샷/4 결과 화면 _F12")]
    static void Result() => Capture("result");

    [MenuItem("LionIceRun/README 스크린샷/저장 폴더 열기")]
    static void OpenFolder()
    {
        Directory.CreateDirectory(Folder);
        EditorUtility.RevealInFinder(Folder);
    }

    static void Capture(string name)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("README 스크린샷: Play 중에만 찍을 수 있습니다.");
            return;
        }

        Directory.CreateDirectory(Folder);
        string path = Path.Combine(Folder, name + ".png");
        // 파일은 이번 프레임이 다 그려진 뒤에 저장된다
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"README 스크린샷 저장: {path}");
    }
}
