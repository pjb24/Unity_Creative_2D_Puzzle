///
/// GridObject: 모든 퍼즐 오브젝트 공통 베이스
/// 
/// 에디터에서 오브젝트를 아무 위치에 던져놔도, OnEnable에서 자동으로 가장 가까운 타일에 스냅 + 등록.
/// 이걸 플레이어, 거울, 문, 벽, 장치 Prefab 전부에 붙임.
///

using UnityEngine;

public class GridObject : MonoBehaviour
{
    [SerializeField] private bool _snapEveryFrameInEditor = true;

    public Vector3Int CurrentCell { get; set; }
    public bool BlocksMovement = true; // 필요하면 비충돌 오브젝트도 만들기

    private void OnEnable()
    {
        // 씬에 처음 배치된 위치 기준으로 등록
        var cell = GridUtil.WorldToCell(transform.position);
        GridOccupancy.Instance.TryRegister(this, cell);
    }

    private void OnDisable()
    {
        GridOccupancy.Instance.Unregister(CurrentCell);
    }

#if UNITY_EDITOR
    // 씬에서 손으로 움직일 때 자동으로 스냅하고 싶으면
    private void OnValidate()
    {
        if (!Application.isPlaying && _snapEveryFrameInEditor)
        {
            GridUtil.SnapTransformToGrid(transform);
            Debug.Log("[OnValidate] GridSnap Active, Object Name: " + gameObject.name);
        }
    }
#endif
}
