///
/// Grid가 붙어있는 오브젝트(보통 상위 Grid 오브젝트)에 GridBootstrap 붙여서 한 번만 초기화.
/// 이후 전역에서 GridUtil.WorldToCell() / CellToWorldCenter() 호출.
///

using UnityEngine;

[DefaultExecutionOrder(-30)]
public class GridBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var grid = GetComponent<Grid>();
        GridUtil.Init(grid);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Debug.Log("[OnValidate][GridBootstrap] Grid Initialize");

        var grid = GetComponent<Grid>();
        GridUtil.Init(grid);
    }
#endif
}
