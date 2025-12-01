///
/// 미러는 BoxCollider2D + Rigidbody2D(kinematic) + PushableMirror + GridObject.
/// 플레이어는 Rigidbody2D(dynamic) + Collider2D.
/// 
/// 
/// Pushable 거울
/// - Interact 시 Player에 "부착" (pushing 상태).
/// - Player가 바라보던 방향의 축(axis)으로만 같이 이동.
///

using UnityEngine;

[RequireComponent(typeof(GridObject))]
[RequireComponent(typeof(Rigidbody2D))]
public class PushableMirror : MonoBehaviour
{
    private GridObject _gridObj;
    private Rigidbody2D _rb;

    private bool _isAttached;
    public bool IsAttached => _isAttached;

    private Transform _pusher;
    private Vector2Int _axis;   // (1,0) 또는 (0,1) 중 하나 (X축 or Y축)

    private Vector3 _localOffset;   // 플레이어 기준 위치(앞 칸)

    private Collider2D _collider;

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;            // 직접 위치 제어

        _collider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// 푸시 시작. 바라보는 방향 dir 기준으로 axis 결정.
    /// dir 은 (1,0),(-1,0),(0,1),(0,-1) 중 하나라고 가정.
    /// 
    /// 거울을 pusher의 자식으로 붙여서 화면상으로 같이 움직이게 함
    /// GridOccupancy에서는 일단 제거 (이동 중 상태)
    /// </summary>
    public bool BeginPush(Transform pusher, Vector2Int facingDir)
    {
        if (_isAttached) return false;
        if (facingDir == Vector2Int.zero) return false;

        // 축만 가져간다. (X or Y)
        _axis = Mathf.Abs(facingDir.x) > 0
            ? new Vector2Int(1, 0)
            : new Vector2Int(0, 1);

        // 기존 셀 점유 해제 (이동 중 상태로 취급)
        GridOccupancy.Instance.Unregister(_gridObj.CurrentCell);

        _collider.enabled = false;

        _pusher = pusher;
        _isAttached = true;

        // 플레이어 앞 한 칸 정도로 붙여두는 오프셋
        _localOffset = new Vector3(facingDir.x * 1.1f, facingDir.y * 1.1f, 0f);
        transform.SetParent(_pusher);
        transform.localPosition = _localOffset;

        LaserWorldEvents.RaiseWorldChanged();

        Debug.Log($"PushableMirror {name} BeginPush. Axis={_axis}");

        return true;
    }

    /// <summary>
    /// 푸시 종료.
    /// - 부모 해제
    /// - 최종 월드 위치 기준 셀을 계산해서 다시 GridOccupancy에 등록
    /// </summary>
    public void EndPush()
    {
        if (!_isAttached) return;

        _isAttached = false;

        // 부모 해제, 현재 월드 좌표 유지
        transform.SetParent(null);

        // 최종 위치를 그리드에 스냅해서 셀 등록
        var worldPos = transform.position;
        var targetCell = GridUtil.WorldToCell(worldPos);

        if (GridOccupancy.Instance.IsOccupied(targetCell))
        {
            // 이 상황이 거슬리면, 한 칸 뒤로 밀거나, Push를 막는 방향으로 정책 수정
            Debug.LogWarning($"PushableMirror {name} EndPush: targetCell {targetCell} 이미 점유 중.");
        }
        else
        {
            GridOccupancy.Instance.TryRegister(_gridObj, targetCell);
        }

        _collider.enabled = true;

        _pusher = null;

        LaserWorldEvents.RaiseWorldChanged();

        Debug.Log($"PushableMirror {name} EndPush. Registered at {targetCell}");
    }

    /// <summary>
    /// 플레이어가 deltaCell 만큼 이동하려 할 때
    /// - 이 방향이 현재 axis와 맞는지
    /// - 거울이 그 방향으로 이동해도 되는지(앞쪽 셀에 벽/장애물 있는지) 만 판단
    /// * 여기서는 GridOccupancy 등록/해제를 하지 않는다 *
    /// </summary>
    public bool CanMoveWithPusher(Vector3Int deltaCell)
    {
        if (!_isAttached) return true;  // 푸시 중이 아니면 거울은 관여 X

        // 축과 안 맞는 방향이면 못 움직임
        if (_axis.x != 0 && deltaCell.y != 0) return false;
        if (_axis.y != 0 && deltaCell.x != 0) return false;

        if (deltaCell == Vector3Int.zero) return true;

        // 현재 거울이 있는 셀 = 월드 포지션 기준
        var currentCell = GridUtil.WorldToCell(transform.position);
        var targetCell = currentCell + deltaCell;

        // 앞 셀에 벽/장치 등으로 "막혀 있는지"만 체크
        if (GridOccupancy.Instance.IsOccupied(targetCell))
        {
            Debug.Log($"PushableMirror {name}: targetCell {targetCell} 막힘, 이동 불가.");
            return false;
        }

        // 이동은 Player + 자식 구조가 담당하므로 여기서는 OK만 반환
        return true;
    }

    /// <summary>
    /// 현재 축 반환. (1,0) or (0,1)
    /// </summary>
    public Vector2Int GetAxis()
    {
        return _axis;
    }
}
