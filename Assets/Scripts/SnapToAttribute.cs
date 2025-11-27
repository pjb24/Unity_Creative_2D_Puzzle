// SnapToAttribute.cs
using UnityEngine;

public class SnapToAttribute : PropertyAttribute
{
    public readonly float snapValue;
    public SnapToAttribute(float snapValue)
    {
        this.snapValue = snapValue;
    }
}
