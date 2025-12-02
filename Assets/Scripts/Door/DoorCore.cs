///
/// Door (Parent)
///  ├─ DoorPortal   [Trigger Collider, GridObject]
///  └─ VisualRoot
///      └─ DoorCore [Blocking Collider(Non-Trigger), SpriteRenderer(placeholder)]
///      └─ DoorVisual [Animator 등 연출 전담]
/// 
/// 문 로직의 단일 진실원천(FSM, 차단 콜라이더, 월드 변경 이벤트)
///

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class DoorCore : MonoBehaviour, IActivable
{
    [Header("Policy")]
    [SerializeField] private E_DoorType _type = E_DoorType.Basic;
    public E_DoorType Type => _type;
    [SerializeField] private E_KeyType _requiredKey = E_KeyType.Basic;
    [SerializeField] private float _openTime = 0.2f;  // 연출 지연(콜라이더 토글 시점)
    [SerializeField] private bool _basicStartsOpen = true;

    private Collider2D _blockCollider;  // '길막'용 콜라이더(Trigger=아님). 없으면 본체 Box 사용

    [Header("Debug/Placeholder")]
    [SerializeField] private SpriteRenderer _sr;        // 플레이스홀더 색상(아트 전 단계)
    [SerializeField] private bool _logTransitions = true;

    [Header("Events (Visual 전용)")]
    public UnityEvent<E_DoorState> OnStateChanged;      // DoorVisual, Animator가 구독

    public E_DoorState State { get; private set; } = E_DoorState.Closed;
    public bool IsOpen => State == E_DoorState.Open || State == E_DoorState.Opening;

    private bool _latched;     // 장치 출력 유지 여부(래치)
    private Coroutine _transitionCo;

    void Reset()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = false; // 기본은 '길막'으로 사용
        }

        _blockCollider = box;
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Awake()
    {
        if (_blockCollider == null) _blockCollider = GetComponent<Collider2D>();
        if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>();

        // 초기 상태
        if (_type == E_DoorType.Basic && _basicStartsOpen)
        {
            OpenImmediate();
        }
        else
        {
            CloseImmediate();
        }
    }

    // 외부 Device를 통한 제어(래치/홀드)를 위한 표준 입력
    public void SetActiveState(bool on)
    {
        _latched = on;
        TryApplyLatch();
    }

    // 플레이어 상호작용 기반 오픈(키 정책 반영)
    public bool TryOpenByPlayer()
    {
        if (_type == E_DoorType.Locked)
        {
            // 일반키 필요
            if (IsOpen) return true;
            if (!PlayerInventory.ConsumeNormalKey()) return false;
            Open(); return true;
        }
        if (_type == E_DoorType.SpecialKey)
        {
            if (IsOpen) return true;
            if (!PlayerInventory.Has(_requiredKey)) return false;
            Open(); return true;
        }
        // Basic
        if (!IsOpen) Open();
        return true;
    }

    public void LockDoor()
    {
        if (_transitionCo != null) StopCoroutine(_transitionCo);

        State = E_DoorState.Locked;
        ApplyCollisionAndPlaceholder();
        RaiseWorldChanged();
        RaiseStateEvent();

        if (_logTransitions) Debug.Log($"[DoorCore] {name} -> Locked");
    }

    public void UnlockDoor()
    {
        if (State == E_DoorState.Locked)
        {
            State = E_DoorState.Closed;
            ApplyCollisionAndPlaceholder();
            RaiseWorldChanged();
            RaiseStateEvent();

            if (_logTransitions) Debug.Log($"[DoorCore] {name} -> Closed (Unlock)");
        }
    }

    void TryApplyLatch()
    {
        if (State == E_DoorState.Locked) return;

        if (_latched)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    public void Open()
    {
        if (IsOpen) return;

        if (_transitionCo != null) StopCoroutine(_transitionCo);

        State = E_DoorState.Opening;
        RaiseStateEvent();
        _transitionCo = StartCoroutine(CoOpen());

        if (_logTransitions) Debug.Log($"[DoorCore] {name} Opening...");
    }

    public void Close()
    {
        if (State == E_DoorState.Closed || State == E_DoorState.Locked) return;

        if (_type == E_DoorType.SpecialKey && PlayerInventory.Has(_requiredKey))
        {
            // 정책에 따라 '특수키 보유 중엔 항상 열림'으로 운용하려면 여기서 return;
        }
        if (_transitionCo != null) StopCoroutine(_transitionCo);

        State = E_DoorState.Closing;
        RaiseStateEvent();
        _transitionCo = StartCoroutine(CoClose());

        if (_logTransitions) Debug.Log($"[DoorCore] {name} Closing...");
    }

    void OpenImmediate()
    {
        State = E_DoorState.Open;
        ApplyCollisionAndPlaceholder();
        RaiseWorldChanged();
        RaiseStateEvent();

        if (_logTransitions) Debug.Log($"[DoorCore] {name} Open (Immediate)");
    }

    void CloseImmediate()
    {
        State = E_DoorState.Closed;
        ApplyCollisionAndPlaceholder();
        RaiseWorldChanged();
        RaiseStateEvent();

        if (_logTransitions) Debug.Log($"[DoorCore] {name} Closed (Immediate)");
    }

    IEnumerator CoOpen()
    {
        yield return new WaitForSeconds(_openTime);

        State = E_DoorState.Open;
        ApplyCollisionAndPlaceholder();
        RaiseWorldChanged();
        RaiseStateEvent();

        if (_logTransitions) Debug.Log($"[DoorCore] {name} Open");
    }

    IEnumerator CoClose()
    {
        yield return new WaitForSeconds(_openTime);

        State = E_DoorState.Closed;
        ApplyCollisionAndPlaceholder();
        RaiseWorldChanged();
        RaiseStateEvent();

        if (_logTransitions) Debug.Log($"[DoorCore] {name} Closed");
    }

    void ApplyCollisionAndPlaceholder()
    {
        if (_blockCollider) _blockCollider.enabled = !IsOpen; // 열리면 통과 가능
        if (_sr)
        {
            // 플레이스홀더 색: Open=초록, Locked=노랑, 나머지=빨강
            _sr.color = State switch
            {
                E_DoorState.Open or E_DoorState.Opening => Color.green,
                E_DoorState.Locked => Color.yellow,
                _ => Color.red
            };
        }
    }

    void RaiseWorldChanged()
    {
        // 레이저 경로 재계산 트리거
        LaserWorldEvents.RaiseWorldChanged();
    }

    void RaiseStateEvent()
    {
        OnStateChanged?.Invoke(State);
    }
}
