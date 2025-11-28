using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class WallToggle : MonoBehaviour, IActivable
{
    [SerializeField] private bool _isOn = true;       // ON=벽 올라옴(막음)
    [SerializeField] private float _anim = 0.15f;     // 연출용

    private BoxCollider2D _col;
    private SpriteRenderer _sr;

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
        }

        ApplyStateImmediate();
    }

    public void SetActiveState(bool on)
    {
        _isOn = on;
        StopAllCoroutines();
        StartCoroutine(_ApplyRoutine());
    }

    private IEnumerator _ApplyRoutine()
    {
        yield return new WaitForSeconds(_anim);
        ApplyStateImmediate();
    }

    private void ApplyStateImmediate()
    {
        _col.enabled = _isOn;
        _sr.color = _isOn ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.2f, 0.2f, 0.2f, 0.25f);
    }
}
