///
/// 타입: Basic / Locked / SpecialKey
/// Basic은 시작부터 open
/// Locked / SpecialKey는 외부에서 TryOpen… 호출해서 열기
/// 열려 있을 때 Player가 들어오면 “문 통과됨” 로그만 찍는 수준
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

    public E_DoorType doorType = E_DoorType.Basic;

    public bool IsOpen { get; private set; }

    private BoxCollider2D _collider;
    private GridObject _gridObj;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
        _gridObj = GetComponent<GridObject>();

        // 문은 플레이어와 충돌해야 하므로 Trigger 사용 안함.
        _collider.isTrigger = false;

        if (doorType == E_DoorType.Basic)
            Open();
        else
            Close();
    }

    public void Open()
    {
        IsOpen = true;
        // 시각적 표현은 나중에: 색 변경, 애니메이션 등
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

        // 여기서 RoomManager 등에 문 통과 이벤트를 넘기면 된다.
        Debug.Log($"Player entered door {name} (type: {doorType}).");
    }
}
