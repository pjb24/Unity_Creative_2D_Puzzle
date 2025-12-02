///
/// 상호작용을 “좌표 변환”으로 연결
/// 플레이어는 자유 이동하지만,
/// 상호작용/퍼즐 판정은 전부 ‘그리드 셀’ 기준으로 처리한다.
///

using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float _interactRange = 0.8f;

    private PlayerFreeMove _move;
    private PlayerPusher _pusher;

    private CarryableMirror _carriedMirror;
    public int normalKeyCount;
    public bool hasSpecialKey;

    private void Awake()
    {
        _move = GetComponent<PlayerFreeMove>();
        _pusher = GetComponent<PlayerPusher>();
    }

    private void Update()
    {
        // 예시: E 키 = 상호작용
        if (Input.GetKeyDown(KeyCode.E))
            Interact();

        // 예시: R 키 = 거울 회전
        if (Input.GetKeyDown(KeyCode.R))
            RotateMirror();
    }

    // 플레이어 앞 셀 찾기
    private Vector3Int GetFrontCell()
    {
        var worldPos = (Vector2)transform.position + _move.FacingDir.normalized * _interactRange;
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

        if (_pusher.IsPushing)
        {
            _pusher.HandleInteract();

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

        var pushable = obj.GetComponent<PushableMirror>();
        if (pushable != null)
        {
            _pusher.HandleInteract();

            return;
        }

        var pathMirror = obj.GetComponent<PathMirror>();
        if (pathMirror != null)
        {
            pathMirror.TryMoveNext();

            return;
        }

        // 3) 문 열기
        var door = obj.GetComponentInChildren<DoorCore>();
        if (door != null && !door.IsOpen)
        {
            door.TryOpenByPlayer();

            return;
        }
    }

    private void RotateMirror()
    {
        var cell = GetFrontCell();
        var occupancy = GridOccupancy.Instance;
        var obj = occupancy.GetOccupant(cell);

        if (obj == null) return;

        var mirror = obj.GetComponent<Mirror>();
        if (mirror == null) return;

        mirror.Rotate45();
    }
}
