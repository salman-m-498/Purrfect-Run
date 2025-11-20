using UnityEngine;

/// <summary>
/// EndlessGameSetupValidator - Verifies all endless game components are properly configured
/// Add to a GameObject in your scene and check the Inspector for setup status
/// </summary>
public class EndlessGameSetupValidator : MonoBehaviour
{
    [System.Serializable]
    public class SetupStatus
    {
        public bool hasEndlessGameManager = false;
        public bool hasGameManager = false;
        public bool hasFallDetector = false;
        public bool hasLevelGenerator = false;
        public bool hasWaveController = false;
        public bool hasPlayerController = false;
        public bool hasScoreSystem = false;
        public bool hasHealthSystem = false;
        public bool hasUIManager = false;
        public bool hasEndlessGameUI = false;

        public int GetCompletionPercent()
        {
            int count = 0;
            if (hasEndlessGameManager) count++;
            if (hasGameManager) count++;
            if (hasFallDetector) count++;
            if (hasLevelGenerator) count++;
            if (hasWaveController) count++;
            if (hasPlayerController) count++;
            if (hasScoreSystem) count++;
            if (hasHealthSystem) count++;
            if (hasUIManager) count++;
            if (hasEndlessGameUI) count++;
            return (count * 100) / 10;
        }

        public string GetStatus()
        {
            return $"Setup Completion: {GetCompletionPercent()}%";
        }
    }

    [Header("Validation Status")]
    [SerializeField] private SetupStatus setupStatus = new SetupStatus();

    private void OnEnable()
    {
        ValidateSetup();
    }

    public void ValidateSetup()
    {
        Debug.Log("🔍 Validating Endless Game Setup...");

        setupStatus.hasEndlessGameManager = FindObjectOfType<EndlessGameManager>() != null;
        setupStatus.hasGameManager = FindObjectOfType<GameManager>() != null;
        setupStatus.hasFallDetector = FindObjectOfType<FallDetector>() != null;
        setupStatus.hasLevelGenerator = FindObjectOfType<EndlessLevelGenerator>() != null;
        setupStatus.hasWaveController = FindObjectOfType<WaveController>() != null;
        setupStatus.hasPlayerController = FindObjectOfType<PlayerController>() != null;
        setupStatus.hasScoreSystem = FindObjectOfType<ScoreSystem>() != null;
        setupStatus.hasHealthSystem = FindObjectOfType<HealthSystem>() != null;
        setupStatus.hasUIManager = UIManager.Instance != null;
        setupStatus.hasEndlessGameUI = FindObjectOfType<EndlessGameUI>() != null;

        PrintValidationResults();
    }

    private void PrintValidationResults()
    {
        Debug.Log("═══════════════════════════════════════════");
        Debug.Log($"✓ Endless Game Setup Validation");
        Debug.Log("═══════════════════════════════════════════");

        PrintStatus("EndlessGameManager", setupStatus.hasEndlessGameManager);
        PrintStatus("GameManager", setupStatus.hasGameManager);
        PrintStatus("FallDetector", setupStatus.hasFallDetector);
        PrintStatus("EndlessLevelGenerator", setupStatus.hasLevelGenerator);
        PrintStatus("WaveController", setupStatus.hasWaveController);
        PrintStatus("PlayerController", setupStatus.hasPlayerController);
        PrintStatus("ScoreSystem", setupStatus.hasScoreSystem);
        PrintStatus("HealthSystem", setupStatus.hasHealthSystem);
        PrintStatus("UIManager", setupStatus.hasUIManager);
        PrintStatus("EndlessGameUI", setupStatus.hasEndlessGameUI);

        Debug.Log("═══════════════════════════════════════════");
        Debug.Log(setupStatus.GetStatus());
        Debug.Log("═══════════════════════════════════════════");

        if (setupStatus.GetCompletionPercent() == 100)
        {
            Debug.Log("✅ All components configured! Ready to play endless mode.");
            Debug.Log("   Start game → Click 'Endless Mode' button → Skate infinitely!");
        }
        else
        {
            Debug.LogWarning("⚠️ Some components are missing. See checklist above.");
            Debug.LogWarning("   Check ENDLESS_GAME_SETUP.md for detailed instructions.");
        }
    }

    private void PrintStatus(string componentName, bool isFound)
    {
        string status = isFound ? "✅" : "❌";
        Debug.Log($"{status} {componentName}");
    }

    // Optional: Add a context menu to validate from editor
    [ContextMenu("Validate Setup")]
    public void ValidateSetupFromContext()
    {
        ValidateSetup();
    }

    [ContextMenu("Fix Common Issues")]
    public void FixCommonIssues()
    {
        Debug.Log("🔧 Attempting to fix common issues...");

        // Fix 1: Ensure EndlessGameManager exists
        if (!setupStatus.hasEndlessGameManager)
        {
            GameObject mgr = new GameObject("EndlessGameManager");
            mgr.AddComponent<EndlessGameManager>();
            Debug.Log("✅ Created EndlessGameManager GameObject");
        }

        // Fix 2: Ensure FallDetector exists
        if (!setupStatus.hasFallDetector)
        {
            GameObject detector = new GameObject("FallDetector");
            detector.AddComponent<FallDetector>();
            Debug.Log("✅ Created FallDetector GameObject");
        }

        // Fix 3: Auto-assign references to EndlessGameManager
        EndlessGameManager endless = FindObjectOfType<EndlessGameManager>();
        if (endless != null)
        {
            if (endless.playerController == null)
                endless.playerController = FindObjectOfType<PlayerController>();
            if (endless.levelGenerator == null)
                endless.levelGenerator = FindObjectOfType<EndlessLevelGenerator>();
            if (endless.waveController == null)
                endless.waveController = FindObjectOfType<WaveController>();
            if (endless.scoreSystem == null)
                endless.scoreSystem = FindObjectOfType<ScoreSystem>();
            if (endless.gameManager == null)
                endless.gameManager = FindObjectOfType<GameManager>();
            Debug.Log("✅ Auto-assigned references to EndlessGameManager");
        }

        // Fix 4: Auto-assign references to FallDetector
        FallDetector detector_comp = FindObjectOfType<FallDetector>();
        if (detector_comp != null)
        {
            if (detector_comp.playerController == null)
                detector_comp.playerController = FindObjectOfType<PlayerController>();
            if (detector_comp.gameManager == null)
                detector_comp.gameManager = FindObjectOfType<GameManager>();
            if (detector_comp.endlessGameManager == null)
                detector_comp.endlessGameManager = FindObjectOfType<EndlessGameManager>();
            if (detector_comp.healthSystem == null)
                detector_comp.healthSystem = FindObjectOfType<HealthSystem>();
            Debug.Log("✅ Auto-assigned references to FallDetector");
        }

        ValidateSetup();
    }
}
