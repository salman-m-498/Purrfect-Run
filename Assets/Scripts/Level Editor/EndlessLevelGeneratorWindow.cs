using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor window for the EndlessLevelGenerator
/// Provides easy UI to generate and customize endless levels
/// </summary>
#if UNITY_EDITOR
public class EndlessLevelGeneratorWindow : EditorWindow
{
    private EndlessLevelGenerator generator;
    private Vector2 scrollPosition;

    [MenuItem("Window/Level Tools/Endless Level Generator")]
    public static void ShowWindow()
    {
        GetWindow<EndlessLevelGeneratorWindow>("Endless Level Generator");
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
            GUILayout.Label("No EndlessLevelGenerator found in scene.", EditorStyles.helpBox);
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

        // Settings
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
        if (GUILayout.Button("Generate and Create Level", GUILayout.Height(50)))
        {
            generator.GenerateAndCreate();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Generate Control Points Only", GUILayout.Height(35)))
        {
            generator.GenerateEndlessLevel();
        }

        if (GUILayout.Button("Create Level from Control Points", GUILayout.Height(35)))
        {
            generator.CreateLevelFromControlPoints();
        }

        GUILayout.Space(10);
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Generated Level", GUILayout.Height(35)))
        {
            // Would clear the generated level here if needed
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(20);
        EditorGUILayout.HelpBox(
            "1. Configure your terrain preferences above\n" +
            "2. Click 'Generate and Create Level'\n" +
            "3. The level will be generated as a child of this object\n" +
            "4. Adjust settings and regenerate as needed\n\n" +
            "The generator creates varied terrain with:\n" +
            "• Flat sections\n" +
            "• Uphill slopes\n" +
            "• Downhill slopes\n" +
            "• Occasional gaps for obstacles",
            MessageType.Info
        );
    }
}
#endif
