///
/// GridOccupancy는 씬에 1개만 존재 (Singleton 또는 Scene Manager에 붙여도 됨).
/// 모든 “퍼즐 오브젝트”는 이 매니저를 통해서만 위치 변경하도록 강제.
/// 
/// 모든 퍼즐 오브젝트는 무조건 GridObject를 갖는다.
/// 스폰 / OnEnable 시에 TryRegister로 들어가며, 이미 있으면 실패 → 에디터에서 배치 충돌도 바로 알 수 있음.
/// 이동은 항상 GridOccupancy.TryMove를 통해서만.
/// 들기/삭제/죽음 시에는 Unregister 호출.
/// Tilemap Collider는 충돌 외곽용, 점유 판정과는 완전히 분리.
/// 
/// - 퍼즐 로직용 "셀 차단/점유" 레이어
/// - 플레이어 이동 충돌은 물리/레이어로 처리, 여기서는 오직 퍼즐 판정만 담당
///

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 다른 MonoBehaviour보다 먼저 실행
[DefaultExecutionOrder(-40)]
public class GridOccupancy : MonoBehaviour
{
    public static GridOccupancy Instance { get; private set; }

    // 1타일 = 1오브젝트
    private Dictionary<Vector3Int, GridObject> _occupied = new();   // 셀 → 객체(단일 점유 가정)
    private readonly Dictionary<GridObject, Vector3Int> _reverse = new();      // 객체 → 셀

    // 외곽벽/내부벽 등 "들어가면 안 되는 셀" 저장용
    private HashSet<Vector3Int> _blockedCells = new HashSet<Vector3Int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 씬 이동할 때 유지할 거면 주석 해제
        // DontDestroyOnLoad(gameObject);
    }

    // 외곽 벽 타일맵에서 막힌 셀 정보 읽어오기
    public void InitWallsFromTilemap(Tilemap wallTilemap)
    {
        _blockedCells.Clear();

        var bounds = wallTilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = wallTilemap.GetTile(cell);
                if (tile != null)
                {
                    _blockedCells.Add(cell);
                }
            }
        }
    }

    public void AddWallsFromTilemap(Tilemap wallTilemap)
    {
        var bounds = wallTilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = wallTilemap.GetTile(cell);
                if (tile != null)
                {
                    _blockedCells.Add(cell);
                }
            }
        }
    }

    /// <summary>셀 차단 여부(벽 또는 점유)</summary>
    public bool IsBlockedCell(Vector3Int cell)
    {
        // 외곽/내부 벽 셀
        if (_blockedCells.Contains(cell)) return true;
        // 이미 오브젝트가 있는 셀
        if (_occupied.TryGetValue(cell, out var obj) && obj != null) return true;

        return false;
    }

    public bool IsOccupied(Vector3Int cell)
    {
        return _occupied.ContainsKey(cell);
    }

    /// <summary>셀의 점유 객체(없으면 null)</summary>
    public GridObject GetOccupant(Vector3Int cell)
    {
        _occupied.TryGetValue(cell, out var obj);
        return obj;
    }

    /// <summary>
    /// 객체를 셀에 등록(벽/점유 충돌 시 false).
    /// 정상 등록 시 객체→셀, 셀→객체가 동기화됨.
    /// </summary>
    public bool TryRegister(GridObject obj, Vector3Int cell)
    {
        if (obj == null) return false;
        if (IsOccupied(cell)) return false;

        if (!obj.BlocksMovement) return false;

        // 기존에 등록되어 있던 경우 위치 갱신
        if (_reverse.TryGetValue(obj, out var prev))
        {
            if (prev == cell)
            {
                _occupied[cell] = obj; // 보정
                return true;
            }

            // 원자적 갱신
            _occupied.Remove(prev);
            _occupied[cell] = obj;
            _reverse[obj] = cell;
            return true;
        }

        // Cell을 이미 점유 중인 다른 오브젝트가 있음
        if (_occupied.TryGetValue(cell, out var exists) && exists != null)
        {
            return false;
        }

        // 신규 등록
        _occupied[cell] = obj;
        _reverse[obj] = cell;
        obj.CurrentCell = cell;
        obj.transform.position = GridUtil.CellToWorldCenter(cell);
        return true;
    }

    /// <summary>
    /// 객체 등록 해제(양방향 테이블에서 제거)
    /// </summary>
    public bool Unregister(GridObject obj)
    {
        if (obj == null) return false;

        if (_reverse.TryGetValue(obj, out var cell))
        {
            _reverse.Remove(obj);

            if (_occupied.TryGetValue(cell, out var cur) && cur == obj)
            {
                _occupied.Remove(cell);
            }

            return true;
        }
        return false;
    }

    /// <summary>
    /// 셀 기준 해제(그 셀의 점유 객체 제거)
    /// </summary>
    public bool Unregister(Vector3Int cell)
    {
        if (_occupied.TryGetValue(cell, out var obj) && obj != null)
        {
            _occupied.Remove(cell);
            _reverse.Remove(obj);

            return true;
        }
        return false;
    }

    /// <summary>
    /// 이미 등록된 객체를 targetCell로 이동(벽/충돌 시 false)
    /// </summary>
    public bool TryMove(GridObject obj, Vector3Int targetCell)
    {
        if (obj == null) return false;

        // 이미 다른 객체가 있으면 이동 불가
        if (IsBlockedCell(targetCell)) return false;
        // 등록되지 않은 오브젝트
        if (!_reverse.TryGetValue(obj, out var from)) return false;

        // 기존과 같은 위치로 이동 시도
        if (from == targetCell) return true;

        // 기존 위치 해제
        _occupied.Remove(from);

        // 새 위치 등록
        _occupied[targetCell] = obj;
        _reverse[obj] = targetCell;

        obj.CurrentCell = targetCell;
        obj.transform.position = GridUtil.CellToWorldCenter(targetCell);
        return true;
    }

    /// <summary>객체→셀 조회(등록되어 있으면 true)</summary>
    public bool TryGetCellOf(GridObject obj, out Vector3Int cell)
    {
        if (obj != null && _reverse.TryGetValue(obj, out cell))
            return true;

        cell = default;
        return false;
    }
}
