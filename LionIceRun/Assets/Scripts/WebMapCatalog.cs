using System;
using UnityEngine;

// 브라우저에는 디렉터리 열거 기능이 없어 빌드 때 JSON 원문과 파일 목록을 함께 묶는다.
public static class WebMapCatalog
{
    [Serializable]
    public class Bundle
    {
        public string[] files;
        public string[] json;
    }

    static Bundle bundle;
    static Bundle Read()
    {
        if (bundle != null) return bundle;
        var asset = Resources.Load<TextAsset>("WebRuntime/Maps");
        if (asset == null) throw new InvalidOperationException("웹 맵 묶음이 없습니다. WebBuildSupport로 다시 빌드하세요.");
        bundle = JsonUtility.FromJson<Bundle>(asset.text);
        if (bundle?.files == null || bundle.json == null || bundle.files.Length != bundle.json.Length)
            throw new InvalidOperationException("웹 맵 묶음의 파일 목록이 잘못되었습니다.");
        return bundle;
    }

    public static string[] ListFiles() => (string[])Read().files.Clone();
    public static MapData Load(string file)
    {
        if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) file += ".json";
        var data = Read();
        int index = Array.FindIndex(data.files, name => string.Equals(name, file, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) return MapData.FromJson(data.json[index]);
        Debug.LogError("웹 맵 묶음에 없는 파일: " + file);
        return null;
    }
}
