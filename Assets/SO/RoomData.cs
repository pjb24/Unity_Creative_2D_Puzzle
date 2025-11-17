using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomData", menuName = "Scriptable Objects/RoomData")]
public class RoomData : ScriptableObject
{
    public string roomId;
    public RectInt cellBound;
    public List<DoorInfo> doors;
}

[System.Serializable]
public class DoorInfo
{
    public string doorId;
    public Vector3Int cellPos;
    public string targetRoomId;
    public string targetDoorId;
}
