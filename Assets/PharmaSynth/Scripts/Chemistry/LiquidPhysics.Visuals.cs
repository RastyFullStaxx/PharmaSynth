using UnityEngine;
using System;
using System.Collections;

/// Drawing the contents: fill level, colour, powder mound, boil and wobble.
public partial class LiquidPhysics
{
    /// Pure (suite-pinned): may LiquidPhysics adopt its host's OWN renderer as the
    /// liquid fill surface? Only when that material runs the PharmaLiquid "_Fill"
    /// shader. An opaque VESSEL mesh must NOT be adopted: UpdateFillPhysics disables
    /// mainRenderer while empty, so a mortar/beaker lending its own mesh would vanish
    /// in Play the moment it was empty.
    public static bool ShouldAdoptHostRenderer(Material hostMaterial)
        => hostMaterial != null && hostMaterial.HasProperty("_Fill");

    void SendMeshBounds()
    {
        // Prefer the liquid renderer's own mesh bounds (the _WithLiquid prefabs keep the
        // liquid volume on a child mesh; the root mesh is the glass shell).
        Mesh boundsMesh = mesh;
        if (mainRenderer != null)
        {
            var mrFilter = mainRenderer.GetComponent<MeshFilter>();
            if (mrFilter != null && mrFilter.sharedMesh != null) boundsMesh = mrFilter.sharedMesh;
        }
        if (boundsMesh == null) return;
        Bounds bounds = boundsMesh.bounds;

        if (mainRenderer != null)
        {
            mainRenderer.material.SetFloat(LocalYMinID, bounds.min.y);
            mainRenderer.material.SetFloat(LocalYMaxID, bounds.max.y);
        }
        if (precipitateRenderer != null)
        {
            precipitateRenderer.material.SetFloat(LocalYMinID, bounds.min.y);
            precipitateRenderer.material.SetFloat(LocalYMaxID, bounds.max.y);
        }
    }

    /// Pure (suite-pinned): the fraction the shader should draw for `ml` in this vessel.
    public static float DisplayFill01(float ml, float maxVolume)
    {
        if (maxVolume <= 0f || ml <= ShowFromMl) return 0f;
        return Mathf.Max(ml / maxVolume, MinVisibleFill01);
    }

    /// Pure (suite-pinned): are these contents a DRY solid rather than a wet mixture?
    public static bool ShowsAsDryPowder(PhysicalState state, bool wetted)
        => !wetted && (state == PhysicalState.Solid || state == PhysicalState.Powder);

    /// Pure (suite-pinned): should the liquid column be drawn at all?
    ///
    /// ⛔ The mound only REPLACES the column when a mound actually exists. A dry solid can
    /// arrive by pour as well as by scoop — the filter and drying steps decant the product
    /// onto a watch glass, and nothing builds a mound there — so suppressing the column on
    /// "this is a solid" alone made five finished products invisible in their own glass.
    /// A vessel must never end up drawing nothing while it holds something.
    public static bool DrawsLiquidColumn(float ml, bool dryPowder, bool hasMound)
        => ml > ShowFromMl && !(dryPowder && hasMound);

    void UpdateFillPhysics()
    {
        float liquidFill = DisplayFill01(currentLiquidVolume, maxVolume);
        float pptFill = DisplayFill01(currentPptVolume, maxVolume);

        // Tilt Correction
        float tilt = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
        float correction = Mathf.Lerp(HorizonalFloatAdj, 1.0f, tilt);

        // Apply to Shader
        if (mainRenderer) mainRenderer.material.SetFloat(FillID, liquidFill * correction);
        if (precipitateRenderer) precipitateRenderer.material.SetFloat(FillID, pptFill * correction);

        // Cutoff Logic (Hide if empty). A DRY powder draws its mound instead of a liquid
        // surface; once anything has been poured on it, it is a mixture and draws like one.
        if (mainRenderer)
        {
            bool hasLiquid = DrawsLiquidColumn(currentLiquidVolume, DryPowder, HasMound);
            if (mainRenderer.enabled != hasLiquid) mainRenderer.enabled = hasLiquid;
        }

        if (precipitateRenderer)
        {
            // ⛔ `>` ShowFromMl, not `> 1f`. Every precipitate rule deposits exactly the incoming
            // pour (one dropper squeeze, 1.0 ml), and 1.0 is not greater than 1.0, so the milky
            // limewater, both iodoform yellows, the acetanilide plates and the benzamide solid
            // would never be drawn.
            bool hasPpt = currentPptVolume > ShowFromMl;
            if (precipitateRenderer.enabled != hasPpt) precipitateRenderer.enabled = hasPpt;
        }

        Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
        if (mainRenderer) mainRenderer.material.SetVector(UpVectorID, localUp);
        if (precipitateRenderer) precipitateRenderer.material.SetVector(UpVectorID, localUp);

        if (mainRenderer) mainRenderer.material.SetFloat(BoilID, BoilAmount());
    }

