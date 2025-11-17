///
/// 집기 / 내리기만 처리
/// 집으면 Occupancy에서 제거 + player 자식으로 붙이기
/// 내릴 때는 셀 체크 후 다시 등록
///

using UnityEngine;

[RequireComponent(typeof(GridObject))]
public class CarryableMirror : MonoBehaviour
{
    private GridObject _gridObj;
    private bool _isCarried;
    public bool IsCarried => _isCarried;
    private Transform _carrier; // 보통 Player transform

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
    }

    public void PickUp(Transform carrier)
    {
        if (_isCarried) return;

        // 그리드 점유 해제
        GridOccupancy.Instance.Unregister(_gridObj.CurrentCell);

        _carrier = carrier;
        transform.SetParent(_carrier);
        // 위치는 carrier 기준으로 적당히 offset
        transform.localPosition = Vector3.up * 0.5f;

        _isCarried = true;
        Debug.Log($"CarryableMirror {name} picked up.");
    }

    public bool TryDrop(Vector3Int targetCell)
    {
        if (!_isCarried) return false;

        if (GridOccupancy.Instance.IsOccupied(targetCell))
        {
            Debug.Log("Cannot drop mirror: target cell is occupied.");
            return false;
        }

        transform.SetParent(null);
        GridOccupancy.Instance.TryRegister(_gridObj, targetCell);

        _isCarried = false;
        _carrier = null;

        Debug.Log($"CarryableMirror {name} dropped at {targetCell}.");
        return true;
    }
}
