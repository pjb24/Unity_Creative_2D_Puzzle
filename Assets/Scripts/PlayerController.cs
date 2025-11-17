///
/// 중요한 건 직접 transform.position을 건드리지 않고, 항상 GridOccupancy.TryMove를 통해 이동하는 것.
/// 이렇게 하면 Dictionary 레벨에서 1타일 1오브젝트가 항상 보장된다.
/// 
/// 입력: WASD / 방향키
/// 이동
///     WASD/방향키 한 번 클릭 → 그리드 한 칸 이동
///     앞 칸에 PushableMirror 있으면 자동으로 TryPush 호출 → 성공 시 같이 한 칸 전진
/// 상호작용(E 키)
///     들고 있는 거울 있음 → 앞 칸이 비어 있으면 TryDrop(frontCell)
///     들고 있는 거울 없음 → 앞 칸에 CarryableMirror 있으면 PickUp(transform)
///     앞 칸에 닫힌 문 있으면 → TryOpenDoor(door)로 키 사용 시도
/// 
/// MovingFloor / RotatingFloor에서 플레이어 탑승 시 LockInput(), 내려갈 때 UnlockInput() 호출 가능
/// RoomManager 붙이면 문 통과 시 씬/카메라 이동 연결만 추가하면 됨
///

using UnityEngine;

[RequireComponent(typeof(GridObject))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 관련")]
    public float moveCooldown = 0.1f; // 연타 방지용

    private GridObject _gridObj;
    private float _lastMoveTime;
    private bool _canControl = true;

    // 플레이어가 바라보는 방향 (기본 위)
    private Vector2Int _facingDir = Vector2Int.up;

    // 들고 있는 거울
    private CarryableMirror _carriedMirror;

    // 키 보유 예시 (테스트용)
    public int normalKeyCount = 0;
    public bool hasSpecialKey = false;

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
    }

    private void Update()
    {
        if (!_canControl)
            return;

        HandleMoveInput();
        HandleInteractionInput();
    }

    #region 이동 처리

    private void HandleMoveInput()
    {
        // 한 번에 한 방향만 처리 (WASD / 방향키)
        Vector2Int dir = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            dir = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            dir = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            dir = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            dir = Vector2Int.right;

        if (dir == Vector2Int.zero)
            return;

        if (Time.time - _lastMoveTime < moveCooldown)
            return;

        TryStep(dir);
    }

    private void TryStep(Vector2Int dir)
    {
        _facingDir = dir;
        _lastMoveTime = Time.time;

        var occupancy = GridOccupancy.Instance;
        var current = _gridObj.CurrentCell;
        var target = current + new Vector3Int(dir.x, dir.y, 0);

        // 0) 벽/외곽 체크 (오브젝트 상관없이)
        if (occupancy.IsBlockedCell(target))
        {
            Debug.Log("벽 또는 점유 셀로 이동 불가");
            return;
        }

        // 1. 타겟 칸에 뭐가 있는지 확인
        var occupant = occupancy.GetOccupant(target);

        // 1) 비어 있으면 그냥 이동
        if (occupant == null)
        {
            occupancy.TryMove(_gridObj, target);
            return;
        }

        // 2) 밀 수 있는 거울이면 먼저 밀기 시도
        var pushable = occupant.GetComponent<PushableMirror>();
        if (pushable != null)
        {
            if (pushable.TryPush(dir))
            {
                // 거울 밀기 성공하면 내가 그 자리로 이동
                occupancy.TryMove(_gridObj, target);
            }
            // 밀기 실패하면 이동 취소
            return;
        }

        // 3) 문이면, 열린 문일 경우 그 칸으로 이동
        var door = occupant.GetComponent<Door>();
        if (door != null)
        {
            if (door.IsOpen)
            {
                occupancy.TryMove(_gridObj, target);
                // Door 쪽 OnTriggerEnter2D로 후속 처리(방 이동 등) 연결 가능
            }
            else
            {
                // 여기서 키 사용 로직을 넣을 수도 있음
                TryOpenDoor(door);
            }
            return;
        }

        // 그 외 오브젝트는 막힌 것으로 처리 (아무것도 안 함)
    }

    #endregion

    #region 상호작용 (E키)

    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        // 앞 칸 셀 계산 (바라보는 방향 기준)
        var frontCell = _gridObj.CurrentCell +
                        new Vector3Int(_facingDir.x, _facingDir.y, 0);

        var occupancy = GridOccupancy.Instance;
        var frontOccupant = occupancy.GetOccupant(frontCell);

        // 1) 거울을 들고 있는 상태 → 앞 칸에 내려놓기
        if (_carriedMirror != null)
        {
            // 앞 칸이 비어 있어야 내려놓기 가능
            if (!occupancy.IsOccupied(frontCell))
            {
                if (_carriedMirror.TryDrop(frontCell))
                {
                    _carriedMirror = null;
                }
            }
            else
            {
                Debug.Log("전방 셀이 이미 점유되어 있어 거울을 내려놓을 수 없습니다.");
            }

            return;
        }

        // 2) 아무것도 안 들고 있는 상태 → 앞 칸에서 집을 수 있는 것 찾기
        if (frontOccupant == null)
            return;

        // 2-1) CarryableMirror → 집기
        var carryable = frontOccupant.GetComponent<CarryableMirror>();
        if (carryable != null && !carryable.IsCarried)
        {
            carryable.PickUp(transform);
            _carriedMirror = carryable;
            return;
        }

        // 2-2) 닫힌 문 → 키로 열기 시도
        var door = frontOccupant.GetComponent<Door>();
        if (door != null && !door.IsOpen)
        {
            TryOpenDoor(door);
            return;
        }

        // 필요하면 여기서 장치/스위치 등의 상호작용도 확장
    }

    private void TryOpenDoor(Door door)
    {
        switch (door.doorType)
        {
            case Door.E_DoorType.Basic:
                // 기본 문인데 닫혀 있다? 강제로 열어주기
                door.Open();
                break;

            case Door.E_DoorType.Locked:
                if (!door.TryOpenWithNormalKey(ref normalKeyCount))
                {
                    Debug.Log("일반 키가 부족하거나 문을 열 수 없습니다.");
                }
                break;

            case Door.E_DoorType.SpecialKey:
                if (!door.TryOpenWithSpecialKey(hasSpecialKey))
                {
                    Debug.Log("특수 키가 없어서 문을 열 수 없습니다.");
                }
                break;
        }
    }

    #endregion

    #region 외부에서 호출할 Lock / Unlock

    public void LockInput()
    {
        _canControl = false;
    }

    public void UnlockInput()
    {
        _canControl = true;
    }

    #endregion
}
