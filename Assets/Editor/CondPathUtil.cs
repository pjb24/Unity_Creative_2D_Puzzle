///
/// 경로 유틸
/// 점 경로, ../, 절대 $, 배열 경로 유지
///

using System.Linq;
using UnityEditor;

public static class CondPathUtil
{
    // parent.path + sibling 형태 기본. 상대경로 ../ 지원. $로 시작하면 절대 경로.
    public static SerializedProperty FindBySmartPath(SerializedProperty property, string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return null;

        string finalPath;
        if (sourcePath.StartsWith("$"))
        {
            finalPath = sourcePath.Substring(1); // 절대 경로
        }
        else
        {
            string parent = GetParentPath(property.propertyPath);
            finalPath = ResolveRelative(parent, sourcePath);
        }

        return property.serializedObject.FindProperty(finalPath);
    }

    // 현재 프로퍼티의 부모 경로
    public static string GetParentPath(string path)
    {
        int last = path.LastIndexOf('.');
        return last >= 0 ? path.Substring(0, last) : string.Empty;
    }

    // ../ 세그먼트 처리. Unity의 "Array.data[x]" 세그먼트는 그대로 유지.
    public static string ResolveRelative(string basePath, string rel)
    {
        if (string.IsNullOrEmpty(basePath)) return rel;

        var baseSegs = basePath.Split('.').ToList();
        var relSegs = rel.Split('.');

        foreach (var seg in relSegs)
        {
            if (seg == "..")
            {
                if (baseSegs.Count > 0) baseSegs.RemoveAt(baseSegs.Count - 1);
            }
            else if (seg == ".")
            {
                // noop
            }
            else
            {
                baseSegs.Add(seg);
            }
        }

        return string.Join(".", baseSegs);
    }
}
