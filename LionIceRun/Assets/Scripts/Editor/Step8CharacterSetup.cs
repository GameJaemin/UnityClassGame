using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Step 8·9: Blender에서 내보낸 캐릭터 FBX를 게임에 연결한다.
// 메뉴: LionIceRun > Step 8 캐릭터 가져오기 (Blender 스크립트로 FBX를 다시 만든 뒤에도 다시 실행하면 됨)
//  1) FBX 가져오기 설정: Generic 리그, 축 변환 굽기, 클립 이름(Rig|Walk → Walk)과 반복 설정
//  2) 색상표(*_palette.txt)로 URP 머티리얼을 만들어 FBX 머티리얼에 연결
//  3) Animator Controller 생성 (사자 클립을 세 스킨이 함께 사용)
//  4) Player에 PlayerSkin, CharacterAnimator 연결
public static class Step8CharacterSetup
{
    const string ModelFolder = "Assets/Models/Characters";
    const string MaterialFolder = "Assets/Materials/Characters";
    const string ControllerPath = "Assets/Animations/Character.controller";
    static readonly string[] Characters = { "Lion", "PolarBear", "Tiger" };
    static readonly string[] LoopClips = { "Idle", "Walk", "Fall" };

    [MenuItem("LionIceRun/Step 8 캐릭터 가져오기")]
    static void Run()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Characters");

        foreach (string character in Characters)
            ConfigureModel(ModelFolder, character, LoopClips);

        AnimatorController controller = CreateController();
        SetUpPlayer(controller);
    }

    // 캐릭터·방해꾼 공용: Generic 리그, 클립 이름(Rig|Walk → Walk)·반복, 색상표(*_palette.txt)로 URP 재질 연결
    internal static void ConfigureModel(string folder, string character, string[] loopClips)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Characters");
        string path = $"{folder}/{character}.fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Step 8: {path}가 없습니다. Tools/blender/build_all.ps1을 먼저 실행하세요.");
            return;
        }

        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;

        importer.clipAnimations = importer.defaultClipAnimations.Select(clip =>
        {
            string name = clip.takeName.Contains("|") ? clip.takeName.Substring(clip.takeName.IndexOf('|') + 1) : clip.takeName;
            clip.name = name;
            clip.loopTime = loopClips.Contains(name);
            return clip;
        }).ToArray();

        string palettePath = $"{folder}/{character}_palette.txt";
        if (File.Exists(palettePath))
        {
            foreach (string line in File.ReadAllLines(palettePath))
            {
                string[] parts = line.Split(' ');
                if (parts.Length != 4) continue;
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                var color = new Color(float.Parse(parts[1], culture), float.Parse(parts[2], culture), float.Parse(parts[3], culture));
                Material material = GetOrCreateMaterial(parts[0], color);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), parts[0]), material);
            }
        }
        else
        {
            Debug.LogWarning($"Step 8: {palettePath}가 없어 FBX 기본 머티리얼을 씁니다.");
        }

        importer.SaveAndReimport();
    }

    static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static AnimatorController CreateController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath($"{ModelFolder}/Lion.fbx")
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name);
        foreach (string needed in new[] { "Idle", "Walk", "Jump", "Fall", "Dive" })
        {
            if (!clips.ContainsKey(needed))
            {
                Debug.LogError($"Step 8: Lion.fbx에 {needed} 클립이 없습니다.");
                return null;
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("WalkAnimSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Down", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = AddState(machine, "Idle", clips["Idle"], new Vector3(300, 0));
        AnimatorState walk = AddState(machine, "Walk", clips["Walk"], new Vector3(550, 0));
        AnimatorState jump = AddState(machine, "Jump", clips["Jump"], new Vector3(300, 150));
        AnimatorState fall = AddState(machine, "Fall", clips["Fall"], new Vector3(550, 150));
        AnimatorState dive = AddState(machine, "Dive", clips["Dive"], new Vector3(425, 300));
        machine.defaultState = idle;
        walk.speedParameterActive = true;
        walk.speedParameter = "WalkAnimSpeed";

        // 땅 위: 속도로 Idle ↔ Walk
        Transition(idle, walk, 0.1f, (AnimatorConditionMode.Greater, 0.2f, "Speed"), (AnimatorConditionMode.If, 0f, "Grounded"));
        Transition(walk, idle, 0.15f, (AnimatorConditionMode.Less, 0.2f, "Speed"), (AnimatorConditionMode.If, 0f, "Grounded"));

        // 점프로 떠오르면 Jump, 블럭 끝에서 걸어 떨어지면 Fall
        foreach (AnimatorState ground in new[] { idle, walk })
        {
            Transition(ground, jump, 0.05f, (AnimatorConditionMode.IfNot, 0f, "Grounded"), (AnimatorConditionMode.Greater, 0.5f, "VerticalSpeed"));
            Transition(ground, fall, 0.15f, (AnimatorConditionMode.IfNot, 0f, "Grounded"), (AnimatorConditionMode.Less, -2f, "VerticalSpeed"));
        }
        // 점프 최고 높이(약 1m)에서 떨어지는 속도보다 빨라지면 맵 밖으로 떨어지는 중
        Transition(jump, fall, 0.2f, (AnimatorConditionMode.Less, -10f, "VerticalSpeed"));

        // 착지
        Transition(jump, idle, 0.1f, (AnimatorConditionMode.If, 0f, "Grounded"));
        Transition(fall, idle, 0.1f, (AnimatorConditionMode.If, 0f, "Grounded"));

        // 다이브·넘어짐: 어디서든 Dive, 일어나면 Idle
        AnimatorStateTransition toDive = machine.AddAnyStateTransition(dive);
        toDive.canTransitionToSelf = false;
        toDive.hasExitTime = false;
        toDive.duration = 0.05f;
        toDive.AddCondition(AnimatorConditionMode.If, 0f, "Down");
        Transition(dive, idle, 0.2f, (AnimatorConditionMode.IfNot, 0f, "Down"));

        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        return state;
    }

    static void Transition(AnimatorState from, AnimatorState to, float duration, params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        foreach (var c in conditions) transition.AddCondition(c.mode, c.threshold, c.parameter);
    }

    static void SetUpPlayer(AnimatorController controller)
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("Step 8: Player가 씬에 없습니다. 먼저 LionIceRun > Step 3 플레이어 세팅을 실행하세요.");
            return;
        }

        PlayerSkin skin = player.GetComponent<PlayerSkin>();
        if (skin == null) skin = player.gameObject.AddComponent<PlayerSkin>();
        if (player.GetComponent<CharacterAnimator>() == null) player.gameObject.AddComponent<CharacterAnimator>();

        skin.models = Characters.Select(c => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{c}.fbx")).ToArray();
        skin.animatorController = controller;

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = player.gameObject;
        Debug.Log("Step 8 캐릭터 가져오기 완료: 스킨 3종, 머티리얼, Animator Controller를 설정하고 Player에 연결했습니다.");
    }
}
