public interface IInteractable
{
    /// <summary>
    /// 플레이어가 상호작용할 수 있는지 여부.
    /// </summary>
    bool CanInteract(PlayerInteractor interactor);

    /// <summary>
    /// 실제 상호작용 처리. 성공 여부 반환.
    /// </summary>
    bool Interact(PlayerInteractor interactor);
}
