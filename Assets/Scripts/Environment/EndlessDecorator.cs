using UnityEngine;
using System.Collections.Generic;

public class EndlessDecorator : MonoBehaviour
{
    public SurfaceDecorationTool decorationTool;
    public EndlessLevelGenerator levelGenerator;

    private int lastDecoratedSegmentIndex = -1;

    void Update()
    {
        int latestSegmentIndex = GetLatestSegmentIndex();
        if (latestSegmentIndex > lastDecoratedSegmentIndex)
        {
            for (int i = lastDecoratedSegmentIndex + 1; i <= latestSegmentIndex; i++)
            {
                DecorateSegment(i);
            }
            lastDecoratedSegmentIndex = latestSegmentIndex;
        }
    }

    int GetLatestSegmentIndex()
    {
        int index = -1;
        foreach (Transform child in levelGenerator.GetGeneratedLevelParent().transform)
        {
            if (child.name.StartsWith("LevelSegment_"))
            {
                int segIndex = int.Parse(child.name.Replace("LevelSegment_", ""));
                index = Mathf.Max(index, segIndex);
            }
        }
        return index;
    }

    void DecorateSegment(int segmentIndex)
    {
        MeshFilter meshFilter = levelGenerator.GetSegmentMeshFilter(segmentIndex);
        if (meshFilter == null) return;

        // Temporarily assign to decoration tool
        MeshFilter original = decorationTool.targetMesh;
        decorationTool.targetMesh = meshFilter;
        decorationTool.GenerateDecorations();
        decorationTool.targetMesh = original; // restore
    }
}