///
/// 상호작용을 “좌표 변환”으로 연결
/// 플레이어는 자유 이동하지만,
/// 상호작용/퍼즐 판정은 전부 ‘그리드 셀’ 기준으로 처리한다.
///

using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    public float interactDistance = 0.6f;
    private PlayerFreeMove _move;

    private CarryableMirror _carriedMirror;
    public int normalKeyCount;
    public bool hasSpecialKey;

    private void Awake()
    {
        _move = GetComponent<PlayerFreeMove>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            Interact();
    }

    // 플레이어 앞 셀 찾기
    private Vector3Int GetFrontCell()
    {
        var worldPos = (Vector2)transform.position + _move.FacingDir.normalized * interactDistance;
        return GridUtil.WorldToCell(worldPos);
    }

    // 앞 셀의 GridObject 가져와서 푸시/픽업/문 열기
    private void Interact()
    {
        var cell = GetFrontCell();
        var occupancy = GridOccupancy.Instance;
        var obj = occupancy.GetOccupant(cell);

        // 1) 들고 있다면 내려놓기
        if (_carriedMirror != null)
        {
            if (!occupancy.IsBlockedCell(cell) && _carriedMirror.TryDrop(cell))
                _carriedMirror = null;
            return;
        }

        if (obj == null) return;

        // 2) 거울 들기
        var carryable = obj.GetComponent<CarryableMirror>();
        if (carryable != null && !carryable.IsCarried)
        {
            carryable.PickUp(transform);
            _carriedMirror = carryable;
            return;
        }

        // 3) 문 열기
        var door = obj.GetComponent<Door>();
        if (door != null && !door.IsOpen)
        {
            TryOpenDoor(door);
            return;
        }
    }

    private void TryOpenDoor(Door door)
    {
        switch (door.doorType)
        {
            case Door.E_DoorType.Basic:
                door.Open();
                break;
            case Door.E_DoorType.Locked:
                door.TryOpenWithNormalKey(ref normalKeyCount);
                break;
            case Door.E_DoorType.SpecialKey:
                door.TryOpenWithSpecialKey(hasSpecialKey);
                break;
        }
    }
}
