using UnityEngine;

// 플레이어가 밟은 얼음 블럭을 한 단계씩 깬다.
//  - 블럭에 새로 올라선 순간 1회 (점프 후 같은 블럭에 다시 착지해도 1회)
//  - 같은 블럭에 오래 서 있으면 standTimeToCrack마다 1회
//  - 시작 블럭은 플레이어가 처음 움직이기 전까지 깨지지 않음
//  - 넘어져 있는 동안(다이브 착지, 기절)에는 오래 서 있기 타이머를 멈춤
[RequireComponent(typeof(PlayerController))]
public class IceStepTracker : MonoBehaviour
{
    public IceGrid grid;

    // 걷다가 이음새에서 한두 프레임 발이 뜨는 것은 블럭을 떠난 것으로 보지 않는다
    const float LeaveGraceTime = 0.12f;

    PlayerController player;
    IceBlock currentBlock;
    float standTimer;
    float airTime;

    public IceBlock CurrentBlock => currentBlock;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        if (grid == null) grid = player.grid;
    }

    void FixedUpdate()
    {
        if (player.networkReplica || (OnlineGameSession.InRoom && !OnlineGameSession.MatchPlaying)) return;
        if (grid == null) grid = player.grid;  // 봇처럼 코드로 만들 때 grid가 나중에 들어오는 경우
        IceBlock block = FindBlockUnderPlayer();
        if (block == null)
        {
            airTime += Time.fixedDeltaTime;
            if (airTime >= LeaveGraceTime) currentBlock = null;
            return;
        }
        airTime = 0f;

        if (block != currentBlock)
        {
            currentBlock = block;
            standTimer = 0f;
            if (player.HasMoved) block.Step();
            return;
        }

        if (!player.HasMoved || grid == null) return;
        if (player.State == PlayerController.MoveState.KnockedDown) return;

        standTimer += Time.fixedDeltaTime;
        if (standTimer >= grid.standTimeToCrack)
        {
            standTimer = 0f;
            block.Step();
        }
    }

    IceBlock FindBlockUnderPlayer()
    {
        if (!player.IsGrounded) return null;

        // 블럭 이음새 위에서는 발 판정이 두 블럭을 오갈 수 있어서, 몸 중심 바로 아래 블럭을 우선한다
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            IceBlock below = hit.collider.GetComponent<IceBlock>();
            if (below != null) return below;
        }
        return player.GroundCollider != null ? player.GroundCollider.GetComponent<IceBlock>() : null;
    }
}
