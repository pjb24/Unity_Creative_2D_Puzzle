///
/// 어트리뷰트 정의
/// Any/All 지원
/// ShowIf/HideIf 지원
///

using System;
using UnityEngine;

public enum CondMode { Any, All }

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ConditionalAttribute : PropertyAttribute
{
    public readonly string SourcePath;  // 점 경로, ../ 지원, $로 절대경로
    public readonly object[] Values;    // 비교 대상 값들
    public readonly CondMode Mode;      // Any = OR, All = AND
    public readonly bool Invert;        // HideIf = true
    public readonly bool Flags;         // Enum Flags 비교 모드

    public ConditionalAttribute(string sourcePath, CondMode mode, bool invert, bool flags, params object[] values)
    {
        SourcePath = sourcePath;
        Mode = mode;
        Invert = invert;
        Flags = flags;
        Values = values ?? Array.Empty<object>();
    }
}

// 별칭 어트리뷰트
public class ShowIfAnyAttribute : ConditionalAttribute
{ public ShowIfAnyAttribute(string src, params object[] any) : base(src, CondMode.Any, false, false, any) { } }

public class ShowIfAllAttribute : ConditionalAttribute
{ public ShowIfAllAttribute(string src, params object[] all) : base(src, CondMode.All, false, false, all) { } }

public class HideIfAnyAttribute : ConditionalAttribute
{ public HideIfAnyAttribute(string src, params object[] any) : base(src, CondMode.Any, true, false, any) { } }

public class HideIfAllAttribute : ConditionalAttribute
{ public HideIfAllAttribute(string src, params object[] all) : base(src, CondMode.All, true, false, all) { } }

// Enum Flags 전용
public class ShowIfFlagsAnyAttribute : ConditionalAttribute
{ public ShowIfFlagsAnyAttribute(string src, params object[] anyFlags) : base(src, CondMode.Any, false, true, anyFlags) { } }

public class ShowIfFlagsAllAttribute : ConditionalAttribute
{ public ShowIfFlagsAllAttribute(string src, params object[] allFlags) : base(src, CondMode.All, false, true, allFlags) { } }

public class HideIfFlagsAnyAttribute : ConditionalAttribute
{ public HideIfFlagsAnyAttribute(string src, params object[] anyFlags) : base(src, CondMode.Any, true, true, anyFlags) { } }

public class HideIfFlagsAllAttribute : ConditionalAttribute
{ public HideIfFlagsAllAttribute(string src, params object[] allFlags) : base(src, CondMode.All, true, true, allFlags) { } }
