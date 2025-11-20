using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Enhanced Editor window for the EnhancedEndlessLevelGenerator
/// Provides easy UI to generate and customize endless levels with grind rails and decorations
/// </summary>
#if UNITY_EDITOR
public class EndlessLevelGeneratorWindow : EditorWindow
{
    private EndlessLevelGenerator generator;
    private Vector2 scrollPosition;
    private bool showGrindRailSettings = true;
    private bool showDecorationSettings = true;
    private bool showLayerSettings = true;

    [MenuItem("Window/Level Tools/Enhanced Endless Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<EndlessLevelGeneratorWindow>("Enhanced Endless Level Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Endless Level Generator", EditorStyles.boldLabel);
        
        // Find or create generator
        if (generator == null)
        {
            GameObject genGO = GameObject.Find("EndlessLevelGenerator");
            if (genGO != null)
            {
                generator = genGO.GetComponent<EndlessLevelGenerator>();
            }
        }

        if (generator == null)
        {
            GUILayout.Label("No EnhancedEndlessLevelGenerator found in scene.", EditorStyles.helpBox);
            if (GUILayout.Button("Create Generator in Scene", GUILayout.Height(40)))
            {
                GameObject genGO = new GameObject("EndlessLevelGenerator");
                generator = genGO.AddComponent<EndlessLevelGenerator>();
                Selection.activeGameObject = genGO;
            }
            return;
        }

        // Display generator
        EditorGUILayout.ObjectField("Generator", generator, typeof(EndlessLevelGenerator), true);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Basic Settings
        GUILayout.Label("Generation Settings", EditorStyles.boldLabel);
        generator.segmentsPerSection = EditorGUILayout.IntField("Segments Per Section", generator.segmentsPerSection);
        generator.sectionSpacing = EditorGUILayout.FloatField("Section Spacing", generator.sectionSpacing);
        generator.totalSections = EditorGUILayout.IntField("Total Sections", generator.totalSections);
        
        GUILayout.Label("Terrain Variation", EditorStyles.boldLabel);
        generator.flatSectionChance = EditorGUILayout.Slider("Flat Section Chance", generator.flatSectionChance, 0f, 1f);
        generator.upSlopeChance = EditorGUILayout.Slider("Upslope Chance", generator.upSlopeChance, 0f, 1f);
        generator.downSlopeChance = EditorGUILayout.Slider("Downslope Chance", generator.downSlopeChance, 0f, 1f);
        
        float total = generator.flatSectionChance + generator.upSlopeChance + generator.downSlopeChance;
        if (Mathf.Abs(total - 1f) > 0.01f)
        {
            EditorGUILayout.HelpBox($"⚠️ Chances should sum to 1.0 (currently {total:F2})", MessageType.Warning);
        }
        
        generator.maxSlopeHeight = EditorGUILayout.FloatField("Max Slope Height", generator.maxSlopeHeight);
        generator.gapChance = EditorGUILayout.Slider("Gap Chance", generator.gapChance, 0f, 0.5f);
        generator.gapLength = EditorGUILayout.FloatField("Gap Length", generator.gapLength);
        
        GUILayout.Label("Level Builder Settings", EditorStyles.boldLabel);
        generator.levelMaterial = EditorGUILayout.ObjectField("Level Material", generator.levelMaterial, typeof(Material), false) as Material;
        generator.width = EditorGUILayout.FloatField("Width", generator.width);
        generator.bankFactor = EditorGUILayout.FloatField("Bank Factor", generator.bankFactor);
        generator.physicsMaterial = EditorGUILayout.ObjectField("Physics Material", generator.physicsMaterial, typeof(PhysicMaterial), false) as PhysicMaterial;

        // Grind Rail Settings
        showGrindRailSettings = EditorGUILayout.Foldout(showGrindRailSettings, "Grind Rail Settings");
        if (showGrindRailSettings)
        {
            EditorGUI.indentLevel++;
            generator.grindRailMaterial = EditorGUILayout.ObjectField("Grind Rail Material", generator.grindRailMaterial, typeof(Material), false) as Material;
            generator.grindRailRadius = EditorGUILayout.FloatField("Grind Rail Radius", generator.grindRailRadius);
            generator.grindRailSegments = EditorGUILayout.IntField("Grind Rail Segments", generator.grindRailSegments);
            generator.grindRailPhysicsMaterial = EditorGUILayout.ObjectField("Grind Rail Physics Material", generator.grindRailPhysicsMaterial, typeof(PhysicMaterial), false) as PhysicMaterial;
            EditorGUI.indentLevel--;
        }

        // Decoration Settings
        showDecorationSettings = EditorGUILayout.Foldout(showDecorationSettings, "Decoration Settings");
        if (showDecorationSettings)
        {
            EditorGUI.indentLevel++;
            
            GUILayout.Label("Tree Materials", EditorStyles.miniLabel);
            SerializedObject so = new SerializedObject(generator);
            SerializedProperty treeMaterials = so.FindProperty("treeMaterials");
            EditorGUILayout.PropertyField(treeMaterials, true);
            
            GUILayout.Label("Rock Materials", EditorStyles.miniLabel);
            SerializedProperty rockMaterials = so.FindProperty("rockMaterials");
            EditorGUILayout.PropertyField(rockMaterials, true);
            
            so.ApplyModifiedProperties();
            
            generator.decorationDensity = EditorGUILayout.FloatField("Decoration Density", generator.decorationDensity);
            generator.decorationOffset = EditorGUILayout.FloatField("Decoration Offset", generator.decorationOffset);
            generator.decorationHeightVariation = EditorGUILayout.FloatField("Height Variation", generator.decorationHeightVariation);
            EditorGUI.indentLevel--;
        }

        // Layer Settings
        showLayerSettings = EditorGUILayout.Foldout(showLayerSettings, "Layer Settings");
        if (showLayerSettings)
        {
            EditorGUI.indentLevel++;
            generator.groundLayerName = EditorGUILayout.TextField("Ground Layer", generator.groundLayerName);
            generator.grindableLayerName = EditorGUILayout.TextField("Grindable Layer", generator.grindableLayerName);
            generator.decorationLayerName = EditorGUILayout.TextField("Decoration Layer", generator.decorationLayerName);
            EditorGUI.indentLevel--;
        }

        GUILayout.Label("Random Seed", EditorStyles.boldLabel);
        generator.randomSeed = EditorGUILayout.IntField("Random Seed (-1 = random)", generator.randomSeed);
        if (GUILayout.Button("Randomize Seed"))
        {
            generator.randomSeed = Random.Range(0, 100000);
        }

        GUILayout.EndScrollView();

        // Action buttons
        GUILayout.Space(20);
        GUILayout.Label("Actions", EditorStyles.boldLabel);
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Enhanced Level", GUILayout.Height(50)))
        {
            generator.GenerateAndCreate();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Clear Level", GUILayout.Height(35)))
        {
            generator.ClearLevel();
        }

        if (GUILayout.Button("Generate Control Points Only", GUILayout.Height(35)))
        {
            generator.GenerateEndlessLevel();
        }

        // Help box with tips
        EditorGUILayout.HelpBox(
            "💡 Tips:\n" +
            "• Gaps will automatically get grind rails\n" +
            "• Decorations spawn on both sides of track\n" +
            "• Use layers for proper collision detection\n" +
            "• Billboard decorations always face camera", 
            MessageType.Info);
    }
}
#endif