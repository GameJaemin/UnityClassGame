using System;
using System.Collections;
using UnityEngine;

// 얼음 블럭 하나 (폴가이즈 Thin Ice를 바탕으로, 무적 시간 없이 더 긴장감 있게).
// 밟는 순간 바로 한 단계씩: 멀쩡함 → 살짝 금 → 많이 금 → 흔들림(breakDelay 뒤 깨짐).
// 흔들리는 블럭에서도 점프로 탈출할 수는 있지만, 다시 밟으면 즉시 깨진다.
// 한 번이라도 금이 간 블럭은 아무도 밟지 않아도 decayInterval마다 한 단계씩 진행해 결국 깨진다 (천천히 부서짐).
// 연출: 단계마다 금 간 텍스처로 바뀌며 움찔하고 얼음 가루가 튀고, 깨지면 조각으로 부서진다.
public class IceBlock : MonoBehaviour
{
    public enum Stage { Clean, Cracked, HeavyCracked, Shaking, Broken }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    IceGrid grid;
    Renderer blockRenderer;
    Collider blockCollider;
    MaterialPropertyBlock properties;
    float lastStepTime = float.NegativeInfinity;
    Coroutine shaking;
    Coroutine punching;
    Coroutine growing;
    Coroutine decaying;
    float lastStageChangeTime;
    Vector3 restPosition;
    Vector3 restScale;
    int variant;

    public Stage CurrentStage { get; private set; }
    public Vector2Int Cell { get; private set; }
    public bool IsBroken => CurrentStage == Stage.Broken;

    // AI가 도착 전에 저절로 흔들리기 시작할 칸을 피할 수 있도록 남은 시간을 제공한다.
    public float SecondsUntilShaking
    {
        get
        {
            if (CurrentStage >= Stage.Shaking) return 0f;
            if (CurrentStage == Stage.Clean || grid == null || grid.decayInterval <= 0f) return float.PositiveInfinity;
            int stages = (int)Stage.Shaking - (int)CurrentStage;
            return Mathf.Max(0f, stages * grid.decayInterval - (Time.time - lastStageChangeTime));
        }
    }

    public event Action<IceBlock> Broken;
    public event Action<IceBlock> Restored;

    Vector3 TopCenter => transform.position + Vector3.up * (restScale.y * 0.5f);

    public void Init(IceGrid owner, Vector2Int cell)
    {
        grid = owner;
        Cell = cell;
        blockRenderer = GetComponent<Renderer>();
        blockCollider = GetComponent<Collider>();
        properties = new MaterialPropertyBlock();
        restPosition = transform.localPosition;
        restScale = transform.localScale;
        CurrentStage = Stage.Clean;

        // 블럭마다 금 무늬 한 벌을 고르고 가로·세로로 무작위로 뒤집어서 같은 모양이 반복되지 않게
        variant = UnityEngine.Random.Range(0, Mathf.Max(1, grid.crackVariantCount));
        bool flipX = UnityEngine.Random.value < 0.5f, flipY = UnityEngine.Random.value < 0.5f;
        properties.SetVector(BaseMapStId, new Vector4(flipX ? -1f : 1f, flipY ? -1f : 1f, flipX ? 1f : 0f, flipY ? 1f : 0f));
        UpdateLook();
    }

    // 밟힘 1회. 한 단계 진행하고, 이미 흔들리는 블럭이면 즉시 깨진다.
    // 이음새에서 같은 발걸음이 두 번 세어지는 것만 막는다 (touchDebounceTime).
    public void Step()
    {
        if (grid.networkReplica) return;
        if (IsBroken || Time.time - lastStepTime < grid.touchDebounceTime) return;
        lastStepTime = Time.time;

        if (CurrentStage == Stage.Shaking)
        {
            if (shaking != null) StopCoroutine(shaking);
            Break();
            return;
        }

        Advance();
    }

    // 시련(눈덩이): 밟힘 중복 방지 없이 한 단계. 흔들리는 중이면 바로 깨짐
    public void ForceAdvance()
    {
        if (grid.networkReplica) return;
        if (IsBroken) return;
        if (CurrentStage == Stage.Shaking) ForceBreak();
        else Advance();
    }

    // 시련(고드름): 단계와 상관없이 바로 깨짐
    public void ForceBreak()
    {
        if (grid.networkReplica) return;
        if (IsBroken) return;
        if (shaking != null) StopCoroutine(shaking);
        Break();
    }

