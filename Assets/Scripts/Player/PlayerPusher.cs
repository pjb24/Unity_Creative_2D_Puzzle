///
/// 플레이어의 Push 상호작용 + 축 Lock 관리
/// - Interact 키로 PushableMirror 붙이기 / 떼기.
/// - 붙은 동안에는 axis 방향으로만 이동 가능.
/// - 이동 전에 거울이 같이 이동 가능한지 체크.
///

using UnityEngine;

public class PlayerPusher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _interactRange = 0.8f;

    private PlayerFreeMove _move;

    private PushableMirror _attachedMirror;
    private Vector2Int _facingDir = Vector2Int.right; // 이동/조작 코드에서 계속 갱신해줘야 함.

    /// <summary>
    /// 현재 push 상태인지 여부
    /// </summary>
    public bool IsPushing => _attachedMirror != null;

    private void Awake()
    {
        _move = GetComponent<PlayerFreeMove>();
    }

    // 플레이어 앞 셀 찾기
    private Vector3Int GetFrontCell()
    {
        var worldPos = (Vector2)transform.position + _move.FacingDir.normalized * _interactRange;
        return GridUtil.WorldToCell(worldPos);
    }

    /// <summary>
    /// 외부에서 플레이어가 바라보는 방향을 갱신해줘야 한다.
    /// ex) 이동 입력이 (1,0) 들어오면 SetFacingDir(Vector2Int.right)
    /// </summary>
    public void SetFacingDir(Vector2Int dir)
    {
        if (dir != Vector2Int.zero)
            _facingDir = dir;
    }

    /// <summary>
    /// Interact 입력 처리 (예: E키)
    /// - 이미 거울을 밀고 있으면 해제.
    /// - 아니면 앞 타일에서 PushableMirror 찾고 BeginPush 시도.
    /// </summary>
    public void HandleInteract()
    {
        // 이미 밀고 있는 상태라면 해제
        if (_attachedMirror != null)
        {
            _attachedMirror.EndPush();
            _attachedMirror = null;
            Debug.Log("Player stopped pushing mirror.");
            return;
        }

        var cell = GetFrontCell();
        var occupancy = GridOccupancy.Instance;
        var obj = occupancy.GetOccupant(cell);
        if (obj == null) return;

        var mirror = obj.GetComponent<PushableMirror>();
        if (mirror == null) return;

        if (mirror.BeginPush(transform, _facingDir))
        {
            _attachedMirror = mirror;
            Debug.Log($"Player started pushing {mirror.name}.");
        }
    }

    /// <summary>
    /// 이동 입력을 축 Lock 규칙에 맞게 필터링하고,
    /// 거울이 같이 움직일 수 있는지 체크한다.
    /// - rawDir: (1,0),(-1,0),(0,1),(0,-1) 등 셀 단위 이동 방향
    /// - 반환값: 실제로 적용할 이동 방향. (막히면 Vector2Int.zero)
    /// </summary>
    public Vector2 FilterMoveAndTryPush(Vector2 rawDir)
    {
        if (rawDir == Vector2.zero)
            return Vector2.zero;

        // Push 상태가 아니라면 그대로 통과
        if (_attachedMirror == null)
            return rawDir;

        // Push 중이면 "시작 시점 축"으로만 이동 허용
        // => Mirror.BeginPush 에서 설정된 axis 사용
        var axis = _attachedMirror.GetAxis();

        Vector2 filtered = rawDir;

        if (axis.x != 0)
        {
            // X축만 허용
            filtered.y = 0;
        }
        else if (axis.y != 0)
        {
            // Y축만 허용
            filtered.x = 0;
        }

        if (filtered == Vector2.zero)
        {
            // 입력이 축과 안 맞으면 이동 불가
            return Vector2.zero;
        }

        // 이제 filtered는 (1,0) 또는 (-1,0) 또는 (0,1) or (0,-1) 중 하나
        // 거울이 같이 움직일 수 있는지 체크
        Vector3Int deltaCell = new Vector3Int((int)filtered.x, (int)filtered.y, 0);

        bool mirrorCanMove = _attachedMirror.CanMoveWithPusher(deltaCell);
        if (!mirrorCanMove)
        {
            // 거울이 막히면 플레이어도 이동 불가
            return Vector2.zero;
        }

        // 거울 이동 OK → 플레이어도 이 방향으로 이동
        return filtered;
    }

    /// <summary>
    /// 외부에서 강제로 푸시 해제하고 싶을 때(예: 충돌, 컷신 등)
    /// </summary>
    public void ForceStopPush()
    {
        if (_attachedMirror != null)
        {
            _attachedMirror.EndPush();
            _attachedMirror = null;
        }
    }
}
