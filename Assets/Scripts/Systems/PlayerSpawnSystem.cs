using UnityEngine;

/// <summary>
/// Tiny service that knows how to place the player at the current level’s
/// start transform and reset its state.  Works in both normal and endless mode.
/// </summary>
public static class PlayerSpawnSystem
{
    private static Transform _startPoint;   // cache to avoid FindObjectOfType every frame

    public static void SetStartPoint(Transform t) => _startPoint = t;

    public static void SpawnPlayer()
    {
        if (_startPoint == null)
        {
            Debug.LogWarning("[PlayerSpawnSystem] No start point assigned – did LevelManager run?");
            return;
        }

        PlayerController pc = Object.FindObjectOfType<PlayerController>();
        if (pc == null)
        {
            Debug.LogWarning("[PlayerSpawnSystem] No PlayerController found in scene.");
            return;
        }

        pc.transform.position = _startPoint.position;
        pc.transform.rotation = _startPoint.rotation;   // keep yaw if you rotated the marker
        pc.ResetState();                                // existing public helper you already have
    }
}