///
/// 한 프레임 내 여러 오브젝트가 상태를 바꿔도
/// OnWorldChanged는 여러 번 불릴 수 있지만, LaserManager는 그저 _isDirty = true만 세우고
/// Update 한 번에 전체 레이저를 재계산해서 부담을 줄일 수 있다.
///

using System;

public static class LaserWorldEvents
{
    public static Action OnWorldChanged;

    public static void RaiseWorldChanged()
    {
        OnWorldChanged?.Invoke();
    }
}