    /// How hard this vessel's contents are boiling, 0-1.
    ///
    /// Driven by `boilingPointC`, so the liquid itself moves when heated instead of only
    /// the station venting steam. It ramps over the last 8 degrees rather than switching on at the threshold, so a
    /// vessel coming up to temperature visibly starts to move before it runs.
    public float BoilAmount()
        => BoilFor(currentLiquidVolume, currentTempC,
                   currentChemical != null ? currentChemical.boilingPointC : 100f);

    /// Pure so the suite can pin it without a Renderer host (thin-MonoBehaviour-over-pure-core).
    public static float BoilFor(float volumeMl, float tempC, float boilingPointC)
    {
        if (volumeMl <= 1f) return 0f;                       // an empty vessel never boils
        return Mathf.Clamp01((tempC - (boilingPointC - RampC)) / RampC);
    }

    void UpdateWobble(Vector3 velocity, Vector3 angularVelocity)
    {
        if (mainRenderer == null || !mainRenderer.enabled) return;

        time += Time.deltaTime;

        // Decay
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * Recovery);
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * Recovery);

        // Oscillate
        pulse = 2 * Mathf.PI * WobbleSpeed;
        wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
        wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

        // Add Velocity Impact
        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        // Send to Shader
        mainRenderer.material.SetFloat(WobbleXID, wobbleAmountX);
        mainRenderer.material.SetFloat(WobbleZID, wobbleAmountZ);

        if (precipitateRenderer != null)
        {
            precipitateRenderer.material.SetFloat(WobbleXID, 0);
            precipitateRenderer.material.SetFloat(WobbleZID, 0);
        }
    }

    public void UpdateAllVisuals()
    {
        // Play-mode only: LerpColor instantiates renderer.material, which leaks
        // (and errors) in edit mode — the suite drives AddLiquid on vessels that
        // now carry REAL liquid renderers, so guard here, not per-caller.
        if (!Application.isPlaying) return;

        // 1. Handle Main Liquid Transition
        if (currentChemical != null && mainRenderer != null)
        {
            // Stop any old transition so they don't fight
            if (liquidChangeRoutine != null) StopCoroutine(liquidChangeRoutine);

            // Start the new smooth transition
            liquidChangeRoutine = StartCoroutine(LerpColor(
                mainRenderer,
                currentChemical.liquidColor,
                currentChemical.sceneColourAmount
            ));
        }

        // 2. Handle Precipitate Transition
        if (currentPptChemical != null && precipitateRenderer != null)
        {
            if (pptChangeRoutine != null) StopCoroutine(pptChangeRoutine);

            pptChangeRoutine = StartCoroutine(LerpColor(
                precipitateRenderer,
                currentPptChemical.liquidColor,
                currentPptChemical.sceneColourAmount
            ));
        }
    }

    // The Worker Function: smoothly changes color over time
    System.Collections.IEnumerator LerpColor(Renderer targetRenderer, Color targetColor, float targetSceneAmt)
    {
        // Get starting values from the material currently
        Color startColor = targetRenderer.material.GetColor(LiquidColorID);
        float startSceneAmt = targetRenderer.material.GetFloat(SceneColorAmtID);
        float t = 0;

        // Loop until t reaches 1 (100% complete)
        while (t < 1f)
        {
            t += Time.deltaTime * colorChangeSpeed;

            // Calculate intermediate values
            Color newColor = Color.Lerp(startColor, targetColor, t);
            float newAmt = Mathf.Lerp(startSceneAmt, targetSceneAmt, t);

            // Apply to shader
            targetRenderer.material.SetColor(LiquidColorID, newColor);
            targetRenderer.material.SetFloat(SceneColorAmtID, newAmt);

            yield return null; // Wait for next frame
        }
    }
}
