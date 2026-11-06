using UnityEngine;

[RequireComponent(typeof(PowderPhysics))]
public class PowderPourer : MonoBehaviour
{
    [Header("Setup")]
    public Transform spout;
    [Tooltip("Use a Particle System to simulate falling powder instead of a LineRenderer.")]
    public ParticleSystem powderStreamParticles; 

    [Header("Settings")]
    public float pourThreshold = 45f;
    public float maxFlowRate = 50f; // Powders usually flow slower than liquids

    private PowderPhysics sourceContainer;
    private ParticleSystem.EmissionModule emissionModule;

    // Null-safe mouth: powder jars have no auto-created spout, and an unwired `spout`
    // would NRE the whole Update loop on the first tilt.
    private Transform Mouth => spout != null ? spout : transform;

    void Start()
    {
        sourceContainer = GetComponent<PowderPhysics>();

        if (powderStreamParticles != null)
        {
            emissionModule = powderStreamParticles.emission;
            emissionModule.enabled = false;
        }
    }

    void Update()
    {
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle > pourThreshold && sourceContainer != null && sourceContainer.currentPowderVolume > 0)
        {
            Pour(tiltAngle);
        }
        else
        {
            StopPouring();
        }
    }

    void Pour(float currentTilt)
    {
        // 1. Calculate Amount
        float tiltDelta = Mathf.InverseLerp(pourThreshold, 180f, currentTilt);
        float currentFlowRate = maxFlowRate * tiltDelta;
        float amountToPour = currentFlowRate * Time.deltaTime;

        // 2. Enable Particles
        if (powderStreamParticles != null && !emissionModule.enabled)
        {
            emissionModule.enabled = true;
            
            // Match particle color to the chemical
            if (sourceContainer.currentChemical != null)
            {
                var main = powderStreamParticles.main;
                main.startColor = sourceContainer.currentChemical.liquidColor;
            }
        }

        // 3. Raycast Physics to find target (triggers ignored — station sensor
        // columns and socket spheres must never swallow the stream; own-body
        // hits skipped like LiquidPourer.ResolveTarget).
        var hits = Physics.RaycastAll(Mouth.position, Vector3.down, 2.0f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit hit = default;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (hits[i].collider.transform.IsChildOf(transform)) continue;   // own body
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; hit = hits[i]; }
        }
        if (hit.collider != null)
        {
            // Check if we hit another powder container
            PowderPhysics targetPowder = hit.collider.GetComponentInParent<PowderPhysics>();

            // (Optional) Check if we hit a LIQUID container to dissolve the powder
            LiquidPhysics targetLiquid = hit.collider.GetComponentInParent<LiquidPhysics>();

            if (targetPowder != null)
            {
                ChemicalData pouredPowder = sourceContainer.PourOut(amountToPour);
                if (pouredPowder != null) targetPowder.AddPowder(pouredPowder, amountToPour);
            }
            else if (targetLiquid != null)
            {
                // If you want powder to be able to fall into your liquid flasks!
                ChemicalData pouredPowder = sourceContainer.PourOut(amountToPour);
                if (pouredPowder != null) targetLiquid.AddLiquid(pouredPowder, amountToPour);
            }
            else
            {
                // Poured onto the table / void - waste it
                sourceContainer.PourOut(amountToPour);
            }
        }
        else
        {
            // Poured into thin air
            sourceContainer.PourOut(amountToPour);
        }
    }

    void StopPouring()
    {
        if (powderStreamParticles != null && emissionModule.enabled)
        {
            emissionModule.enabled = false;
        }
    }
}