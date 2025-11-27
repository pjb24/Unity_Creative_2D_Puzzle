// SnapToDrawer.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SnapToAttribute))]
public class SnapToDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginChangeCheck();

        var snap = ((SnapToAttribute)attribute).snapValue;

        // 타입별 처리 분기
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                float fValue = EditorGUI.FloatField(position, label, property.floatValue);
                if (EditorGUI.EndChangeCheck())
                    property.floatValue = Mathf.Round(fValue / snap) * snap;
                break;

            case SerializedPropertyType.Integer:
                int iValue = EditorGUI.IntField(position, label, property.intValue);
                if (EditorGUI.EndChangeCheck())
                    property.intValue = Mathf.RoundToInt(Mathf.Round(iValue / snap) * snap);
                break;

            default:
                EditorGUI.LabelField(position, label.text, "SnapTo only supports int/float.");
                break;
        }
    }
}
#endif
