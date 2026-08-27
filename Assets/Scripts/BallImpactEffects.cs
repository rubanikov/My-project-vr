using UnityEngine;

// Expanding ring ripple where the ball hits court surfaces (2026-08-27
// design-refresh, user choice over a flat splash): a Tron-style ring flares
// at the contact point on walls, floor, ceiling, or net, oriented flat on
// the surface, growing and fading over ~0.4s. Radius and brightness scale
// with impact speed — soft touches barely register.
//
// Racket contacts are excluded on purpose: those already speak through
// haptics and the hit sound. Pooled quads with one MaterialPropertyBlock
// each; no lights, no instantiation at play time.
[RequireComponent(typeof(Rigidbody))]
public class BallImpactEffects : MonoBehaviour
{
    [Tooltip("CourtClash/ImpactRing material (asset so the shader ships in builds).")]
    [SerializeField] private Material ringMaterial;
    [SerializeField] private int poolSize = 8;
    [SerializeField] private float lifeSeconds = 0.4f;
    [SerializeField] private float minSize = 0.18f;
    [SerializeField] private float maxSize = 0.6f;
    [Tooltip("Impact speed (m/s) at which the ring reaches full size and brightness.")]
    [SerializeField] private float saturationSpeed = 9f;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform[] rings;
    private MeshRenderer[] renderers;
    private MaterialPropertyBlock[] blocks;
    private float[] startTimes;
    private float[] intensities;
    private int next;

    private void Awake()
    {
        rings = new Transform[poolSize];
        renderers = new MeshRenderer[poolSize];
        blocks = new MaterialPropertyBlock[poolSize];
        startTimes = new float[poolSize];
        intensities = new float[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"ImpactRing_{i}";
            Destroy(quad.GetComponent<Collider>());
            quad.SetActive(false);

            renderers[i] = quad.GetComponent<MeshRenderer>();
            if (ringMaterial != null) renderers[i].sharedMaterial = ringMaterial;
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            rings[i] = quad.transform;
            blocks[i] = new MaterialPropertyBlock();
            startTimes[i] = float.NegativeInfinity;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        string surface = collision.gameObject.name;
        bool isCourtSurface = surface == "CourtFloor" || surface == "CourtCeiling"
            || surface == "CourtNet" || surface.StartsWith("CourtWall");
        if (!isCourtSurface) return;

        ContactPoint contact = collision.GetContact(0);
        float speed = collision.relativeVelocity.magnitude;
        float intensity = Mathf.Clamp01(speed / saturationSpeed);
        if (intensity < 0.05f) return;

        int i = next;
        next = (next + 1) % poolSize;

        rings[i].position = contact.point + contact.normal * 0.01f;
        rings[i].rotation = Quaternion.LookRotation(-contact.normal);
        startTimes[i] = Time.time;
        intensities[i] = intensity;
        rings[i].gameObject.SetActive(true);
    }

    private void Update()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (!rings[i].gameObject.activeSelf) continue;

            float progress = (Time.time - startTimes[i]) / lifeSeconds;
            if (progress >= 1f)
            {
                rings[i].gameObject.SetActive(false);
                continue;
            }

            float size = Mathf.Lerp(minSize, maxSize, intensities[i]);
            rings[i].localScale = Vector3.one * size;

            blocks[i].SetFloat(ProgressId, progress);
            blocks[i].SetColor(ColorId,
                new Color(0.35f, 1f, 1f, Mathf.Lerp(0.35f, 1f, intensities[i])));
            renderers[i].SetPropertyBlock(blocks[i]);
        }
    }
}
