using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelBuilder))]
public class LevelBuilderEditor : Editor
{
    LevelBuilder builder;
    SplineComponent spline;

    void OnEnable()
    {
        builder = (LevelBuilder)target;
        spline = builder.GetComponent<SplineComponent>();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate"))
        {
            Undo.RecordObject(builder, "Generate Level");
            builder.Generate();
            EditorUtility.SetDirty(builder);
        }
        if (GUILayout.Button("Clear"))
        {
            Undo.RecordObject(builder, "Clear Generated");
            builder.Clear();
            EditorUtility.SetDirty(builder);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Auto-Fit Sample Distance (based on total length)"))
        {
            float len = builder ? builder.GetComponent<LevelBuilder>().EstimateSplineLength() : 10f;
            builder.sampleDistance = Mathf.Max(0.25f, len / 50f);
        }
    }

    // scene handles for spline editing
    void OnSceneGUI()
    {
        if (spline == null) return;
        Transform t = spline.transform;
        for (int i = 0; i < spline.controlPoints.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 worldPos = t.TransformPoint(spline.controlPoints[i]);
            Vector3 newWorld = Handles.PositionHandle(worldPos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Move Spline Point");
                spline.controlPoints[i] = t.InverseTransformPoint(newWorld);
                EditorUtility.SetDirty(spline);
            }
        }

        // add/remove points with keyboard shortcuts? Simple GUI:
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 220, 200), "Spline Tools", GUI.skin.window);
        if (GUILayout.Button("Add Point At End"))
        {
            Undo.RecordObject(spline, "Add Spline Point");
            Vector3 last = spline.controlPoints[spline.controlPoints.Count - 1];
            spline.controlPoints.Add(last + Vector3.right * 5f);
            EditorUtility.SetDirty(spline);
        }
        if (GUILayout.Button("Remove Last Point"))
        {
            if (spline.controlPoints.Count > 1)
            {
                Undo.RecordObject(spline, "Remove Spline Point");
                spline.controlPoints.RemoveAt(spline.controlPoints.Count - 1);
                EditorUtility.SetDirty(spline);
            }
        }
        GUILayout.EndArea();
        Handles.EndGUI();
    }
}
