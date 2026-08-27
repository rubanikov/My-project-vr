using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Score Pulse (2026-08-27 design-refresh): emissive trim strips along
// the court's edges — floor perimeter, wall-top edges, vertical corners, and
// the net top — idle at a faint Tron cyan, surging bright GREEN when the
// player scores and RED when the AI does, easing back over ~1s. Built as
// separate geometry because the cyan lines on the wall/floor textures are
// baked in and can't be recolored individually.
//
// One shared material instance drives every strip, so a pulse is a single
// color write per frame.
public class CourtGlowPulse : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CourtBuilder courtBuilder;
    [SerializeField] private MatchController matchController;
    [Tooltip("Unlit material asset for the strips (asset so its shader ships in builds).")]
    [SerializeField] private Material trimMaterial;

    [Header("Look")]
    [SerializeField] private Color idleColor = new Color(0f, 0.55f, 0.6f);
    [SerializeField] private Color playerPulseColor = new Color(0.1f, 1.6f, 0.3f);
    [SerializeField] private Color aiPulseColor = new Color(1.7f, 0.15f, 0.1f);
    [SerializeField] private float pulseSeconds = 1.1f;
    [SerializeField] private float matchEndPulseSeconds = 3f;
    [SerializeField] private float trimThickness = 0.035f;

    private Material sharedInstance;
    private Coroutine pulseCoroutine;

    private void OnEnable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt += OnCourtBuilt;
        if (matchController != null)
        {
            matchController.PointDetail += OnPointDetail;
            matchController.MatchEnded += OnMatchEnded;
        }
    }

    private void OnDisable()
    {
        if (courtBuilder != null) courtBuilder.CourtBuilt -= OnCourtBuilt;
        if (matchController != null)
        {
            matchController.PointDetail -= OnPointDetail;
            matchController.MatchEnded -= OnMatchEnded;
        }
    }

    private void OnCourtBuilt(Vector3 halfExtents)
    {
        if (sharedInstance != null) return;

        sharedInstance = trimMaterial != null ? new Material(trimMaterial) : null;
        if (sharedInstance == null) return;
        sharedInstance.SetColor("_BaseColor", idleColor);

        float cx = courtBuilder.CenterX;
        float netZ = courtBuilder.NetZ;
        float hw = halfExtents.x;
        float hd = courtBuilder.HalfDepthPerSide;
        float minZ = courtBuilder.CourtMinZ;
        float maxZ = courtBuilder.CourtMaxZ;
        float top = 4f - trimThickness; // wall/ceiling junction (walls reach the 4m ceiling)
        float t = trimThickness;
        float width = hw * 2f;
        float depth = hd * 2f;

        foreach (float y in new[] { t * 0.5f, top })
        {
            Strip($"Trim_NS_{y:0.0}_min", new Vector3(cx, y, minZ + t), new Vector3(width, t, t));
            Strip($"Trim_NS_{y:0.0}_max", new Vector3(cx, y, maxZ - t), new Vector3(width, t, t));
            Strip($"Trim_EW_{y:0.0}_e", new Vector3(cx + hw - t, y, netZ), new Vector3(t, t, depth));
            Strip($"Trim_EW_{y:0.0}_w", new Vector3(cx - hw + t, y, netZ), new Vector3(t, t, depth));
        }

        foreach (float x in new[] { cx - hw + t, cx + hw - t })
        {
            Strip($"Trim_Corner_{x:0.0}_min", new Vector3(x, 2f, minZ + t), new Vector3(t, 4f, t));
            Strip($"Trim_Corner_{x:0.0}_max", new Vector3(x, 2f, maxZ - t), new Vector3(t, 4f, t));
        }

        Strip("Trim_NetTop", new Vector3(cx, 0.92f, netZ), new Vector3(width, t, t));
    }

    private void Strip(string name, Vector3 position, Vector3 scale)
    {
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = name;
        Destroy(strip.GetComponent<Collider>()); // pure light — never a bounce surface
        strip.transform.SetParent(transform, false);
        strip.transform.position = position;
        strip.transform.localScale = scale;
        strip.GetComponent<Renderer>().sharedMaterial = sharedInstance;
    }

    private void OnPointDetail(Side side, FaultKind kind)
    {
        Pulse(side == Side.Player ? playerPulseColor : aiPulseColor, pulseSeconds);
    }

    private void OnMatchEnded(Side winner)
    {
        Pulse(winner == Side.Player ? playerPulseColor : aiPulseColor, matchEndPulseSeconds);
    }

    private void Pulse(Color color, float seconds)
    {
        if (sharedInstance == null) return;
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine(color, seconds));
    }

    private IEnumerator PulseRoutine(Color color, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float fade = Mathf.Pow(1f - Mathf.Clamp01(elapsed / seconds), 2f); // ease-out
            sharedInstance.SetColor("_BaseColor", Color.Lerp(idleColor, color, fade));
            yield return null;
        }
        sharedInstance.SetColor("_BaseColor", idleColor);
        pulseCoroutine = null;
    }
}
