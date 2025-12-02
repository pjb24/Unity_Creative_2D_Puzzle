///
/// Door (Parent)
///  ├─ DoorPortal   [Trigger Collider, GridObject]
///  └─ VisualRoot
///      └─ DoorCore [Blocking Collider(Non-Trigger), SpriteRenderer(placeholder)]
///      └─ DoorVisual [Animator 등 연출 전담]
/// 
/// 방 전환 전용(트리거 콜라이더)
///

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DoorPortal : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string _doorId;
    public string DoorId => _doorId;

    [Header("Link")]
    [SerializeField] private RoomController _room;          // 소속 Room
    [SerializeField] private RoomController _targetRoom;    // 목적지 Room
    [SerializeField] private string _targetDoorId;          // 목적지 DoorId

    [Header("Spawn")]
    [SerializeField] private Vector2 _spawnOffset;

    [Header("Refs")]
    [SerializeField] private DoorCore _core;                // 같은 문 로직

    private BoxCollider2D _trigger;

    void Reset()
    {
        _trigger = GetComponent<BoxCollider2D>();
        if (_trigger != null) _trigger.isTrigger = true;

        if (_room == null) _room = GetComponentInParent<RoomController>();
        if (_core == null) _core = GetComponentInChildren<DoorCore>();
    }

    void Awake()
    {
        _trigger = GetComponent<BoxCollider2D>();
        if (_core == null) _core = GetComponentInChildren<DoorCore>();
        if (_trigger != null) _trigger.isTrigger = true;
    }

    public Vector3 GetSpawnPosition() => (Vector3)_spawnOffset;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_core == null || !_core.IsOpen) return;
        if (!other.CompareTag("Player")) return;
        if (RoomManager.Instance == null) return;
        if (_targetRoom == null) return;

        Debug.Log($"[DoorPortal] Player entered {name}");

        var toDoor = _targetRoom.FindDoor(_targetDoorId);  // RoomController가 DoorPortal 또는 DoorCore 조회 제공
        RoomManager.Instance.ChangeRoom(_targetRoom, this, toDoor);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
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
