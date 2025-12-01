///
/// GridUtil: Grid 래퍼
/// 
/// 씬 시작 시 GridUtil.Init(grid); 한 번 호출.
/// 이후 모든 오브젝트는 World 좌표가 아니라 Cell(Vector3Int) 기준으로 조작.
///

using UnityEngine;

public static class GridUtil
{
    private static Grid _grid;

    public static void Init(Grid grid)
    {
        _grid = grid;
    }

    public static bool IsReady => _grid != null;

    public static Vector3Int WorldToCell(Vector3 worldPos)
    {
        return _grid.WorldToCell(worldPos);
    }

    public static Vector3 CellToWorldCenter(Vector3Int cellPos)
    {
        // 타일 중앙 스냅
        return _grid.GetCellCenterWorld(cellPos);
    }

    // 스냅 함수 (핵심)
    public static void SnapTransformToGrid(Transform t)
    {
        if (_grid == null)
        {
            Debug.LogWarning("Grid instance is null");
            return;
        }

        var cell = _grid.WorldToCell(t.position);
        var world = _grid.GetCellCenterWorld(cell);
        t.position = world;
    }
}
