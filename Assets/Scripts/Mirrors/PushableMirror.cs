///
/// Push 요청이 들어오면, 앞 칸이 비었는지만 보고 GridOccupancy를 통해 이동
/// 
/// 미러는 그리드 스냅 오브젝트 (GridObject).
/// 플레이어는 Rigidbody2D로 자유롭게 이동.
/// 플레이어가 미러에 부딪혀서 “밀어붙이는 방향”이 생기면
/// → 그 방향의 셀로 미러를 1타일 밀어주는 그리드 이동 + 살짝 Lerp 애니메이션.
/// 
/// 
/// 미러는 BoxCollider2D + Rigidbody2D(kinematic) + PushableMirror + GridObject.
/// 플레이어는 Rigidbody2D(dynamic) + Collider2D.
/// 플레이어가 미러를 향해 움직여서 부딪히면, 미러가 그리드 한 칸 Lerp로 밀린다.
/// 
/// 
/// 연속 이동 플레이어 기준으로,
/// OnCollisionStay2D에서 “플레이어가 미러를 정면에서 밀고 있는가?” 판정.
/// Grid 셀 단위로 이동 + Lerp 애니메이션.
/// Push는 GridOccupancy와 타일 좌표를 통해 퍼즐 규칙과 일관성 유지.
///

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GridObject))]
[RequireComponent(typeof(Rigidbody2D))]
public class PushableMirror : MonoBehaviour
{
    public float pushDuration = 0.1f;      // 한 타일 밀리는 시간
    public float minDotToPush = 0.6f;      // 플레이어가 얼마나 “정면에서” 밀어야 인정할지

    private GridObject _gridObj;
    private Rigidbody2D _rb;
    private bool _isMoving;

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;            // 직접 위치 제어
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_isMoving) return;
        if (!collision.collider.CompareTag("Player")) return;

        var playerMove = collision.collider.GetComponent<PlayerFreeMove>();
        if (playerMove == null) return;

        // 플레이어가 어떤 방향으로 움직이고 있는지
        Vector2 playerDir = playerMove.FacingDir.normalized;
        if (playerDir.sqrMagnitude < 0.01f) return;

        // 플레이어 → 미러 방향 벡터
        Vector2 toMirror = (Vector2)transform.position - (Vector2)collision.transform.position;
        toMirror.Normalize();

        // 플레이어가 미러 쪽으로 밀고 있는지 확인 (내적)
        float dot = Vector2.Dot(playerDir, toMirror);
        if (dot < minDotToPush) return; // 정면에서 밀어붙이는 상황이 아니면 무시

        // 그리드 기준으로 어떤 방향으로 밀릴지 결정
        Vector2Int pushDir = GetCardinalDirection(playerDir);
        if (pushDir == Vector2Int.zero) return;

        TryStartPush(pushDir);
    }

    private Vector2Int GetCardinalDirection(Vector2 dir)
    {
        // X/Y 중 더 큰 축으로 정규화해서 4방향으로 스냅
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x > 0 ? Vector2Int.right : Vector2Int.left;
        else if (Mathf.Abs(dir.y) > 0.01f)
            return dir.y > 0 ? Vector2Int.up : Vector2Int.down;
        return Vector2Int.zero;
    }

    private void TryStartPush(Vector2Int dir)
    {
        if (_isMoving) return;

        var occ = GridOccupancy.Instance;
        Vector3Int currentCell = _gridObj.CurrentCell;
        Vector3Int targetCell = currentCell + new Vector3Int(dir.x, dir.y, 0);

        // 타겟 셀이 막혀 있으면 밀지 않음
        if (occ.IsBlockedCell(targetCell))
            return;

        StartCoroutine(PushRoutine(currentCell, targetCell));
    }

    private IEnumerator PushRoutine(Vector3Int fromCell, Vector3Int toCell)
    {
        _isMoving = true;

        Vector3 startPos = GridUtil.CellToWorldCenter(fromCell);
        Vector3 endPos = GridUtil.CellToWorldCenter(toCell);
        float t = 0f;

        // 밀리는 동안 그리드 점유는 "도착 시점"에 업데이트
        // (필요하면 시작할 때 임시 점유 예약 로직을 추가 가능)
        while (t < pushDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / pushDuration);
            Vector3 pos = Vector3.Lerp(startPos, endPos, normalized);
            _rb.MovePosition(pos);
            yield return null;
        }

        // 마지막에 GridOccupancy 상의 셀 정보 업데이트 + 위치 스냅
        GridOccupancy.Instance.TryMove(_gridObj, toCell);

        OnPushFinished();

        _isMoving = false;
    }

    /// <summary>
    /// dir: 플레이어가 미는 방향 (예: (1,0), (0,1) 등)
    /// </summary>
    public bool TryPush(Vector2Int dir)
    {
        var current = _gridObj.CurrentCell;
        var target = current + new Vector3Int(dir.x, dir.y, 0);

        if (GridOccupancy.Instance.IsOccupied(target))
        {
            Debug.Log($"Cannot push mirror {name}: target cell occupied.");
            return false;
        }

        var moved = GridOccupancy.Instance.TryMove(_gridObj, target);
        if (moved)
        {
            Debug.Log($"Pushed mirror {name} to {target}.");
        }

        return moved;
    }

    // 예: MirrorMover에서 한 칸 밀기 끝난 시점
    private void OnPushFinished()
    {
        // 위치 스냅 등 처리 후
        LaserWorldEvents.RaiseWorldChanged();
    }

}