    // 파워업(얼음 굳히기): 금 간·흔들리는 블럭을 멀쩡하게 (깨진 블럭은 그대로)
    public void Heal()
    {
        if (IsBroken || CurrentStage == Stage.Clean) return;
        if (shaking != null) StopCoroutine(shaking);
        if (decaying != null) StopCoroutine(decaying);
        if (growing != null) StopCoroutine(growing);
        shaking = decaying = growing = null;
        CurrentStage = Stage.Clean;
        transform.localPosition = restPosition;
        UpdateLook();
        grid.PlayIceBurst(TopCenter, 10, 0.4f);
        if (punching != null) StopCoroutine(punching);
        punching = StartCoroutine(Punch());
    }

    // 한 단계 진행 (밟힘 또는 시간이 지나 저절로)
    void Advance()
    {
        CurrentStage++;
        lastStageChangeTime = Time.time;
        if (growing != null) StopCoroutine(growing);
        growing = StartCoroutine(GrowCrack());
        grid.PlayIceBurst(TopCenter, 4 + 4 * (int)CurrentStage, 0.6f);
        if (punching != null) StopCoroutine(punching);
        punching = StartCoroutine(Punch());
        if (CurrentStage == Stage.Cracked && decaying == null) decaying = StartCoroutine(Decay());
        if (CurrentStage == Stage.Shaking) shaking = StartCoroutine(ShakeThenBreak());
    }

    // 금이 간 뒤로는 마지막 단계 변화에서 decayInterval이 지날 때마다 저절로 한 단계 (흔들림 전까지)
    IEnumerator Decay()
    {
        while (CurrentStage == Stage.Cracked || CurrentStage == Stage.HeavyCracked)
        {
            if (grid.decayInterval > 0f && Time.time - lastStageChangeTime >= grid.decayInterval) Advance();
            yield return null;
        }
        decaying = null;
    }

    // override가 있으면 그 텍스처(금이 자라는 중간 프레임)를, 없으면 현재 단계 텍스처를 쓴다
    void UpdateLook(Texture2D overrideTexture = null)
    {
        if (blockRenderer == null) return;
        int index = (int)CurrentStage;
        if (grid.stageColors != null && grid.stageColors.Length > 0)
            properties.SetColor(BaseColorId, grid.stageColors[Mathf.Min(index, grid.stageColors.Length - 1)]);

        Texture2D texture = overrideTexture;
        if (texture == null && grid.stageTextures != null && grid.stageTextures.Length > 0)
        {
            int stageIndex = Mathf.Min(index, 3);
            int textureIndex = variant * 4 + stageIndex;
            if (textureIndex >= grid.stageTextures.Length) textureIndex = Mathf.Min(stageIndex, grid.stageTextures.Length - 1);
            texture = grid.stageTextures[textureIndex];
        }
        if (texture != null) properties.SetTexture(BaseMapId, texture);
        blockRenderer.SetPropertyBlock(properties);
    }

    // 단계가 바뀔 때 새 금이 충격점에서 바깥으로 뻗어 나가는 모습
    IEnumerator GrowCrack()
    {
        int frames = grid.crackGrowFramesPerStage;
        int start = (variant * 3 + ((int)CurrentStage - 1)) * frames;
        bool hasFrames = frames > 0 && grid.crackGrowFrames != null && start + frames <= grid.crackGrowFrames.Length;
        if (hasFrames && grid.crackGrowTime > 0f)
        {
            float frameTime = grid.crackGrowTime / (frames + 1);
            for (int i = 0; i < frames; i++)
            {
                UpdateLook(grid.crackGrowFrames[start + i]);
                yield return new WaitForSeconds(frameTime);
            }
        }
        UpdateLook();
        growing = null;
    }

