// 통합 enum

public enum E_DoorType
{
    Basic,        // 일반 문 (기본적으로 플레이어 인터랙션으로 열림 가능)
    Locked,       // 일반 키 필요
    SpecialKey,   // 특정 키 필요
    DeviceOnly,   // 장치 출력으로만 열림/닫힘 (플레이어로는 절대 열 수 없음)
}

public enum E_DoorState { Closed, Opening, Open, Closing, Locked }

public enum E_KeyType { None, Basic, SpecialA, SpecialB }
