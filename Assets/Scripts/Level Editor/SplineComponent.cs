using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class SplineComponent : MonoBehaviour
{
    public List<Vector3> controlPoints = new List<Vector3>() { Vector3.zero, Vector3.right * 5f, Vector3.right * 10f };
    public bool loop = false;
    public Color gizmoColor = Color.cyan;
    public float handleSize = 0.3f;

    // Catmull-Rom sampling
    public Vector3 GetPoint(float t)
    {
        if (controlPoints == null || controlPoints.Count == 0) return transform.position;
        if (controlPoints.Count == 1) return transform.TransformPoint(controlPoints[0]);

        // Map t to segment
        int numSections = loop ? controlPoints.Count : controlPoints.Count - 1;
        if (numSections <= 0) return transform.TransformPoint(controlPoints[0]);

        float tScaled = Mathf.Clamp01(t) * numSections;
        int currPt = Mathf.FloorToInt(tScaled);
        float localT = tScaled - currPt;

        // indices for Catmull-Rom
        int p0 = WrapIndex(currPt - 1);
        int p1 = WrapIndex(currPt);
        int p2 = WrapIndex(currPt + 1);
        int p3 = WrapIndex(currPt + 2);

        Vector3 P0 = controlPoints[p0];
        Vector3 P1 = controlPoints[p1];
        Vector3 P2 = controlPoints[p2];
        Vector3 P3 = controlPoints[p3];

        Vector3 point = CatmullRom(P0, P1, P2, P3, localT);
        return transform.TransformPoint(point);
    }

    public Vector3 GetTangent(float t)
    {
        float delta = 0.001f;
        Vector3 a = GetPoint(Mathf.Clamp01(t - delta));
        Vector3 b = GetPoint(Mathf.Clamp01(t + delta));
        return (b - a).normalized;
    }

    int WrapIndex(int i)
    {
        if (controlPoints.Count == 0) return 0;
        if (loop)
        {
            i = (i % controlPoints.Count + controlPoints.Count) % controlPoints.Count;
        }
        else
        {
            i = Mathf.Clamp(i, 0, controlPoints.Count - 1);
        }
        return i;
    }

    static Vector3 CatmullRom(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, float t)
    {
        // standard Catmull-Rom spline
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * P1) +
            (-P0 + P2) * t +
            (2f * P0 - 5f * P1 + 4f * P2 - P3) * t2 +
            (-P0 + 3f * P1 - 3f * P2 + P3) * t3);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        if (controlPoints == null) return;
        for (int i = 0; i < controlPoints.Count; i++)
        {
            Vector3 world = transform.TransformPoint(controlPoints[i]);
            Gizmos.DrawSphere(world, handleSize * HandleUtilityGetSize(world));
            // draw small lines showing tangent
            Vector3 t = GetTangent(i / (float)Mathf.Max(1, controlPoints.Count - 1));
            Gizmos.DrawLine(world, world + t * (handleSize * 2f));
        }

        // draw spline
        int steps = Mathf.Max(8, controlPoints.Count * 10);
        Vector3 prev = GetPoint(0);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = GetPoint(t);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    float HandleUtilityGetSize(Vector3 worldPos)
    {
        Camera cam = UnityEditor.SceneView.lastActiveSceneView?.camera;
        if (cam == null) return 1f;
        float dist = Vector3.Distance(cam.transform.position, worldPos);
        return Mathf.Max(0.02f, dist * 0.02f);
    }
#endif
}