    // 금이 갈 때 순간 움찔 눌렸다가 돌아온다
    IEnumerator Punch()
    {
        const float duration = 0.18f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float squash = Mathf.Sin(t / duration * Mathf.PI) * 0.12f;
            transform.localScale = new Vector3(restScale.x * (1f + squash * 0.3f), restScale.y * (1f - squash), restScale.z * (1f + squash * 0.3f));
            yield return null;
        }
        transform.localScale = restScale;
        punching = null;
    }

    IEnumerator ShakeThenBreak()
    {
        float elapsed = 0f;
        while (elapsed < grid.breakDelay)
        {
            // 깨지기 직전일수록 세게 흔들린다
            float strength = Mathf.Lerp(0.02f, 0.07f, elapsed / grid.breakDelay);
            transform.localPosition = restPosition + new Vector3(
                UnityEngine.Random.Range(-strength, strength), 0f, UnityEngine.Random.Range(-strength, strength));
            elapsed += Time.deltaTime;
            yield return null;
        }
        shaking = null;
        Break();
    }

    void Break()
    {
        if (punching != null) StopCoroutine(punching);
        if (growing != null) StopCoroutine(growing);
        if (decaying != null) StopCoroutine(decaying);
        decaying = null;  // 복구된 뒤 다시 금이 가면 새로 시작
        transform.localPosition = restPosition;  // 흔들리던 중에 깨져도 제자리에서 부서지게
        transform.localScale = restScale;
        CurrentStage = Stage.Broken;

        // 발판이 바로 사라져서 위에 있던 플레이어가 떨어진다
        blockCollider.enabled = false;
        blockRenderer.enabled = false;
        SpawnShards();
        grid.PlayIceBurst(transform.position, 30, 1f);
        Broken?.Invoke(this);
        gameObject.SetActive(false);
    }

    // 얼음 복구 아이템: 깨진 블럭을 멀쩡한 상태로 다시 얼린다
    public void Restore()
    {
        if (!IsBroken) return;

        gameObject.SetActive(true);
        transform.localPosition = restPosition;
        transform.localScale = restScale;
        CurrentStage = Stage.Clean;
        lastStepTime = float.NegativeInfinity;
        blockCollider.enabled = true;
        blockRenderer.enabled = true;
        UpdateLook();

        punching = StartCoroutine(Refreeze());
        grid.PlayIceBurst(TopCenter, 20, 0.5f);
        Restored?.Invoke(this);
    }

    // 확정된 상태만 반영하며 참가자 PC에서 파괴 타이머를 동작시키지 않는다.
    public void ApplyNetworkStage(Stage stage)
    {
        if (!grid.networkReplica || stage == CurrentStage) return;
        StopAllCoroutines();
        shaking = growing = decaying = punching = null;
        bool wasBroken = IsBroken;
        CurrentStage = stage;
        transform.localPosition = restPosition;
        transform.localScale = restScale;
        bool exists = stage != Stage.Broken;
        gameObject.SetActive(exists);
        blockCollider.enabled = blockRenderer.enabled = exists;
        if (exists) UpdateLook();
        else { SpawnShards(); grid.PlayIceBurst(TopCenter, 20, 0.8f); Broken?.Invoke(this); }
        if (wasBroken && exists) { grid.PlayIceBurst(TopCenter, 12, 0.5f); Restored?.Invoke(this); }
    }

    // 작게 솟아올랐다가 살짝 커졌다 제 크기로 (톡 얼어붙는 느낌)
    IEnumerator Refreeze()
    {
        const float duration = 0.35f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            float scale = p < 0.7f ? Mathf.Lerp(0.2f, 1.1f, p / 0.7f) : Mathf.Lerp(1.1f, 1f, (p - 0.7f) / 0.3f);
            transform.localScale = restScale * scale;
            yield return null;
        }
        transform.localScale = restScale;
        punching = null;
    }

    void SpawnShards()
    {
        int n = Mathf.Max(1, grid.shardsPerAxis);
        int ny = Mathf.Max(1, n - 1);
        Vector3 size = new Vector3(restScale.x / n, restScale.y / ny, restScale.z / n);
        Color color = grid.stageColors != null && grid.stageColors.Length > 0 ? grid.stageColors[grid.stageColors.Length - 1] : Color.white;
        var shardProperties = new MaterialPropertyBlock();
        shardProperties.SetColor(BaseColorId, color);

        for (int x = 0; x < n; x++)
        for (int y = 0; y < ny; y++)
        for (int z = 0; z < n; z++)
        {
            Vector3 local = new Vector3((x + 0.5f) / n - 0.5f, (y + 0.5f) / ny - 0.5f, (z + 0.5f) / n - 0.5f);
            Vector3 offset = Vector3.Scale(local, restScale);

            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "IceShard";
            Destroy(shard.GetComponent<Collider>());
            shard.transform.SetParent(grid.transform, false);
            shard.transform.position = transform.position + offset;
            shard.transform.rotation = UnityEngine.Random.rotation;
            shard.transform.localScale = size * UnityEngine.Random.Range(0.7f, 1f);

            Renderer shardRenderer = shard.GetComponent<Renderer>();
            if (grid.shardMaterial != null) shardRenderer.sharedMaterial = grid.shardMaterial;
            shardRenderer.SetPropertyBlock(shardProperties);

            // 가운데서 바깥으로 튀어나가며 살짝 위로 솟았다가 떨어진다
            Vector3 outward = new Vector3(offset.x, 0f, offset.z).normalized;
            Vector3 velocity = outward * UnityEngine.Random.Range(1f, 2.5f) + Vector3.up * UnityEngine.Random.Range(0.5f, 2.5f);
            shard.AddComponent<IceShard>().Launch(velocity, UnityEngine.Random.Range(1.2f, 1.8f));
        }
    }
}
