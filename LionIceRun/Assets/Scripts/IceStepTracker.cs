using UnityEngine;

// 플레이어가 밟은 얼음 블럭을 한 단계씩 깬다.
//  - 블럭에 새로 올라선 순간 1회 (점프 후 같은 블럭에 다시 착지해도 1회)
//  - 같은 블럭에 오래 서 있으면 standTimeToCrack마다 1회
//  - 경기 시작 뒤에는 이동 입력 없이도 금이 간다. 준비/카운트다운 중에는 보호한다.
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
    bool landingPending;

    public IceBlock CurrentBlock => currentBlock;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        if (grid == null) grid = player.grid;
    }

    // 접지를 갱신한 직후, 예약 점프가 접지 상태를 지우기 전에 컨트롤러가 호출한다.
    public void TrackGroundContact()
    {
        if (!isActiveAndEnabled) return;
        if (player.networkReplica || player.localPrediction || (OnlineGameSession.InRoom && !OnlineGameSession.MatchPlaying)) return;
        var manager = GameManager.Instance;
        if (manager == null || manager.State != GameManager.GameState.Playing)
        { currentBlock = null; standTimer = airTime = 0; landingPending = false; return; }
        if (grid == null) grid = player.grid;  // 봇처럼 코드로 만들 때 grid가 나중에 들어오는 경우
        IceBlock block = FindBlockUnderPlayer();
        if (block == null)
        {
            airTime += Time.fixedDeltaTime;
            if (airTime >= LeaveGraceTime) { currentBlock = null; landingPending = true; }
            return;
        }
        airTime = 0f;

        if (block != currentBlock)
        {
            currentBlock = block;
            standTimer = 0f;
            block.Step(landingPending);
            landingPending = false;
            return;
        }

        if (grid == null) return;
        if (player.State == PlayerController.MoveState.KnockedDown) return;

        standTimer += Time.fixedDeltaTime;
        if (standTimer >= grid.standTimeToCrack)
        {
            standTimer = 0f;
            block.Step();
        }
    }

    // 실제 점프는 이음새 유예와 달리 같은 칸에 재착지해도 새 발걸음이다.
    public void LeaveForJump()
    {
        currentBlock = null;
        standTimer = airTime = 0f;
        landingPending = true;
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
