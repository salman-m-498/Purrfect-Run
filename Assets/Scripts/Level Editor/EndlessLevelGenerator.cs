using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

/// <summary>
/// Endless Level Generator - Procedurally generates skateable terrain with grind rails, decorations, and proper layer assignments
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
    
    [Header("Grind Rail Settings")]
    public Material grindRailMaterial;
    public float grindRailRadius = 0.3f;
    public int grindRailSegments = 8;
    public PhysicMaterial grindRailPhysicsMaterial;

    [Header("Lava River under rails")]
    public Material lavaMaterial;

    [Header("Decoration Settings")]
    public Material[] treeMaterials;
    public Material[] rockMaterials;
    public float decorationDensity = 0.5f; // How many decorations per unit length
    public float decorationOffset = 4f; // How far from track center
    public float decorationHeightVariation = 2f;
    public float decorationMinSize = 0.8f; // Minimum decoration scale
    public float decorationMaxSize = 1.5f; // Maximum decoration scale
    
    [Header("Layer Settings")]
    public string groundLayerName = "Ground";
    public string grindableLayerName = "Grindable";
    public string decorationLayerName = "Decoration";

    [Header("Player Spawn")]
    public bool createPlayerSpawn = true;   // set false if you prefer manual spawn
    public string spawnMarkerName = "EndlessStart";
    
    [Header("Random Seed")]
    public int randomSeed = -1; // -1 = use random, otherwise fixed seed for reproducibility
    
    private List<Vector3> allControlPoints = new List<Vector3>();
    private List<List<Vector3>> splineSegments = new List<List<Vector3>>(); // Separate splines for each continuous section
    private List<GapInfo> gaps = new List<GapInfo>();
    private GameObject generatedLevelParent;
    private int sectionCount = 0;
    private int groundLayer;
    private int grindableLayer;
    private int decorationLayer;
    private float currentX = 0f; // Track current position for incremental generation
    private float currentY = 22f; // Track current height for incremental generation

    private class GapInfo
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float length;
    }

    /// <summary>
    /// Main entry point for initial full level generation
    /// </summary>
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
        gaps.Clear();
        sectionCount = 0;
        
        // Get layer IDs
        groundLayer = LayerMask.NameToLayer(groundLayerName);
        grindableLayer = LayerMask.NameToLayer(grindableLayerName);
        decorationLayer = LayerMask.NameToLayer(decorationLayerName);
        
        // Create parent for all generated sections
        generatedLevelParent = new GameObject("GeneratedLevel");
        generatedLevelParent.transform.SetParent(transform);
        
        // Start from your handmade section endpoint, on Z=0 plane
        Vector3 currentPos = new Vector3(139.017395f, 22.5955849f, 0);
        currentY = currentPos.y;
        currentX = currentPos.x;
        
        // Create initial spline segment with starting point
        List<Vector3> initialSegment = new List<Vector3>();
        initialSegment.Add(currentPos);  // Add starting point
        splineSegments.Add(initialSegment);
        
        // Generate sections
        for (int i = 0; i < totalSections; i++)
        {
            GenerateTerrainSection(ref currentX, ref currentY, true); // true = is initial generation
        }
        
        // Build all generated segments
        BuildNewSegments(0);

        
        // Generate grind rails for gaps
        GenerateGrindRails();
        
        // Generate decorations
        GenerateDecorations();
        
        Debug.Log($"✅ Generated enhanced endless level with {sectionCount} sections, {gaps.Count} gaps, and {allControlPoints.Count} control points (all on Z=0 plane)");
    }

    /// <summary>
    /// Generates a single terrain section - handles both initial and incremental generation
    /// </summary>
    /// <param name="currentX">Current X position (ref)</param>
    /// <param name="currentY">Current Y position (ref)</param>
    /// <param name="isInitialGeneration">Whether this is part of the initial full generation</param>
    private void GenerateTerrainSection(ref float currentX, ref float currentY, bool isInitialGeneration = false)
    {
        List<Vector3> currentSegment;
        // Ensure there is at least one spline segment to operate on
        if (splineSegments.Count == 0)
        {
            splineSegments.Add(new List<Vector3>());
        }
        currentSegment = splineSegments[splineSegments.Count - 1];
        
        // For incremental generation, ensure we have a valid starting point
        if (!isInitialGeneration && currentSegment.Count == 0)
        {
            // This should never happen with proper initialization, but add safety
            currentSegment.Add(new Vector3(currentX, currentY, 0));
        }
        
        // Determine section type and height change
        float rand = Random.value;
        float endX, endY;
        string terrainType = "";

        if (rand < flatSectionChance)
        {
            endX = currentX + sectionSpacing;
            endY = currentY;
            terrainType = "🛤️ FLAT";
        }
        else if (rand < flatSectionChance + upSlopeChance)
        {
            float slopeHeight = Random.Range(maxSlopeHeight * 0.5f, maxSlopeHeight);
            endX = currentX + sectionSpacing;
            endY = currentY + slopeHeight;
            terrainType = $"🔺 UP (+{slopeHeight:F1}m)";
        }
        else
        {
            float slopeHeight = Random.Range(maxSlopeHeight * 0.5f, maxSlopeHeight);
            endX = currentX + sectionSpacing;
            endY = currentY - slopeHeight;
            terrainType = $"⬇️ DOWN (-{slopeHeight:F1}m)";
        }

        // Generate control points for this section
        int pointsToAdd = segmentsPerSection;
        for (int i = 1; i <= pointsToAdd; i++)  // Start at 1 to avoid duplicate first point
        {
            float t = i / (float)pointsToAdd;
            float sectionX = Mathf.Lerp(currentX, endX, t);
            float sectionY = Mathf.Lerp(currentY, endY, t);
            
            Vector3 point = new Vector3(sectionX, sectionY, 0);
            
            // Only add if not already in list (avoid duplicates at section boundaries)
            if (currentSegment.Count == 0 || Vector3.Distance(point, currentSegment[currentSegment.Count - 1]) > 0.1f)
            {
                currentSegment.Add(point);
                allControlPoints.Add(point);
            }
        }

        if (Random.value < gapChance)
            {
                float gapEndX = endX + gapLength;

                // Save gap for grind rail
                gaps.Add(new GapInfo
                {
                    startPoint = new Vector3(endX, endY, 0),
                    endPoint = new Vector3(gapEndX, endY, 0),
                    length = gapLength
                });

                // Create the new spline segment WITH starting point immediately
                List<Vector3> newSeg = new List<Vector3>();
                newSeg.Add(new Vector3(gapEndX, endY, 0));  // Add starting point BEFORE adding to list
                splineSegments.Add(newSeg);

                endX = gapEndX;
                currentX = endX;
                currentY = endY;

                Debug.Log($"➡️ GAP added. New segment begins at ({endX},{endY})");
                return; // prevent adding more points to the OLD segment
            }

        // Update position for next section
        currentX = endX;
        currentY = endY;
        
        Debug.Log($"Section {sectionCount}: {terrainType} (X: {currentX:F1}, Y: {currentY:F1})");
        sectionCount++;
    }

    /// <summary>
    /// Generates additional sections incrementally - for endless gameplay
    /// This does NOT clear existing level - it adds to it
    /// </summary>
    public void GenerateAdditionalSections(int numSections)
    {
        if (generatedLevelParent == null)
        {
            Debug.LogError("GeneratedLevel parent not found! Call GenerateEndlessLevel first.");
            return;
        }

        // Get layers if not already set
        if (groundLayer == 0)
            groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (grindableLayer == 0)
            grindableLayer = LayerMask.NameToLayer(grindableLayerName);
        if (decorationLayer == 0)
            decorationLayer = LayerMask.NameToLayer(decorationLayerName);

        // Find the rightmost point from existing geometry to continue from
        LevelBuilder[] existingSegments = generatedLevelParent.GetComponentsInChildren<LevelBuilder>();
        float maxX = 139.017395f; // Default start
        float lastY = 22.5955849f;
        
        foreach (var builder in existingSegments)
        {
            SplineComponent spline = builder.GetComponent<SplineComponent>();
            if (spline != null && spline.controlPoints.Count > 0)
            {
                Vector3 lastPoint = spline.controlPoints[spline.controlPoints.Count - 1];
                if (lastPoint.x > maxX)
                {
                    maxX = lastPoint.x;
                    lastY = lastPoint.y;
                }
            }
        }
        
        currentX = maxX;
        currentY = lastY;
        
        // Ensure we have a valid current segment to continue from
        if (splineSegments.Count == 0 || splineSegments[splineSegments.Count - 1].Count == 0)
        {
            // Create new segment WITH starting point
            List<Vector3> newSeg = new List<Vector3>();
            newSeg.Add(new Vector3(currentX, currentY, 0));
            splineSegments.Add(newSeg);
            
            Debug.Log($"🔧 Created new segment starting at: X={currentX:F1}, Y={currentY:F1}");
        }
        else
        {
            // CRITICAL: Continue the existing last segment by adding a starting point if needed
            List<Vector3> lastSegment = splineSegments[splineSegments.Count - 1];
            Vector3 lastSegmentEnd = lastSegment[lastSegment.Count - 1];
            
            // Only create new segment if there's actually a gap (like after a gap/rail)
            // Otherwise, continue the existing segment
            float distanceFromLastPoint = Vector3.Distance(lastSegmentEnd, new Vector3(currentX, currentY, 0));
            
            if (distanceFromLastPoint > 0.5f)
            {
                // There's a gap - create new segment
                List<Vector3> newSeg = new List<Vector3>();
                newSeg.Add(new Vector3(currentX, currentY, 0));
                splineSegments.Add(newSeg);
                Debug.Log($"🔧 Gap detected ({distanceFromLastPoint:F1}m) - created new segment at: X={currentX:F1}, Y={currentY:F1}");
            }
            else
            {
                // Continue existing segment
                currentX = lastSegmentEnd.x;
                currentY = lastSegmentEnd.y;
                Debug.Log($"✅ Continuing existing segment from: X={currentX:F1}, Y={currentY:F1}");
            }
        }

        int startSegmentIndex = sectionCount;
        int startSplineSegmentIndex = splineSegments.Count - 1; // Start from current segment, not next one
        
        // Generate more terrain sections
        for (int i = 0; i < numSections; i++)
        {
            GenerateTerrainSection(ref currentX, ref currentY, false); // false = incremental generation
        }

        // Build level geometry for new segments
        // If we continued an existing segment, we need to rebuild it
        bool continuedExistingSegment = (startSplineSegmentIndex == splineSegments.Count - 1 && 
                                         splineSegments[startSplineSegmentIndex].Count > 1);
        
        if (continuedExistingSegment)
        {
            // Rebuild the segment we continued
            RebuildSegment(startSplineSegmentIndex);
            // Build any new segments created after it
            BuildNewSegments(startSplineSegmentIndex + 1);
        }
        else
        {
            // Build all new segments
            BuildNewSegments(startSplineSegmentIndex);
        }
        
        // Generate grind rails for any new gaps
        BuildNewGrindRails();
        
        // Generate decorations for new segments
        BuildNewDecorations(startSplineSegmentIndex);
        
        Debug.Log($"✅ Added {numSections} new sections. Total segments: {splineSegments.Count}, CurrentX: {currentX:F1}");
    }

    /// <summary>
    /// Rebuilds a specific segment that was extended with new points.
    /// If the GameObject is missing, recreates it as a fallback.
    /// </summary>
    private void RebuildSegment(int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= splineSegments.Count)
        {
            Debug.LogWarning($"Cannot rebuild segment {segmentIndex} - index out of range");
            return;
        }

        var segment = splineSegments[segmentIndex];
        if (segment.Count < 2)
        {
            Debug.LogWarning($"Segment {segmentIndex} has fewer than 2 points, skipping rebuild");
            return;
        }

        // Try to find existing GameObject
        GameObject existingSegmentGO = null;
        foreach (Transform child in generatedLevelParent.transform)
        {
            if (child.name == $"LevelSegment_{segmentIndex}")
            {
                existingSegmentGO = child.gameObject;
                break;
            }
        }

        // ✅ FALLBACK: Recreate missing segment
        if (existingSegmentGO == null)
        {
            Debug.LogWarning($"🚨 Segment {segmentIndex} GameObject missing! Recreating...");

            existingSegmentGO = new GameObject($"LevelSegment_{segmentIndex}");
            existingSegmentGO.transform.SetParent(generatedLevelParent.transform);
            existingSegmentGO.layer = groundLayer;

            // Add SplineComponent
            SplineComponent spline = existingSegmentGO.AddComponent<SplineComponent>();
            spline.controlPoints = new List<Vector3>(segment);
            spline.loop = false;

            // Add LevelBuilder
            LevelBuilder builder = existingSegmentGO.AddComponent<LevelBuilder>();
            builder.sampleDistance = 1f;
            builder.simplifyTolerance = 0.1f;
            builder.alignToWorldRight = false;
            builder.width = width;
            builder.bankFactor = bankFactor;
            builder.colliderMode = LevelBuilder.ColliderMode.BoxSegments;
            builder.material = levelMaterial;
            if (physicsMaterial != null)
                builder.physicsMaterial = physicsMaterial;

            // Generate geometry
            builder.Generate();
            SetLayerRecursive(existingSegmentGO, groundLayer);

            Debug.Log($"✅ Fallback: Recreated segment {segmentIndex} with {segment.Count} points");
            return;
        }

        // ✅ Normal rebuild path
        SplineComponent existingSpline = existingSegmentGO.GetComponent<SplineComponent>();
        if (existingSpline != null)
        {
            existingSpline.controlPoints = new List<Vector3>(segment);
        }

        LevelBuilder existingBuilder = existingSegmentGO.GetComponent<LevelBuilder>();
        if (existingBuilder != null)
        {
            existingBuilder.Generate();
            Debug.Log($"🔄 Rebuilt segment {segmentIndex} with {segment.Count} control points");
        }
    }

    /// <summary>
    /// Builds only the new segments that were just generated
    /// </summary>
    private void BuildNewSegments(int startSplineIndex)
    {
        // Count existing LevelSegment_X children to get the right index for new ones
        int existingSegmentCount = 0;
        foreach (Transform child in generatedLevelParent.transform)
        {
            if (child.name.StartsWith("LevelSegment_"))
                existingSegmentCount++;
        }

        for (int i = startSplineIndex; i < splineSegments.Count; i++)
        {
            var segment = splineSegments[i];
            
            if (segment.Count < 2)
            {
                Debug.LogWarning($"Segment {i} has fewer than 2 points, skipping...");
                continue;
            }
            
            int segmentGameObjectIndex = existingSegmentCount + (i - startSplineIndex);
            
            // Create a spline component for this segment
            GameObject splineGO = new GameObject($"LevelSegment_{segmentGameObjectIndex}");
            splineGO.transform.SetParent(generatedLevelParent.transform);
            splineGO.layer = groundLayer;
            
            SplineComponent spline = splineGO.AddComponent<SplineComponent>();
            spline.controlPoints = new List<Vector3>(segment);
            spline.loop = false;

            // -----------------------------------------------------------------
            // CREATE PLAYER SPAWN (only on the very first segment)
            // -----------------------------------------------------------------
            if (createPlayerSpawn && segmentGameObjectIndex == 0)
            {
                GameObject spawnGO = new GameObject(spawnMarkerName);
                spawnGO.transform.SetParent(splineGO.transform);

                // place it exactly at the first spline point
                spawnGO.transform.position = segment[0] + new Vector3(10f,10f,0f);
                // rotate so forward faces the start of the level
                if (segment.Count > 1)
                    spawnGO.transform.forward = (segment[1] - segment[0]).normalized;

                spawnGO.tag = "PlayerSpawn";
            }
            
            // Create a level builder for this segment
            LevelBuilder builder = splineGO.AddComponent<LevelBuilder>();
            builder.sampleDistance = 1f;
            builder.simplifyTolerance = 0.1f;
            builder.alignToWorldRight = false;
            builder.width = width;
            builder.bankFactor = bankFactor;
            builder.colliderMode = LevelBuilder.ColliderMode.BoxSegments;
            builder.material = levelMaterial;
            if (physicsMaterial != null)
                builder.physicsMaterial = physicsMaterial;
            
            // Generate the level for this segment
            builder.Generate();
            
            // Set layer for all generated meshes
            SetLayerRecursive(splineGO, groundLayer);
            
            Debug.Log($"✅ Generated level segment {segmentGameObjectIndex} with {segment.Count} control points");
        }
    }

    /// <summary>
    /// Builds grind rails only for new gaps
    /// </summary>
    private void BuildNewGrindRails()
    {
        GameObject grindRailsParent = GameObject.Find("GeneratedLevel/GrindRails");
        if (grindRailsParent == null)
        {
            grindRailsParent = new GameObject("GrindRails");
            grindRailsParent.transform.SetParent(generatedLevelParent.transform);
        }
        
        // Find gaps that don't have grind rails yet
        foreach (var gap in gaps)
        {
            string expectedName = $"GrindRail_{gap.startPoint.x:F0}_{gap.endPoint.x:F0}";
            if (GameObject.Find(expectedName) == null)
            {
                CreateGrindRail(gap, grindRailsParent.transform);
            }
        }
    }

    /// <summary>
    /// Builds decorations only for new segments
    /// </summary>
    private void BuildNewDecorations(int startSplineIndex)
    {
        GameObject decorationsParent = GameObject.Find("GeneratedLevel/Decorations");
        if (decorationsParent == null)
        {
            decorationsParent = new GameObject("Decorations");
            decorationsParent.transform.SetParent(generatedLevelParent.transform);
            decorationsParent.layer = decorationLayer;
        }
        
        for (int i = startSplineIndex; i < splineSegments.Count; i++)
        {
            if (splineSegments[i].Count >= 2)
            {
                GenerateSegmentDecorations(splineSegments[i], decorationsParent.transform);
            }
        }
    }

    private void GenerateGrindRails()
    {
        if (gaps.Count == 0)
        {
            Debug.Log("No gaps found - no grind rails needed");
            return;
        }
        
        GameObject grindRailsParent = new GameObject("GrindRails");
        grindRailsParent.transform.SetParent(generatedLevelParent.transform);
        
        foreach (var gap in gaps)
        {
            CreateGrindRail(gap, grindRailsParent.transform);
        }
        
        Debug.Log($"✅ Generated {gaps.Count} grind rails");
    }

    private void CreateGrindRail(GapInfo gap, Transform parent)
    {
        // Create grind rail GameObject
        GameObject grindRail = new GameObject($"GrindRail_{gap.startPoint.x:F0}_{gap.endPoint.x:F0}");
        grindRail.transform.SetParent(parent);
        grindRail.transform.position = gap.startPoint;
        grindRail.layer = grindableLayer;
        
        // Calculate direction and length
        Vector3 direction = gap.endPoint - gap.startPoint;
        float length = direction.magnitude;
        direction.Normalize();
        
        // Create cylinder mesh
        MeshFilter meshFilter = grindRail.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = grindRail.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = grindRail.AddComponent<MeshCollider>();
        meshCollider.convex = true;
        
        // Generate cylinder mesh
        Mesh cylinderMesh = GenerateCylinderMesh(grindRailRadius, length, grindRailSegments);
        meshFilter.mesh = cylinderMesh;
        meshRenderer.material = grindRailMaterial;
        meshCollider.sharedMesh = cylinderMesh;
        
        if (grindRailPhysicsMaterial != null)
        {
            meshCollider.material = grindRailPhysicsMaterial;
        }
        
        // Align cylinder with gap direction
        grindRail.transform.rotation = Quaternion.Euler(0,0,90);
        
        // Position at center of gap
        grindRail.transform.position = gap.startPoint + direction * (length * 0.5f) + new Vector3(0, 0.75f, 0); // Slightly above ground
        
        Debug.Log($"✅ Created grind rail: {length:F1}m long from {gap.startPoint} to {gap.endPoint}");
        CreateLava(gap, grindRail.transform);
    }

    private Mesh GenerateCylinderMesh(float radius, float length, int segments)
    {
        Mesh mesh = new Mesh();
        
        int verticesPerCap = segments + 1;
        int totalVertices = verticesPerCap * 2;
        
        Vector3[] vertices = new Vector3[totalVertices];
        Vector2[] uvs = new Vector2[totalVertices];
        int[] triangles = new int[segments * 6 * 2]; // Top, bottom, and side triangles
        
        // Generate vertices
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            
            // Bottom vertices
            vertices[i] = new Vector3(x, -length * 0.5f, z);
            uvs[i] = new Vector2(i / (float)segments, 0f);
            
            // Top vertices
            vertices[verticesPerCap + i] = new Vector3(x, length * 0.5f, z);
            uvs[verticesPerCap + i] = new Vector2(i / (float)segments, 1f);
        }
        
        // Generate triangles for sides
        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            int current     = i;
            int next        = (i + 1) % verticesPerCap;
            int currentTop  = verticesPerCap + current;
            int nextTop     = verticesPerCap + next;

            // first quad triangle
            triangles[triIndex++] = current;
            triangles[triIndex++] = currentTop;   // <- swapped
            triangles[triIndex++] = next;         // <- swapped

            // second quad triangle
            triangles[triIndex++] = currentTop;
            triangles[triIndex++] = nextTop;
            triangles[triIndex++] = next;         // <- swapped
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        return mesh;
    }

    private void CreateLava(GapInfo gap, Transform parent)
    {
        Vector3 centre = (gap.startPoint + gap.endPoint) * 0.5f;
        Vector3 dir    = (gap.endPoint - gap.startPoint).normalized;

        GameObject lavaGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
        lavaGO.name       = $"Lava_{gap.startPoint.x:F0}_{gap.endPoint.x:F0}";
        lavaGO.transform.SetParent(parent, false);

        // 1. position
        lavaGO.transform.position = centre + Vector3.down * 0.3f;

        // 2. rotation: align local-Z with the gap direction
        lavaGO.transform.rotation = Quaternion.LookRotation(dir, Vector3.up)
                                * Quaternion.Euler(0f, 90f, 0f);  // lay it flat in XY plane

        // 3. scale: length × 2-length (Plane default is 10×10, so normalize first)
        float scaleX = gap.length / 10f;   // localScale.x becomes length
        float scaleZ = gap.length * 2f / 10f; // localScale.z becomes 2×length
        lavaGO.transform.localScale = new Vector3(scaleX, 1f, scaleZ);

        // 4. material
        lavaGO.GetComponent<MeshRenderer>().sharedMaterial = lavaMaterial;

        // 5. collider trigger needed
        Destroy(lavaGO.GetComponent<MeshCollider>());
        lavaGO.AddComponent<BoxCollider>();
        lavaGO.GetComponent<BoxCollider>().isTrigger = true;
        lavaGO.AddComponent<Lava>();
    }


    private void GenerateDecorations()
    {
        GameObject decorationsParent = new GameObject("Decorations");
        decorationsParent.transform.SetParent(generatedLevelParent.transform);
        decorationsParent.layer = decorationLayer;
        
        foreach (var segment in splineSegments)
        {
            if (segment.Count < 2) continue;
            
            GenerateSegmentDecorations(segment, decorationsParent.transform);
        }
    }

    private void GenerateSegmentDecorations(List<Vector3> segment, Transform parent)
    {
        float segmentLength = 0f;
        for (int i = 1; i < segment.Count; i++)
        {
            segmentLength += Vector3.Distance(segment[i], segment[i - 1]);
        }
        
        int decorationCount = Mathf.FloorToInt(segmentLength * decorationDensity);
        
        for (int i = 0; i < decorationCount; i++)
        {
            float t = i / (float)decorationCount;
            Vector3 position = GetPointOnSegment(segment, t);
            
            // For a spline traveling along X-axis in X-Y plane (Z=0)
            // The perpendicular direction for left/right placement is along the Z-axis
            // We use Vector3.forward and Vector3.back for left/right offset
            
            // Left side decoration (negative Z direction)
            Vector3 leftPos = position + Vector3.back * decorationOffset;
            leftPos.y += Random.Range(-decorationHeightVariation, decorationHeightVariation);
            CreateDecoration(leftPos, parent, true);
            
            // Right side decoration (positive Z direction)
            Vector3 rightPos = position + Vector3.forward * decorationOffset;
            rightPos.y += Random.Range(-decorationHeightVariation, decorationHeightVariation);
            CreateDecoration(rightPos, parent, false);
        }
    }

    private Vector3 GetPointOnSegment(List<Vector3> segment, float t)
    {
        if (segment.Count < 2) return segment[0];
        
        float totalLength = 0f;
        for (int i = 1; i < segment.Count; i++)
        {
            totalLength += Vector3.Distance(segment[i], segment[i - 1]);
        }
        
        float targetLength = t * totalLength;
        float currentLength = 0f;
        
        for (int i = 1; i < segment.Count; i++)
        {
            float segmentLength = Vector3.Distance(segment[i], segment[i - 1]);
            if (currentLength + segmentLength >= targetLength)
            {
                float localT = (targetLength - currentLength) / segmentLength;
                return Vector3.Lerp(segment[i - 1], segment[i], localT);
            }
            currentLength += segmentLength;
        }
        
        return segment[segment.Count - 1];
    }

    private Vector3 GetTangentOnSegment(List<Vector3> segment, float t)
    {
        if (segment.Count < 2) return Vector3.right;
        
        float totalLength = 0f;
        for (int i = 1; i < segment.Count; i++)
        {
            totalLength += Vector3.Distance(segment[i], segment[i - 1]);
        }
        
        float targetLength = t * totalLength;
        float currentLength = 0f;
        
        for (int i = 1; i < segment.Count; i++)
        {
            float segmentLength = Vector3.Distance(segment[i], segment[i - 1]);
            if (currentLength + segmentLength >= targetLength)
            {
                return (segment[i] - segment[i - 1]).normalized;
            }
            currentLength += segmentLength;
        }
        
        return (segment[segment.Count - 1] - segment[segment.Count - 2]).normalized;
    }

    private void CreateDecoration(Vector3 position, Transform parent, bool isLeftSide)
    {
        GameObject decoration = new GameObject($"Decoration_{position.x:F0}_{(isLeftSide ? "L" : "R")}");
        decoration.transform.SetParent(parent);
        decoration.layer = decorationLayer;
        
        // Create quad mesh for billboard
        MeshFilter meshFilter = decoration.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = decoration.AddComponent<MeshRenderer>();
        
        // Random scale variation using the exposed min/max values
        float scale = Random.Range(decorationMinSize, decorationMaxSize);
        
        Mesh quadMesh = GenerateQuadMesh(scale);
        meshFilter.mesh = quadMesh;
        
        // Position: place at terrain height
        // The quad mesh goes from 0 to 2*scale in Y, so its center is at scale
        // We want the bottom (y=0 of mesh) to be at the terrain position
        // So we don't need to offset - just place it directly at the terrain position
        decoration.transform.position = position;
        
        // Randomly choose tree or rock
        Material[] materials = Random.value > 0.5f ? treeMaterials : rockMaterials;
        if (materials.Length > 0)
        {
            meshRenderer.material = materials[Random.Range(0, materials.Length)];
        }
        
        // Make billboard
        BillboardManager.Register(decoration.transform);
    }

    private Mesh GenerateQuadMesh(float scale = 1f)
    {
        Mesh mesh = new Mesh();
        
        // Quad from bottom (0) to top (2*scale)
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-scale, 0, 0),
            new Vector3(scale, 0, 0),
            new Vector3(-scale, 2*scale, 0),
            new Vector3(scale, 2*scale, 0)
        };
        
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        
        int[] triangles = new int[6]
        {
            0, 2, 1,
            2, 3, 1
        };
        
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        return mesh;
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    public MeshFilter GetSegmentMeshFilter(int segmentIndex)
{
    string segmentName = $"LevelSegment_{segmentIndex}";
    Transform segment = generatedLevelParent.transform.Find(segmentName);
    if (segment != null)
    {
        return segment.GetComponentInChildren<MeshFilter>();
    }
    return null;
}

    /// <summary>
    /// Get the current forward progress position (for manager to track)
    /// </summary>
    public float GetCurrentGenerationX()
    {
        return currentX;
    }
    
    /// <summary>
    /// Get the parent object for generated level sections
    /// </summary>
    public GameObject GetGeneratedLevelParent()
    {
        return generatedLevelParent;
    }

    /// <summary>
    /// Sync cursor to last-built spline end before generating more
    /// </summary>
    public void SyncCursorToRightmostPoint()
    {
        if (generatedLevelParent == null) return;

        float maxX = 139.017395f;
        float lastY = 22.5955849f;

        foreach (Transform child in generatedLevelParent.transform)
        {
            SplineComponent spline = child.GetComponent<SplineComponent>();
            if (spline != null && spline.controlPoints.Count > 0)
            {
                Vector3 last = spline.controlPoints[spline.controlPoints.Count - 1];
                if (last.x > maxX)
                {
                    maxX = last.x;
                    lastY = last.y;
                }
            }
        }

        currentX = maxX;
        currentY = lastY;

        if (splineSegments.Count == 0 ||
            splineSegments[splineSegments.Count - 1].Count == 0)
        {
            List<Vector3> newSeg = new List<Vector3>();
            newSeg.Add(new Vector3(currentX, currentY, 0));  // Add starting point immediately
            splineSegments.Add(newSeg);
        }

        Debug.Log($"[EndlessLevelGenerator] cursor synced to  X={currentX:F2}  Y={currentY:F2}");
    }

    /// <summary>
    /// Cleans up segments that the player has completely passed
    /// Much simpler - just delete segments behind the player
    /// </summary>
    public List<GameObject> CleanupPassedSegments(float playerX)
    {
        if (generatedLevelParent == null)
        {
            return new List<GameObject>();
        }
        
        List<GameObject> destroyedObjects = new List<GameObject>();
        List<Transform> childrenToDestroy = new List<Transform>();
        
        // Find all LevelSegment children and check if player has passed them
        foreach (Transform child in generatedLevelParent.transform)
        {
            if (!child.name.StartsWith("LevelSegment_")) continue;
            
            SplineComponent spline = child.GetComponent<SplineComponent>();
            if (spline != null && spline.controlPoints.Count > 0)
            {
                float segmentStartX = spline.controlPoints[0].x;
                float segmentEndX = spline.controlPoints[spline.controlPoints.Count - 1].x;
                
                // Delete segment if player is safely past it (with buffer)
                float cleanupBuffer = 20f; // Adjust this based on your camera/view distance
                if (playerX > segmentEndX + cleanupBuffer)
                {
                    Debug.Log($"🗑️ Player (X={playerX:F1}) passed segment {child.name} (endX={segmentEndX:F1}) - DESTROYING");
                    childrenToDestroy.Add(child);
                    destroyedObjects.Add(child.gameObject);
                }
            }
        }
        
        // Destroy the GameObjects
        foreach (Transform child in childrenToDestroy)
        {
            DestroyImmediate(child.gameObject);
        }
        
        // Refresh the active sections list and rebuild tracking
        if (destroyedObjects.Count > 0)
        {
            RebuildSplineSegmentsList();
            Debug.Log($"✅ Cleaned up {destroyedObjects.Count} passed segments. Remaining: {splineSegments.Count}");
        }
        
        return destroyedObjects;
    }
    
    /// <summary>
    /// Rebuilds the splineSegments list from existing GameObjects in the scene
    /// Ensures internal tracking matches scene reality
    /// </summary>
    private void RebuildSplineSegmentsList()
    {
        if (generatedLevelParent == null) return;
        
        splineSegments.Clear();
        
        // Collect all LevelSegment children in order
        List<GameObject> segments = new List<GameObject>();
        foreach (Transform child in generatedLevelParent.transform)
        {
            if (child.name.StartsWith("LevelSegment_"))
            {
                segments.Add(child.gameObject);
            }
        }
        
        // Sort by segment number
        segments.Sort((a, b) => 
        {
            int numA = int.Parse(a.name.Replace("LevelSegment_", ""));
            int numB = int.Parse(b.name.Replace("LevelSegment_", ""));
            return numA.CompareTo(numB);
        });
        
        // Rebuild splineSegments from existing GameObjects
        foreach (GameObject segmentObj in segments)
        {
            SplineComponent spline = segmentObj.GetComponent<SplineComponent>();
            if (spline != null && spline.controlPoints.Count > 0)
            {
                splineSegments.Add(new List<Vector3>(spline.controlPoints));
            }
        }
        
        Debug.Log($"🔄 Rebuilt splineSegments list: {splineSegments.Count} segments from {segments.Count} GameObjects");
    }

    /// <summary>
    /// Removes old segments from tracking when they're destroyed by the game manager
    /// DEPRECATED - Use CleanupOldSegments instead which handles everything
    /// </summary>
    public void RemoveSegmentFromTracking(int segmentIndex)
    {
        if (segmentIndex >= 0 && segmentIndex < splineSegments.Count)
        {
            // Remove the spline segment from our tracking
            List<Vector3> removedSegment = splineSegments[segmentIndex];
            
            // Remove all points from this segment from allControlPoints
            foreach (Vector3 point in removedSegment)
            {
                allControlPoints.Remove(point);
            }
            
            splineSegments.RemoveAt(segmentIndex);
            
            Debug.Log($"🗑️ Removed segment {segmentIndex} from tracking. Remaining segments: {splineSegments.Count}");
        }
    }

    public void GenerateAndCreate()
    {
        GenerateEndlessLevel();
    }

    public void ClearLevel()
    {
        if (generatedLevelParent != null)
        {
            DestroyImmediate(generatedLevelParent);
        }
    }
}