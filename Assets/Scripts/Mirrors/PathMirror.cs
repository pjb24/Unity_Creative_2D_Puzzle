///
/// 경로를 따라 한 칸씩 이동하는 거울
/// - GridObject + GridOccupancy 기반
/// - _pathCells 에 정의된 셀들만 이동 가능
/// - Occupancy 체크 후 이동
///

using UnityEngine;
using System.Collections;

[RequireComponent(typeof(GridObject))]
[RequireComponent(typeof(Rigidbody2D))]
public class PathMirror : MonoBehaviour
{
    [Header("Path Settings")]
    [Tooltip("이 거울이 따라갈 그리드 셀 경로 (월드 Grid 기준 셀 좌표)")]
    [SerializeField] private Vector3Int[] _pathCells;

    [Tooltip("시작 인덱스 (path 배열 기준)")]
    [SerializeField] private int _startIndex = 0;

    [Tooltip("경로 끝에서 더 이상 이동 못하게 할지 여부 (false면 끝에서 멈춤)")]
    [SerializeField] private bool _loop = true;

    public float pushDuration = 0.1f;      // 한 타일 밀리는 시간

    private GridObject _gridObj;
    private Rigidbody2D _rb;
    private int _currentIndex;
    private bool _isMoving;

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
        _rb = GetComponent<Rigidbody2D>();

        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Start()
    {
        if (_pathCells == null || _pathCells.Length == 0)
        {
            Debug.LogWarning($"PathMirror {name}: pathCells 비어있음.");
            return;
        }

        // 인덱스 보정
        _currentIndex = Mathf.Clamp(_startIndex, 0, _pathCells.Length - 1);

        // 초기 위치를 경로 상 위치로 보정
        var currentCell = _pathCells[_currentIndex];
        if (_gridObj.CurrentCell != currentCell)
        {
            Debug.LogWarning(
                    $"PathMirror {name}: GridObject.CurrentCell({_gridObj.CurrentCell})과 " +
                    $"pathCells[{_currentIndex}]({currentCell}) 불일치. 경로 기준 셀로 이동시킴."
                );
            GridOccupancy.Instance.Unregister(_gridObj.CurrentCell);
            GridOccupancy.Instance.TryRegister(_gridObj, currentCell);
        }

        LaserWorldEvents.RaiseWorldChanged();
    }

    /// <summary>
    /// step = +1 이면 다음 셀, -1 이면 이전 셀.
    /// 이동에 성공하면 true 반환.
    /// </summary>
    public bool TryMove(int step)
    {
        if (_pathCells == null || _pathCells.Length == 0)
            return false;

        if (step == 0)
            return false;

        if (_isMoving) return false;

        int targetIndex = _currentIndex + step;

        // 루프 처리
        if (_loop)
        {
            // 음수 보정
            if (targetIndex < 0)
                targetIndex = _pathCells.Length - 1;
            else if (targetIndex >= _pathCells.Length)
                targetIndex = 0;
        }
        else
        {
            // 루프 안할 경우 범위 밖이면 이동 불가
            if (targetIndex < 0 || targetIndex >= _pathCells.Length)
            {
                Debug.Log($"PathMirror {name}: 경로 끝, 더 이상 이동 불가 (index={targetIndex}).");
                return false;
            }
        }

        var targetCell = _pathCells[targetIndex];

        // 자기 자신이 이미 점유하고 있는 셀로 이동 요청이면 무시
        if (_gridObj.CurrentCell == targetCell)
            return false;

        // 점유 체크
        if (GridOccupancy.Instance.IsOccupied(targetCell))
        {
            Debug.Log($"PathMirror {name}: targetCell {targetCell} 점유 중, 이동 불가.");
            return false;
        }

        // 기존 셀 해제 + 새 셀 등록
        var pushDir = targetCell - _gridObj.CurrentCell;
        TryStartPush((Vector2Int)pushDir);

        _currentIndex = targetIndex;

        LaserWorldEvents.RaiseWorldChanged();

        Debug.Log($"PathMirror {name}: index {_currentIndex} / cell {targetCell} 로 이동.");

        return true;
    }

    /// <summary>
    /// 다음 셀로 이동 시도 (경로에서 +1)
    /// </summary>
    public bool TryMoveNext()
    {
        return TryMove(+1);
    }

    /// <summary>
    /// 이전 셀로 이동 시도 (경로에서 -1)
    /// </summary>
    public bool TryMovePrev()
    {
        return TryMove(-1);
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

    // 예: MirrorMover에서 한 칸 밀기 끝난 시점
    private void OnPushFinished()
    {
        // 위치 스냅 등 처리 후
        LaserWorldEvents.RaiseWorldChanged();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_pathCells == null || _pathCells.Length == 0)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _pathCells.Length; i++)
        {
            var cell = _pathCells[i];
            var world = GridOccupancy.Instance != null
                ? GridUtil.CellToWorldCenter(cell)
                : (Vector3)cell; // 임시

            Gizmos.DrawSphere(world, 0.08f);

            if (i < _pathCells.Length - 1)
            {
                var nextCell = _pathCells[i + 1];
                var nextWorld = GridOccupancy.Instance != null
                    ? GridUtil.CellToWorldCenter(nextCell)
                    : (Vector3)nextCell;

                Gizmos.DrawLine(world, nextWorld);
            }
        }
    }
#endif
}
