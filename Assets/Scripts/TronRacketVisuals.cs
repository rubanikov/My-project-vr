using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the futuristic Tron-style neon lighting effects, color themes,
/// idle glow pulsing, hit impact flashes, and light trails on the padel racket.
/// </summary>
[DisallowMultipleComponent]
public class TronRacketVisuals : MonoBehaviour
{
    public enum TronTheme
    {
        TronCyan,      // Classic Tron electric cyan
        TronOrange,    // Tron Legacy / Rinzler fiery amber
        CyberLime,     // Futuristic matrix green
        LaserMagenta,  // Synthwave magenta / neon pink
        PureWhite      // Clean high-tech white
    }

    [Header("Theme & Color")]
    [SerializeField] private TronTheme colorTheme = TronTheme.TronCyan;
    [SerializeField] private Color customNeonColor = new Color(0f, 0.95f, 1f, 1f);
    [SerializeField] private bool useCustomColor = false;
    [SerializeField] [Range(1f, 10f)] private float baseEmissionIntensity = 3.5f;

    [Header("Glow & Pulse")]
    [SerializeField] private bool enableIdlePulse = true;
    [SerializeField] [Range(0.2f, 5f)] private float pulseSpeed = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float pulseDepth = 0.25f;

    [Header("Hit Impact Flash")]
    [SerializeField] private bool enableHitFlash = true;
    [SerializeField] [Range(1.5f, 5f)] private float flashMultiplier = 2.5f;
    [SerializeField] private float flashDuration = 0.18f;

    [Header("Trail & Light")]
    [SerializeField] private TrailRenderer swingTrail;
    [SerializeField] private Light rimPointLight;

    [Header("Renderers to illuminate")]
    [SerializeField] private List<Renderer> neonRenderers = new List<Renderer>();

    private MaterialPropertyBlock propBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private float hitFlashTimer = 0f;
    private Color activeColor;

    private static readonly Dictionary<TronTheme, Color> ThemeColors = new Dictionary<TronTheme, Color>()
    {
        { TronTheme.TronCyan, new Color(0.0f, 0.92f, 1.0f, 1.0f) },
        { TronTheme.TronOrange, new Color(1.0f, 0.42f, 0.0f, 1.0f) },
        { TronTheme.CyberLime, new Color(0.1f, 1.0f, 0.35f, 1.0f) },
        { TronTheme.LaserMagenta, new Color(1.0f, 0.05f, 0.6f, 1.0f) },
        { TronTheme.PureWhite, new Color(0.9f, 0.95f, 1.0f, 1.0f) }
    };

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        UpdateActiveColor();
    }

    private void Start()
    {
        ApplyLighting(1f);
    }

    private void OnValidate()
    {
        UpdateActiveColor();
        ApplyLighting(1f);
    }

    private void Update()
    {
        float intensityMultiplier = 1f;

        if (enableIdlePulse)
        {
            float wave = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            intensityMultiplier = 1f - (wave * pulseDepth);
        }

        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            float flashT = Mathf.Clamp01(hitFlashTimer / flashDuration);
            intensityMultiplier += flashT * (flashMultiplier - 1f);
        }

        ApplyLighting(intensityMultiplier);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (enableHitFlash)
        {
            TriggerHitFlash();
        }
    }

    /// <summary>
    /// Trigger bright neon pulse upon striking the ball.
    /// </summary>
    public void TriggerHitFlash()
    {
        hitFlashTimer = flashDuration;
    }

    public void SetTheme(TronTheme theme)
    {
        colorTheme = theme;
        useCustomColor = false;
        UpdateActiveColor();
        ApplyLighting(1f);
    }

    public void SetCustomColor(Color color)
    {
        customNeonColor = color;
        useCustomColor = true;
        UpdateActiveColor();
        ApplyLighting(1f);
    }

    private void UpdateActiveColor()
    {
        if (useCustomColor)
        {
            activeColor = customNeonColor;
        }
        else if (ThemeColors.TryGetValue(colorTheme, out Color c))
        {
            activeColor = c;
        }
        else
        {
            activeColor = new Color(0f, 0.92f, 1f, 1f);
        }

        if (swingTrail != null)
        {
            swingTrail.startColor = new Color(activeColor.r, activeColor.g, activeColor.b, 0.8f);
            swingTrail.endColor = new Color(activeColor.r, activeColor.g, activeColor.b, 0f);
        }

        if (rimPointLight != null)
        {
            rimPointLight.color = activeColor;
        }
    }

    private void ApplyLighting(float factor)
    {
        if (neonRenderers == null || neonRenderers.Count == 0) return;

        if (propBlock == null)
            propBlock = new MaterialPropertyBlock();

        Color hdrEmission = activeColor * (baseEmissionIntensity * factor);

        foreach (var rend in neonRenderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor(EmissionColorId, hdrEmission);
            propBlock.SetColor(BaseColorId, activeColor);
            rend.SetPropertyBlock(propBlock);
        }

        if (rimPointLight != null)
        {
            rimPointLight.intensity = 0.8f * factor;
        }
    }

    public void RegisterNeonRenderer(Renderer rend)
    {
        if (rend != null && !neonRenderers.Contains(rend))
        {
            neonRenderers.Add(rend);
            ApplyLighting(1f);
        }
    }
}
