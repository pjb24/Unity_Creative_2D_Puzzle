///
/// Room ID, 카메라 중심, Door 관리
/// 
/// 
/// Room 하나당 RoomController 붙이고, Door들을 자식으로 배치 → Door 붙이기
/// 
/// 
///

using System.Collections.Generic;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    [SerializeField] private string _roomId;

    [Header("Camera")]
    [SerializeField] private Vector2 _cameraCenterOffset; // Room 기준 카메라 중심 오프셋
    [SerializeField] private Vector2 _roomSize = new Vector2(18, 10); // 디버그용, 선택사항

    [Header("Doors")]
    [SerializeField] private List<Door> _doors = new List<Door>();

    private Vector3 _centerOfRoom;
    public Vector3 CenterOfRoom => _centerOfRoom;

    public string RoomId => _roomId;
    public IReadOnlyList<Door> Doors => _doors;

    private void Awake()
    {
        // RoomManager 등록
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.RegisterRoom(this);
        }
    }

    public Vector3 GetCameraCenterWorld()
    {
        // Room의 기준 위치 + 오프셋
        return transform.position + (Vector3)_cameraCenterOffset;
    }

    public Door FindDoor(string doorId)
    {
        if (string.IsNullOrEmpty(doorId))
            return null;

        foreach (var door in _doors)
        {
            if (door != null && door.DoorId == doorId)
                return door;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Room Bounds 시각화 (선택사항)
        Gizmos.color = Color.yellow;
        var center = transform.position;
        var size = new Vector3(_roomSize.x, _roomSize.y, 0f);
        Gizmos.DrawWireCube(center, size);

        // 카메라 중심 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(GetCameraCenterWorld(), 0.2f);
    }

    private void OnValidate()
    {
        _centerOfRoom = _cameraCenterOffset;
    }
#endif
}
