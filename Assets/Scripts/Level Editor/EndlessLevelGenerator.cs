using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Endless Level Generator - Procedurally generates skateable terrain with varied slopes, flat sections, and gaps
/// Uses spline-based level generation to create natural flowing levels
/// 
/// IMPORTANT: Generates only on X-Y plane (Z always = 0) so player can maintain Z=0 position
/// Player moves forward along X axis (world right), Y oscillates up/down for terrain variation
/// </summary>
public class EndlessLevelGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public int segmentsPerSection = 5; // How many spline points per terrain section
    public float sectionSpacing = 20f; // Distance between major terrain sections (along X)
    public int totalSections = 10; // How many sections to generate
    
    [Header("Terrain Variation")]
    public float flatSectionChance = 0.4f; // 40% flat sections
    public float upSlopeChance = 0.3f; // 30% uphill
    public float downSlopeChance = 0.3f; // 30% downhill
    public float maxSlopeHeight = 15f; // Max height difference per slope
    public float minSectionLength = 10f; // Minimum X distance per section (prevents too-steep slopes)
    public float gapChance = 0.15f; // 15% chance of gap in a section
    public float gapLength = 8f; // How long gaps are
    
    [Header("Level Builder Settings")]
    public Material levelMaterial;
    public float width = 6f;
    public float bankFactor = 1f;
    public PhysicMaterial physicsMaterial;
    
    [Header("Random Seed")]
    public int randomSeed = -1; // -1 = use random, otherwise fixed seed for reproducibility
    
    private List<Vector3> allControlPoints = new List<Vector3>();
    private List<List<Vector3>> splineSegments = new List<List<Vector3>>(); // Separate splines for each continuous section
    private GameObject generatedLevelParent;
    private int sectionCount = 0;

    public void GenerateEndlessLevel()
    {
        // Set random seed for reproducibility
        if (randomSeed >= 0)
            Random.InitState(randomSeed);
        
        // Clear previous level
        if (generatedLevelParent != null)
            DestroyImmediate(generatedLevelParent);
        
        allControlPoints.Clear();
        splineSegments.Clear();
        sectionCount = 0;
        
        // Create parent for all generated sections
        generatedLevelParent = new GameObject("GeneratedLevel");
        generatedLevelParent.transform.SetParent(transform);
        
        // Start from your handmade section endpoint, on Z=0 plane
        Vector3 currentPos = new Vector3(139.017395f, 22.5955849f, 0);
        float currentY = currentPos.y;
        float currentX = currentPos.x;
        
        // Generate sections
        for (int i = 0; i < totalSections; i++)
        {
            GenerateTerrainSection(ref currentX, ref currentY);
        }
        
        Debug.Log($"✅ Generated endless level with {sectionCount} sections and {allControlPoints.Count} control points (all on Z=0 plane)");
    }

    private void GenerateTerrainSection(ref float currentX, ref float currentY)
    {
        // Start a new spline segment (in case we need one after a gap)
        if (splineSegments.Count == 0 || splineSegments[splineSegments.Count - 1].Count == 0)
        {
            splineSegments.Add(new List<Vector3>());
        }
        
        List<Vector3> currentSegment = splineSegments[splineSegments.Count - 1];
        
        float rand = Random.value;
        float endX;
        float endY;
        string terrainType = "";
        
        // Determine section type and height change
        if (rand < flatSectionChance)
        {
            // FLAT SECTION - no height change
            endX = currentX + sectionSpacing;
            endY = currentY;
            terrainType = "🏁 FLAT";
        }
        else if (rand < flatSectionChance + upSlopeChance)
        {
            // UPHILL SLOPE - height increases
            float slopeHeight = Random.Range(maxSlopeHeight * 0.5f, maxSlopeHeight);
            endX = currentX + sectionSpacing;
            endY = currentY + slopeHeight;
            terrainType = $"🏔️ UP (+{slopeHeight:F1}m)";
        }
        else
        {
            // DOWNHILL SLOPE - height decreases
            float slopeHeight = Random.Range(maxSlopeHeight * 0.5f, maxSlopeHeight);
            endX = currentX + sectionSpacing;
            endY = currentY - slopeHeight;
            terrainType = $"⬇️ DOWN (-{slopeHeight:F1}m)";
        }
        
        // Generate control points for this section, linearly along X with Y height variation
        // All points stay at Z=0 (the 2D plane the player moves on)
        int pointsToAdd = segmentsPerSection;
        for (int i = 0; i <= pointsToAdd; i++)
        {
            float t = i / (float)pointsToAdd;
            float sectionX = Mathf.Lerp(currentX, endX, t);
            float sectionY = Mathf.Lerp(currentY, endY, t);
            
            // Z is ALWAYS 0 - player moves only on X-Y plane
            Vector3 point = new Vector3(sectionX, sectionY, 0);
            
            // Only add if not already in list (avoid duplicates at section boundaries)
            if (currentSegment.Count == 0 || Vector3.Distance(point, currentSegment[currentSegment.Count - 1]) > 0.1f)
            {
                currentSegment.Add(point);
            }
        }
        
        // Add gap if random chance
        if (Random.value < gapChance)
        {
            // Create a gap by starting a NEW spline segment
            // This breaks continuity and prevents geometry from being generated across the gap
            float gapEndX = endX + gapLength;
            
            // Start fresh segment for terrain AFTER the gap
            splineSegments.Add(new List<Vector3>());
            
            endX = gapEndX;
            Debug.Log($"  ➡️ Added GAP ({gapLength:F1}m) - starting new spline segment");
        }
        
        // Update position for next section
        currentX = endX;
        currentY = endY;
        
        Debug.Log($"Section {sectionCount}: {terrainType} (X: {currentX:F1}, Y: {currentY:F1})");
        sectionCount++;
    }

    public void CreateLevelFromControlPoints()
    {
        if (splineSegments.Count == 0)
        {
            Debug.LogWarning("No spline segments generated!");
            return;
        }
        
        int segmentIndex = 0;
        foreach (var segment in splineSegments)
        {
            if (segment.Count < 2)
            {
                Debug.LogWarning($"Segment {segmentIndex} has fewer than 2 points, skipping...");
                segmentIndex++;
                continue;
            }
            
            // Create a spline component for this segment
            GameObject splineGO = new GameObject($"LevelSegment_{segmentIndex}");
            splineGO.transform.SetParent(generatedLevelParent.transform);
            
            SplineComponent spline = splineGO.AddComponent<SplineComponent>();
            spline.controlPoints = new List<Vector3>(segment);
            spline.loop = false;
            
            // Create a level builder for this segment
            LevelBuilder builder = splineGO.AddComponent<LevelBuilder>();
            builder.sampleDistance = 1f;
            builder.simplifyTolerance = 0.1f;
            builder.alignToWorldRight = false; // Don't rotate - we're already aligned
            builder.width = width;
            builder.bankFactor = bankFactor;
            builder.colliderMode = LevelBuilder.ColliderMode.BoxSegments;
            builder.material = levelMaterial;
            if (physicsMaterial != null)
                builder.physicsMaterial = physicsMaterial;
            
            // Generate the level for this segment
            builder.Generate();
            
            Debug.Log($"✅ Generated level segment {segmentIndex} with {segment.Count} control points");
            segmentIndex++;
        }
        
        Debug.Log($"✅ Level created from {splineSegments.Count} separate spline segments (gaps create breaks)!");
    }

    public void GenerateAndCreate()
    {
        GenerateEndlessLevel();
        CreateLevelFromControlPoints();
    }

    // Preview method to show control points in editor
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (allControlPoints.Count < 2) return;
        
        Gizmos.color = Color.green;
        for (int i = 0; i < allControlPoints.Count; i++)
        {
            Gizmos.DrawSphere(allControlPoints[i], 0.5f);
            if (i < allControlPoints.Count - 1)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(allControlPoints[i], allControlPoints[i + 1]);
                
                // Draw Z=0 reference line
                if (i % 5 == 0)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(allControlPoints[i] - Vector3.forward * 5f, allControlPoints[i] + Vector3.forward * 5f);
                    Gizmos.color = Color.green;
                }
            }
        }
        
        // Draw reference plane
        Gizmos.color = new Color(0, 0, 1, 0.3f);
        if (allControlPoints.Count > 1)
        {
            float minX = allControlPoints[0].x;
            float maxX = allControlPoints[allControlPoints.Count - 1].x;
            float minY = allControlPoints[0].y;
            float maxY = allControlPoints[0].y;
            
            foreach (var point in allControlPoints)
            {
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }
            
            // Draw corners of Z=0 plane
            Gizmos.color = new Color(0, 0, 1, 0.5f);
            Gizmos.DrawLine(new Vector3(minX, minY, 0), new Vector3(maxX, minY, 0));
            Gizmos.DrawLine(new Vector3(minX, maxY, 0), new Vector3(maxX, maxY, 0));
        }
    }
#endif
}
