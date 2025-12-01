///
/// 타입: Basic / Locked / SpecialKey
/// Basic은 시작부터 open
/// Locked / SpecialKey는 외부에서 TryOpen… 호출해서 열기
/// 열려 있을 때 Player가 들어오면 “문 통과됨” 로그만 찍는 수준
/// 
/// 
/// Door의 Room 필드 비면 Reset()에서 자동으로 부모 Room 할당
/// TargetRoom, TargetDoorId로 이동 목적 지정
///

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(GridObject))]
public class Door : MonoBehaviour
{
    public enum E_DoorType
    {
        Basic,
        Locked,
        SpecialKey
    }

    [Header("Door Id")]
    [SerializeField] private string _doorId;
    public string DoorId => _doorId;

    [Header("Room Link")]
    [SerializeField] private RoomController _room;          // 이 Door가 속한 Room
    public RoomController Room => _room;
    [SerializeField] private RoomController _targetRoom;    // 이동할 대상 Room
    public RoomController TargetRoom => _targetRoom;
    [SerializeField] private string _targetDoorId;          // 대상 Room 안의 Door Id
    public string TargetDoorId => _targetDoorId;

    public E_DoorType doorType = E_DoorType.Basic;

    [Header("Spawn Point")]
    [SerializeField] private Vector2 _spawnOffset;          // 플레이어 스폰 오프셋

    public bool IsOpen { get; private set; }

    private BoxCollider2D _collider;
    private GridObject _gridObj;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
        _gridObj = GetComponent<GridObject>();

        if (doorType == E_DoorType.Basic)
            Open();
        else
            Close();
    }

    private void Reset()
    {
        // 자동 Room 할당 시도
        if (_room == null)
        {
            _room = GetComponentInParent<RoomController>();
        }

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position + (Vector3)_spawnOffset;
    }

    public void Open()
    {
        IsOpen = true;

        // 시각적 표현은 나중에: 색 변경, 애니메이션 등

        // Collider On/Off 등 처리 후
        LaserWorldEvents.RaiseWorldChanged();

        Debug.Log($"Door {name} opened.");
    }

    public void Close()
    {
        IsOpen = false;
        Debug.Log($"Door {name} closed.");
    }

    // 일반 키로 여는 잠긴 문
    public bool TryOpenWithNormalKey(ref int normalKeyCount)
    {
        if (doorType != E_DoorType.Locked) return false;
        if (IsOpen) return true;
        if (normalKeyCount <= 0) return false;

        normalKeyCount--;
        Open();
        return true;
    }

    // 특수 키 문
    public bool TryOpenWithSpecialKey(bool hasSpecialKey)
    {
        if (doorType != E_DoorType.SpecialKey) return false;
        if (IsOpen) return true;
        if (!hasSpecialKey) return false;

        Open();
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOpen) return;
        if (!other.CompareTag("Player")) return;
        if (RoomManager.Instance == null) return;
        if (_targetRoom == null) return;

        // 여기서 RoomManager 등에 문 통과 이벤트를 넘기면 된다.
        Debug.Log($"Player entered door {name} (type: {doorType}).");

        // 대상 Room 안의 Door 찾기
        Door toDoor = _targetRoom.FindDoor(_targetDoorId);
        RoomManager.Instance.ChangeRoom(_targetRoom, this, toDoor);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GetSpawnPosition(), 0.15f);

        if (_targetRoom != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _targetRoom.CenterOfRoom);
        }
    }
#endif
}
