using System.Collections;
using UnityEngine;

public enum E_DoorType { Normal, Locked, SpecialKey }
public enum E_DoorState { Closed, Opening, Open, Closing, Locked }

public enum E_KeyType { Basic, SpecialA, SpecialB } // 필요 시 확장

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class DoorVisual : MonoBehaviour, IActivable
{
    [SerializeField] private E_DoorType _type = E_DoorType.Normal;
    [SerializeField] private E_KeyType _requiredKey = E_KeyType.Basic;
    [SerializeField] private float _openTime = 0.2f; // 연출용(아트 없으면 Collider 토글만)

    private BoxCollider2D _col;
    private SpriteRenderer _sr;
    private E_DoorState _state = E_DoorState.Closed;
    private bool _latched;           // on 상태 유지(장치가 유지형일 때)

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
        {
            _sr = gameObject.AddComponent<SpriteRenderer>(); // placeholder 색
        }

        UpdateVisual();
    }

    // 장치 출력 신호
    public void SetActiveState(bool on)
    {
        _latched = on;
        TryApplyLatch();
    }

    // 플레이어 상호작용 등
    public void TryOpenByPlayer()
    {
        if (_type == E_DoorType.Locked && _state == E_DoorState.Locked) return;
        if (_type == E_DoorType.SpecialKey && !PlayerInventory.Has(_requiredKey)) return;

        Open();
    }

    public void Lock()
    {
        _state = E_DoorState.Locked; UpdateVisual();
    }

    public void Unlock()
    {
        if (_state == E_DoorState.Locked)
        {
            _state = E_DoorState.Closed; UpdateVisual();
        }
    }

    void TryApplyLatch()
    {
        if (_state == E_DoorState.Locked)
            return;

        if (_latched)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    void Open()
    {
        if (_state == E_DoorState.Open)
            return;

        _state = E_DoorState.Opening;
        StopAllCoroutines();
        StartCoroutine(_OpenRoutine());
    }

    void Close()
    {
        if (_type == E_DoorType.SpecialKey && !PlayerInventory.Has(_requiredKey))
            return; // 키가 없으면 열린 상태 유지 옵션

        if (_state == E_DoorState.Closed) return;

        _state = E_DoorState.Closing;
        StopAllCoroutines();
        StartCoroutine(_CloseRoutine());
    }

    private IEnumerator _OpenRoutine()
    {
        yield return new WaitForSeconds(_openTime);
        _col.enabled = false;
        _state = E_DoorState.Open;
        UpdateVisual();
    }

    private  IEnumerator _CloseRoutine()
    {
        yield return new WaitForSeconds(_openTime);
        _col.enabled = true;
        _state = E_DoorState.Closed;
        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (_sr == null) return;

        // Placeholder 색상 규칙
        // Closed: 빨강, Open: 초록, Locked: 노랑
        Color c = _state switch
        {
            E_DoorState.Open or E_DoorState.Opening => Color.green,
            E_DoorState.Locked => Color.yellow,
            _ => Color.red
        };
        _sr.color = c;
    }
}
