///
/// GridOccupancy는 씬에 1개만 존재 (Singleton 또는 Scene Manager에 붙여도 됨).
/// 모든 “퍼즐 오브젝트”는 이 매니저를 통해서만 위치 변경하도록 강제.
/// 
/// 모든 퍼즐 오브젝트는 무조건 GridObject를 갖는다.
/// 스폰 / OnEnable 시에 TryRegister로 들어가며, 이미 있으면 실패 → 에디터에서 배치 충돌도 바로 알 수 있음.
/// 이동은 항상 GridOccupancy.TryMove를 통해서만.
/// 들기/삭제/죽음 시에는 Unregister 호출.
/// Tilemap Collider는 충돌 외곽용, 점유 판정과는 완전히 분리.
/// 이렇게 하면:
/// “같은 칸에 거울+플레이어 같이 있는 상태” 같은 버그가 구조적으로 안 나옴.
/// 레이저 계산 할 때도 그리드 기준으로 “이 칸에 뭐 있음?”을 바로 가져와서 처리 가능.
/// 
/// 이동 시에 Tilemap Collider를 전혀 믿지 말고, 무조건 GridOccupancy의 셀 정보를 기준으로 허용/차단을 결정하라는 것.
///

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 다른 MonoBehaviour보다 먼저 실행
[DefaultExecutionOrder(-20)]
public class GridOccupancy : MonoBehaviour
{
    public static GridOccupancy Instance { get; private set; }

    // 1타일 = 1오브젝트
    private Dictionary<Vector3Int, GridObject> _occupied = new Dictionary<Vector3Int, GridObject>();

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

    public bool IsBlockedCell(Vector3Int cell)
    {
        // 외곽/내부 벽 셀 OR 이미 오브젝트가 있는 셀
        return _blockedCells.Contains(cell) || _occupied.ContainsKey(cell);
    }

    public bool IsOccupied(Vector3Int cell)
    {
        return _occupied.ContainsKey(cell);
    }

    public GridObject GetOccupant(Vector3Int cell)
    {
        _occupied.TryGetValue(cell, out var obj);
        return obj;
    }

    public bool TryRegister(GridObject obj, Vector3Int cell)
    {
        if (IsOccupied(cell)) return false;

        _occupied[cell] = obj;
        obj.CurrentCell = cell;
        obj.transform.position = GridUtil.CellToWorldCenter(cell);
        return true;
    }

    public void Unregister(Vector3Int cell)
    {
        _occupied.Remove(cell);
    }

    public bool TryMove(GridObject obj, Vector3Int targetCell)
    {
        // 이미 다른 객체가 있으면 이동 불가
        if (IsBlockedCell(targetCell))
            return false;

        // 기존 위치 해제
        _occupied.Remove(obj.CurrentCell);

        // 새 위치 등록
        _occupied[targetCell] = obj;
        obj.CurrentCell = targetCell;
        obj.transform.position = GridUtil.CellToWorldCenter(targetCell);
        return true;
    }
}
