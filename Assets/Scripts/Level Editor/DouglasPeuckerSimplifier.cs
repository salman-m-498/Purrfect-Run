using System.Collections.Generic;
using UnityEngine;

public static class DouglasPeuckerSimplifier
{
    public static List<Vector3> Simplify(List<Vector3> points, float tolerance)
    {
        if (points == null || points.Count < 3) return new List<Vector3>(points);
        bool[] keep = new bool[points.Count];
        keep[0] = keep[points.Count - 1] = true;
        SimplifySection(points, 0, points.Count - 1, tolerance, keep);
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < points.Count; i++) if (keep[i]) result.Add(points[i]);
        return result;
    }

    static void SimplifySection(List<Vector3> pts, int first, int last, float tol, bool[] keep)
    {
        if (last <= first + 1) return;
        float maxDist = -1f;
        int index = -1;
        Vector3 a = pts[first];
        Vector3 b = pts[last];
        for (int i = first + 1; i < last; i++)
        {
            float d = PerpendicularDistance(pts[i], a, b);
            if (d > maxDist) { index = i; maxDist = d; }
        }

        if (maxDist > tol)
        {
            keep[index] = true;
            SimplifySection(pts, first, index, tol, keep);
            SimplifySection(pts, index, last, tol, keep);
        }
    }

    static float PerpendicularDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        if (a == b) return Vector3.Distance(p, a);
        Vector3 ap = p - a;
        Vector3 ab = b - a;
        float ab2 = Vector3.Dot(ab, ab);
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab2);
        Vector3 proj = a + ab * t;
        return Vector3.Distance(p, proj);
    }
}
