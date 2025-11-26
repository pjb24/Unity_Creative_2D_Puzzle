///
/// Drawer 구현
/// 다중 어트리뷰트 결합
/// Enum, Flags, bool, int 지원
///

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Globalization;

[CustomPropertyDrawer(typeof(ConditionalAttribute), true)]
public class ConditionalDrawer : PropertyDrawer
{
    private const double FLOAT_EPS = 1e-4; // 부동소수 비교 오차

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool show = EvaluateAll(property);

        return show ? EditorGUI.GetPropertyHeight(property, label, true) : 0f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (EvaluateAll(property))
            EditorGUI.PropertyField(position, property, label, true);
    }

    private bool EvaluateAll(SerializedProperty property)
    {
        // 같은 필드에 여러 조건이 부착된 경우 모두 AND로 결합
        var conds = fieldInfo.GetCustomAttributes(typeof(ConditionalAttribute), true).Cast<ConditionalAttribute>().ToArray();
        if (conds.Length == 0) return true;

        foreach (var cond in conds)
        {
            bool pass = EvaluateOne(property, cond);
            if (!pass) return false;
        }
        return true;
    }

    private bool EvaluateOne(SerializedProperty property, ConditionalAttribute cond)
    {
        var src = CondPathUtil.FindBySmartPath(property, cond.SourcePath);
        if (src == null) return true; // 소스 없으면 표시

        bool match;
        if (cond.Flags)
            match = MatchFlags(src, cond.Values, cond.Mode);
        else
            match = MatchSimple(src, cond.Values, cond.Mode);

        return cond.Invert ? !match : match;
    }

    // bool, int, float, string, enum(값/이름/인덱스) 비교
    private bool MatchSimple(SerializedProperty src, object[] values, CondMode mode)
    {
        Func<object, bool> pred = v =>
        {
            switch (src.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return src.boolValue == Convert.ToBoolean(v);

                case SerializedPropertyType.Integer:
                    return src.intValue == Convert.ToInt32(v);

                case SerializedPropertyType.Float:
                    {
                        // v가 float/double/decimal 숫자거나 파싱 가능한 문자열이면 비교
                        double target;
                        if (v is float f) target = f;
                        else if (v is double d) target = d;
                        else if (v is decimal m) target = (double)m;
                        else if (v is int i) target = i;    // 정수 리터럴도 허용
                        else if (v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                            target = parsed;
                        else return false;

                        return Math.Abs(src.floatValue - target) <= FLOAT_EPS;
                    }

                case SerializedPropertyType.String:
                    {
                        // 대소문자 구분 기본. 필요 시 StringComparison 옵션으로 확장 가능
                        if (v is string s) return string.Equals(src.stringValue, s, StringComparison.Ordinal);
                        // enum 이름과 혼동 방지: 문자열로만 비교
                        return false;
                    }

                case SerializedPropertyType.Enum:
                    {
                        // 우선 인덱스 비교
                        if (v is int i) return src.enumValueIndex == i;
                        // enum 인스턴스면 그 값과 비교
                        if (v is Enum e) return src.enumValueIndex == Convert.ToInt32(e);
                        // 이름 문자열 비교
                        if (v is string s) return src.enumNames[src.enumValueIndex] == s;
                        return false;
                    }

                default:
                    // 필요 시 타입 추가(string, float 등)
                    return false;
            }
        };

        return mode == CondMode.Any ? values.Any(pred) : values.All(pred);
    }

    // Enum Flags: src의 내부 정수값과 비트 비교
    private bool MatchFlags(SerializedProperty src, object[] values, CondMode mode)
    {
        // Enum이라도 내부는 int로 읽을 수 있다
        int current = src.propertyType == SerializedPropertyType.Enum ? src.intValue : src.intValue;

        Func<object, bool> hasFlag = v =>
        {
            int mask = Convert.ToInt32(v);
            return (current & mask) == mask; // All: 해당 마스크 비트 모두 켜짐
        };

        if (mode == CondMode.Any)
            return values.Any(v => (current & Convert.ToInt32(v)) != 0); // Any: 한 비트라도 켜짐
        else
            return values.All(hasFlag);
    }
}
