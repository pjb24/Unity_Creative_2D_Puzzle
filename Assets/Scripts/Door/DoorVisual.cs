///
/// Door (Parent)
///  ├─ DoorPortal   [Trigger Collider, GridObject]
///  └─ VisualRoot
///      └─ DoorCore [Blocking Collider(Non-Trigger), SpriteRenderer(placeholder)]
///      └─ DoorVisual [Animator 등 연출 전담]
/// 
/// DoorVisual: 아트/애니/사운드 전담. 로직은 DoorCore가 전부 담당.
/// - 현 단계: placeholder 색은 DoorCore가 처리하므로 여기서는 Animator/FX 훅만.
/// - 추후: 스킨 교체(프리팹 교체), Addressables 로딩, VFX/사운드 재생 등 연결.
///

using UnityEngine;

[DisallowMultipleComponent]
public class DoorVisual : MonoBehaviour, IActivable
{
    [SerializeField] private DoorCore _core;
    [Header("Optional Refs")]
    [SerializeField] private Animator _anim; // 아티스트가 붙일 Animator

    // 애니 파라미터명(필요시 인스펙터에서 변경)
    [SerializeField] private string _paramState = "DoorState"; // int
    [SerializeField] private string _paramOpenTrigger = "Open";
    [SerializeField] private string _paramCloseTrigger = "Close";
    [SerializeField] private string _paramLockedBool = "Locked";

    void Reset()
    {
        if (_core == null) _core = GetComponentInParent<DoorCore>();
        if (_anim == null) _anim = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        if (_core == null) _core = GetComponentInParent<DoorCore>();
        if (_core != null) _core.OnStateChanged.AddListener(OnCoreStateChanged);
    }

    void OnDestroy()
    {
        if (_core != null) _core.OnStateChanged.RemoveListener(OnCoreStateChanged);
    }

    void OnCoreStateChanged(E_DoorState state)
    {
        if (_anim == null) return;

        _anim.SetInteger(_paramState, (int)state);
        _anim.SetBool(_paramLockedBool, state == E_DoorState.Locked);

        if (state == E_DoorState.Opening) _anim.SetTrigger(_paramOpenTrigger);
        if (state == E_DoorState.Closing) _anim.SetTrigger(_paramCloseTrigger);
        // Open/Closed 도착 상태에서 루핑 애니가 있으면 상태값으로 동작
    }

    // 장치 바인더 호환(아트 시스템 단에 연결돼 있어도 안전하게 코어로 위임)
    public void SetActiveState(bool on)
    {
        if (_core != null) _core.SetActiveState(on);
    }
}
