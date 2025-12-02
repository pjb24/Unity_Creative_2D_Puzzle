///
/// 싱글톤 + 룸 전환 + 카메라 이동
/// 
/// CinemachineCamera는
/// Follow/LookAt 비워두고, 위 스크립트가 vcam 위치를 직접 이동
///

using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Initial Setup")]
    [SerializeField] private RoomController _startRoom;
    [SerializeField] private Transform _player;
    [SerializeField] private CinemachineCamera _vcam;
    [SerializeField] private float _cameraMoveDuration = 0.5f;

    private readonly Dictionary<string, RoomController> _rooms = new Dictionary<string, RoomController>();
    private RoomController _currentRoom;
    private bool _isTransitioning;

    public RoomController CurrentRoom => _currentRoom;
    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 필요하면 DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        if (_startRoom != null)
        {
            SetCurrentRoomImmediate(_startRoom);
        }
    }

    #region Room Registration

    public void RegisterRoom(RoomController room)
    {
        if (room == null || string.IsNullOrEmpty(room.RoomId))
            return;

        if (!_rooms.ContainsKey(room.RoomId))
        {
            _rooms.Add(room.RoomId, room);
        }
    }

    public RoomController GetRoom(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
            return null;

        _rooms.TryGetValue(roomId, out var room);
        return room;
    }

    #endregion

    #region Room Change

    // Door에서 직접 호출하는 API
    public void ChangeRoom(RoomController targetRoom, DoorPortal fromDoor, DoorPortal toDoor)
    {
        if (_isTransitioning) return;
        if (targetRoom == null) return;

        StartCoroutine(ChangeRoomRoutine(targetRoom, fromDoor, toDoor));
    }

    private IEnumerator ChangeRoomRoutine(RoomController targetRoom, DoorPortal fromDoor, DoorPortal toDoor)
    {
        _isTransitioning = true;

        // 입력 Lock
        //var player = _player.GetComponent<PlayerFreeMove>();
        //if (player != null)
        //{
        //    player.SetExternalLock(true);
        //}

        // 플레이어 위치 결정
        if (_player != null && toDoor != null)
        {
            _player.position += toDoor.GetSpawnPosition();
        }

        // 카메라 이동
        if (_vcam != null)
        {
            Vector3 start = _vcam.transform.position;
            Vector3 target = targetRoom.GetCameraCenterWorld();
            target.z = start.z; // 카메라 Z 유지

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / _cameraMoveDuration;
                _vcam.transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            _vcam.transform.position = target;
        }

        _currentRoom = targetRoom;

        // TODO: 룸 활성/비활성 처리 필요하면 여기에서
        // ex) EnableCurrentRoomOnly();

        // 입력 Unlock
        //if (player != null)
        //{
        //    player.SetExternalLock(false);
        //}

        _isTransitioning = false;
    }

    private void SetCurrentRoomImmediate(RoomController room)
    {
        _currentRoom = room;

        if (_vcam != null)
        {
            var target = room.GetCameraCenterWorld();
            var camPos = _vcam.transform.position;
            target.z = camPos.z;
            _vcam.transform.position = target;
        }

        // TODO: 초기 플레이어 스폰 위치가 필요하면 여기서 처리
    }

    #endregion
}
