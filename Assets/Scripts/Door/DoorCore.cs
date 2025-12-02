///
/// Door (Parent)
///  ├─ DoorCore [Blocking Collider(Non-Trigger), SpriteRenderer(placeholder)]
///  └─ DoorPortal   [Trigger Collider, GridObject]
///  └─ DoorVisual [Animator 등 연출 전담]
/// 
/// DoorCore: 문 로직의 단일 진실원천
/// - FSM, 차단 콜라이더, 월드 변경 이벤트 트리거
///

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class DoorCore : MonoBehaviour, IActivable, IInteractable
{
    [Header("Policy")]
    [SerializeField] private E_DoorType _type = E_DoorType.Basic;
    public E_DoorType Type => _type;

    [Tooltip("SpecialKey 타입일 때 필요한 키. 그 외 타입에서도 None/Basic 등으로 사용할 수 있음.")]
    [SerializeField] private E_KeyType _requiredKey = E_KeyType.None;

    [Tooltip("연출 지연(콜라이더 토글 시점). 아트가 붙으면 애니 타이밍과 동기화.")]
    [SerializeField] private float _openTime = 0.2f;  // 연출 지연(콜라이더 토글 시점)
    
    [Tooltip("Basic만 적용. 시작 시 즉시 Open 처리.")]
    [SerializeField] private bool _basicStartsOpen = true;

    [Header("Blocking Collider (Non-Trigger)")]
    private Collider2D _blockCollider;  // '길막'용 콜라이더(Trigger=아님). 없으면 본체 Box 사용

    [Header("Debug/Placeholder")]
    [SerializeField] private SpriteRenderer _sr;        // 플레이스홀더 색상(아트 전 단계)
    [SerializeField] private bool _logTransitions = true;

    [Header("Events (Visual 전용)")]
    public UnityEvent<E_DoorState> OnStateChanged;      // DoorVisual, Animator가 구독

    public E_DoorState State { get; private set; } = E_DoorState.Closed;
    public bool IsOpen => State == E_DoorState.Open || State == E_DoorState.Opening;

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

    // 장치 출력(레이저 장치/레버 등)으로 제어
    // DeviceOnly: on/off에 그대로 반응
    // 기타 타입: 장치와 플레이어 모두 허용(디자인 정책에 따라 사용)
    public void SetActiveState(bool on)
    {
        if (_type == E_DoorType.DeviceOnly)
        {
            if (on)
            {
                Open();
            }
            else
            {
                Close();
            }
        }
    }

    // 플레이어 상호작용(문 앞에서 E 등)
    // DeviceOnly는 항상 false 반환
    public bool TryOpenByPlayer()
    {
        if (_type == E_DoorType.DeviceOnly)
        {
            return false;   // 장치 전용
        }

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
        if (_type == E_DoorType.Locked || _type == E_DoorType.SpecialKey)
        {
            State = E_DoorState.Locked;
        }
        else
        {
            State = E_DoorState.Closed;
        }

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

    public bool CanInteract(PlayerInteractor interactor)
    {
        return interactor != null && _type != E_DoorType.DeviceOnly;
    }

    public bool Interact(PlayerInteractor interactor)
    {
        return TryOpenByPlayer();
    }
}
