using UnityEngine;

// Put this on the ROOT of every level prefab.
public class LevelSettings : MonoBehaviour
{
    [Tooltip("The level picture. White pixels = white world, black pixels = black world.")]
    public SpriteRenderer art;
    [Tooltip("Optional. level.json exported from the web version (spikes + brambles).")]
    public TextAsset hazardsJson;
    [Tooltip("First = where the ball starts. The rest are checkpoint rings, in the order you reach them.")]
    public Transform[] checkpoints;
    [Tooltip("The exit.")]
    public Transform gate;
    [Tooltip("Pixels (same numbers as the web version).")]
    public float checkpointRadius = 130f, gateRadius = 60f;
    [Tooltip("RESTRICTION: how many swaps you get. 0 = unlimited. Small dots above the ball show what is left.")]
    public int maxSwaps = 0;

    void OnDrawGizmos()
    {
        float ppu = art && art.sprite ? art.sprite.pixelsPerUnit : 100f;
        Gizmos.color = Color.green;
        if (checkpoints != null)
            for (int i = 0; i < checkpoints.Length; i++)
                if (checkpoints[i]) Gizmos.DrawWireSphere(checkpoints[i].position, (i == 0 ? 16f : checkpointRadius) / ppu);
        Gizmos.color = Color.red;
        if (gate) Gizmos.DrawWireSphere(gate.position, gateRadius / ppu);
    }
}
