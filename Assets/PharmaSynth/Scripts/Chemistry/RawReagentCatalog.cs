using System.Collections.Generic;
using UnityEngine;

/// THE manuscript materials table: Appendix C names ~54 distinct materials (raw
/// precursors, prepared solutions and small consumables) that must exist in the lab,
/// not just end-products.
/// One row per manuscript material, each mapped to nature-appropriate labware.
/// Source of truth for: ChemicalData generation (RawReagentForge), the cabinet
/// stocking pass (ReagentCabinetBuilder), hover-info blurbs (LabInfoDatabase
/// fallback) and the demo ready-made kits. Pure + test-pinned.
public static class RawReagentCatalog
{
    public enum LabwareKind
    {
        ReagentBottle,   // solutions/liquids → Vial_WithLabel
        AmberBottle,     // light-sensitive → Vial_Brown_WithLabel
        PowderJar,       // solids/powders → Beaker_100mL_WithLiquid (Powder state)
        DropperBottle,   // test reagents added by drops → WashBottle_WithLabel
        SmallBox,        // paper/stick consumables → procedural labelled box
        IceBucket,       // the cold bath → procedural bucket
    }

    public class Row
    {
        public string chemicalName;   // == ChemicalData.chemicalName (reused when it already exists)
        public PhysicalState state;
        public Color color;
        public float pH = 7f;
        public HazardType hazard = HazardType.None;
        public bool fumeHood;
        public LabwareKind labware;
        public string group;          // cabinet shelf grouping
        public string blurb;          // hover-card trivia + hazard line
        public string uses;           // which experiments call for it (label copy)
    }

    public const string GroupAcids = "Acids & Bases";
    public const string GroupOrganics = "Organics";
    public const string GroupTests = "Test Reagents";
    public const string GroupConsumables = "Consumables & Cold";

    private static List<Row> _rows;

    public static IReadOnlyList<Row> Rows { get { Build(); return _rows; } }

    public static Row Find(string chemicalName)
    {
        Build();
        foreach (var r in _rows)
            if (r.chemicalName == chemicalName) return r;
        return null;
    }

    /// Hover-card fallback body for any catalog chemical (LabInfoDatabase hook).
    public static string BlurbFor(string chemicalName)
    {
        Build();
        string key = Norm(chemicalName);
        foreach (var r in _rows)
            if (Norm(r.chemicalName) == key)
                return r.blurb + (string.IsNullOrEmpty(r.uses) ? "" : " Used in: " + r.uses + ".");
        return null;
    }

    private static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    static Color C(float r, float g, float b, float a = 0.85f) => new Color(r, g, b, a);

    static Row R(string name, PhysicalState st, Color col, float ph, HazardType hz, bool hood,
                 LabwareKind kind, string group, string blurb, string uses)
        => new Row { chemicalName = name, state = st, color = col, pH = ph, hazard = hz,
                     fumeHood = hood, labware = kind, group = group, blurb = blurb, uses = uses };

    private static void Build()
    {
        if (_rows != null) return;
        _rows = new List<Row>
        {
            // ---- Acids & Bases / inorganics (Appendix C reagent lists) --------
            R("Sulfuric Acid", PhysicalState.Liquid, C(0.93f,0.9f,0.8f), 0.3f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "Concentrated sulfuric acid — a powerful dehydrating acid. Always add ACID to water, never the reverse.", "Exp 2, 3, 4, 7"),
            R("Sulfuric Acid 6N", PhysicalState.Liquid, C(0.94f,0.92f,0.85f), 0.5f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "6N sulfuric acid working solution for acidifying test mixtures.", "Exp 2"),
            R("Hydrochloric Acid 6N", PhysicalState.Liquid, C(0.95f,0.95f,0.88f), 0.5f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "6N hydrochloric acid — strong mineral acid for hydrolysis and acidification.", "Exp 2, 4"),
            // ⭐ FUME-HOOD RULE: the manuscript demands the hood in exactly ONE place, Exp 5
            // §A a-c, aniline + acetyl chloride into the flask "in the fume hood". ONLY those
            // two carry hood=true. Flagging anything else makes CORRECT bench play (Exp 2's
            // phenol / acetaldehyde / conc-HCl pours, Exp 5's own §B conc-HCl + bromine-water
            // tests) log FumeHoodViolation mistakes.
            R("Concentrated Hydrochloric Acid", PhysicalState.Liquid, C(0.96f,0.96f,0.9f), 0.1f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "Fuming concentrated HCl — corrosive vapour; keep it capped and at arm's length.", "Exp 2, 5"),
            R("Hydrochloric Acid 0.1N", PhysicalState.Liquid, C(0.97f,0.97f,0.94f), 1.3f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "Dilute 0.1N HCl for gentle acidification.", "Exp 5"),
            R("Diluted Hydrochloric Acid", PhysicalState.Liquid, C(0.97f,0.97f,0.94f), 1f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "Diluted hydrochloric acid for the benzamide hydrolysis test.", "Exp 8"),
            R("Sodium Hydroxide 10%", PhysicalState.Liquid, C(0.92f,0.94f,0.97f), 13.5f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "10% caustic soda solution — saponifies skin on contact. Gloves on.", "Exp 3, 4, 8"),
            R("Sodium Hydroxide 6N", PhysicalState.Liquid, C(0.9f,0.93f,0.97f), 14f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "6N sodium hydroxide — the strong base for neutralisations.", "Exp 2, 4"),
            R("Potassium Hydroxide 10%", PhysicalState.Liquid, C(0.92f,0.95f,0.95f), 13.5f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "10% caustic potash, the alkali for the acetone tests.", "Exp 6"),
            R("Sodium Bicarbonate 10%", PhysicalState.Liquid, C(0.95f,0.95f,0.95f), 8.3f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "Mild 10% bicarbonate solution — fizzes with acids, releasing CO2.", "Exp 2"),
            R("Ammonia Solution", PhysicalState.Liquid, C(0.93f,0.96f,0.98f), 11.6f, HazardType.Toxic, false, LabwareKind.ReagentBottle, GroupAcids,
              "Concentrated ammonia — sharp choking vapour; keep the bottle closed when not pouring.", "Exp 8"),
            R("Ammonium Phosphate", PhysicalState.Powder, C(0.9f,0.9f,0.85f), 8f, HazardType.None, false, LabwareKind.PowderJar, GroupAcids,
              "A pinch feeds the yeast — the nitrogen source for fermentation.", "Exp 3"),
            R("Limewater", PhysicalState.Liquid, C(0.94f,0.95f,0.93f), 12.4f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "Saturated calcium hydroxide — turns milky when CO2 bubbles through it.", "Exp 3"),
            R("Calcium Acetate", PhysicalState.Powder, C(0.92f,0.9f,0.86f), 8f, HazardType.None, false, LabwareKind.PowderJar, GroupAcids,
              "Dry-distilled together with sodium acetate to crack out acetone.", "Exp 6"),
            R("Sodium Acetate", PhysicalState.Powder, C(0.93f,0.92f,0.88f), 8f, HazardType.None, false, LabwareKind.PowderJar, GroupAcids,
              "Anhydrous sodium acetate — methane feedstock with soda lime, acetone feedstock with calcium acetate.", "Tutorial, Exp 6"),
            R("Bleaching Powder", PhysicalState.Powder, C(0.93f,0.95f,0.93f), 11f, HazardType.Corrosive, false, LabwareKind.PowderJar, GroupAcids,
              "Calcium hypochlorite. NEVER mix with acids — it releases toxic chlorine gas.", "Exp 7"),
            R("Anhydrous Calcium Chloride", PhysicalState.Powder, C(0.94f,0.94f,0.94f), 8f, HazardType.None, false, LabwareKind.PowderJar, GroupAcids,
              "The drying agent — soaks water out of the chloroform layer.", "Exp 7"),
            R("Potassium Dichromate", PhysicalState.Powder, C(0.95f,0.55f,0.15f), 4f, HazardType.Toxic, false, LabwareKind.PowderJar, GroupAcids,
              "Bright orange oxidizer for the chloroform oxidation test. Toxic — spatula only, never fingers.", "Exp 7"),
            R("Potassium Iodide 10%", PhysicalState.Liquid, C(0.96f,0.96f,0.9f), 7f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "10% KI solution — pairs with hypochlorite in the iodoform test.", "Exp 3, 6"),
            R("Sodium Hypochlorite", PhysicalState.Liquid, C(0.93f,0.96f,0.9f), 11.5f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupAcids,
              "5-6% hypochlorite (laboratory bleach). Keep it far from every acid — chlorine gas.", "Exp 3, 6"),
            R("Ferric Chloride 10%", PhysicalState.Liquid, C(0.85f,0.65f,0.25f), 2f, HazardType.None, false, LabwareKind.ReagentBottle, GroupTests,
              "Yellow-brown FeCl3 — phenols flash violet on contact: the classic enol test.", "Exp 2"),
            // Named with its strength and authored Liquid: the manuscript ONLY ever uses the
            // 0.1% SOLUTION at the bench —
            // its single solid mention is the prep step ("dissolve 0.1 g in 100 ml"), which
            // the game doesn't simulate. Solid broke Exp 2's "2 ml of 0.1% KMnO4" pour, and
            // it was the one solution on the shelf without its strength in the name.
            R("Potassium Permanganate 0.1%", PhysicalState.Liquid, C(0.45f,0.1f,0.5f), 7f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "0.1% deep-purple oxidizer; decolourising it is positive evidence of oxidation. Keep away from flammables.", "Exp 2, 4"),
            R("Sodium Bisulfite", PhysicalState.Liquid, C(0.94f,0.94f,0.9f), 4.5f, HazardType.None, false, LabwareKind.ReagentBottle, GroupTests,
              "Saturated bisulfite forms a crystalline adduct with acetone — the bisulfite test.", "Exp 6"),
            // Named to MATCH the procedures verbatim ("add 10 ml of distilled water" —
            // it was "Purified Water", so the hover on this bottle AND the wash bottle
            // both read "distilled water" and the player couldn't tell which one a
            // step meant). The wash bottle is rinse-only.
            R("Distilled Water", PhysicalState.Liquid, C(0.85f,0.92f,0.98f, 0.6f), 7f, HazardType.None, false, LabwareKind.ReagentBottle, GroupAcids,
              "The universal solvent. Every 'add distilled water' step in the procedures pours from THIS bottle — the wash bottle is only for rinsing glassware.", "all experiments"),

            // ---- Organics (samples, precursors) --------------------------------
            R("Ethanol", PhysicalState.Liquid, C(0.9f,0.93f,0.96f, 0.6f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Ethyl alcohol — flammable; keep clear of open flames and oxidizers.", "Exp 2, 3, 6"),
            R("Methanol", PhysicalState.Liquid, C(0.9f,0.93f,0.96f, 0.6f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Methyl alcohol — toxic cousin of ethanol; never taste, burns with an almost invisible flame.", "Exp 2, 4"),
            R("Acetaldehyde", PhysicalState.Liquid, C(0.92f,0.94f,0.9f, 0.6f), 7f, HazardType.Volatile, false, LabwareKind.AmberBottle, GroupOrganics,
              "Volatile aldehyde sample — boils near room temperature; recap it quickly.", "Exp 2"),
            R("n-Butyl Alcohol", PhysicalState.Liquid, C(0.92f,0.92f,0.9f, 0.6f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "A primary alcohol sample for the classification tests.", "Exp 2"),
            R("sec-Butyl Alcohol", PhysicalState.Liquid, C(0.92f,0.92f,0.9f, 0.6f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "A secondary alcohol sample — reacts at its own pace in the Lucas-style tests.", "Exp 2"),
            R("tert-Butyl Alcohol", PhysicalState.Liquid, C(0.92f,0.92f,0.9f, 0.6f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "A tertiary alcohol sample — the fastest to react with Lucas-type reagents.", "Exp 2"),
            R("Propyl Alcohol", PhysicalState.Liquid, C(0.92f,0.92f,0.9f, 0.6f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Propan-1-ol, an ester-forming alcohol for the benzoate ester test.", "Exp 4"),
            R("Benzyl Alcohol", PhysicalState.Liquid, C(0.93f,0.92f,0.88f, 0.65f), 7f, HazardType.None, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Aromatic alcohol sample with a faint almond note.", "Exp 2"),
            R("Glycerol", PhysicalState.Liquid, C(0.94f,0.94f,0.9f, 0.7f), 7f, HazardType.None, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Thick, syrupy triol — pours slowly; the viscosity is the point.", "Exp 2"),
            R("Phenol", PhysicalState.Liquid, C(0.95f,0.9f,0.85f, 0.7f), 5.5f, HazardType.Toxic, false, LabwareKind.AmberBottle, GroupOrganics,
              "Carbolic acid — burns skin on contact and darkens in light. Gloves on.", "Exp 2"),
            R("Diluted Acetic Acid", PhysicalState.Liquid, C(0.96f,0.96f,0.92f, 0.6f), 2.9f, HazardType.None, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Vinegar-strength acetic acid for gentle acidification.", "Exp 2, 3"),
            R("Brown Sugar", PhysicalState.Powder, C(0.55f,0.35f,0.18f), 7f, HazardType.None, false, LabwareKind.PowderJar, GroupOrganics,
              "Sucrose feedstock — dissolved to a 12% solution, it feeds the fermentation.", "Exp 3, 4"),
            R("Yeast", PhysicalState.Powder, C(0.85f,0.78f,0.6f), 6f, HazardType.None, false, LabwareKind.PowderJar, GroupOrganics,
              "Dry baker's yeast — the living catalyst that turns sugar into ethanol and CO2.", "Exp 3, 9"),
            // A bench bottle must exist so the player pours their own must into the jar
            // (a jar pre-filled with juice hides the step). The manuscript's non-grape fruit
            // of the student's choice, added by the ml into the jar.
            R("Mixed Fruit Juice", PhysicalState.Liquid, C(0.62f,0.18f,0.32f, 0.85f), 4f, HazardType.None, false, LabwareKind.ReagentBottle, GroupOrganics,
              "The fermentable base — any fruit juice EXCEPT grapes (manuscript exclusion). Its sugars become wine.", "Exp 9"),
            R("Salicylic Acid", PhysicalState.Powder, C(0.95f,0.95f,0.93f), 2.4f, HazardType.None, false, LabwareKind.PowderJar, GroupOrganics,
              "White crystalline phenol-acid — aspirin's parent compound; FeCl3 turns it violet.", "Exp 2"),
            // Aspirin is a RAW reagent even though the game-authored Aspirin
            // module was dropped: Exp 2 §D hydrolyses it back to salicylic acid, so it
            // must sit on the bench in normal play, not just in demo.
            R("Aspirin", PhysicalState.Powder, C(0.97f,0.97f,0.95f), 2.5f, HazardType.None, false, LabwareKind.PowderJar, GroupOrganics,
              "Acetylsalicylic acid tablets — boiling in acid hydrolyses it back to salicylic acid.", "Exp 2"),
            R("Benzoic Acid", PhysicalState.Powder, C(0.96f,0.96f,0.94f), 2.9f, HazardType.None, false, LabwareKind.PowderJar, GroupOrganics,
              "Reference sample of the white aromatic acid you synthesise in the midterm.", "Exp 4"),
            R("Aniline", PhysicalState.Liquid, C(0.6f,0.5f,0.35f, 0.8f), 8.8f, HazardType.Toxic, true, LabwareKind.AmberBottle, GroupOrganics,
              "Oily aromatic amine that darkens in light — toxic through skin; fume hood MANDATORY.", "Exp 5"),
            R("Benzaldehyde", PhysicalState.Liquid, C(0.9f,0.88f,0.8f, 0.7f), 7f, HazardType.Volatile, false, LabwareKind.AmberBottle, GroupOrganics,
              "Smells of almonds; air slowly oxidises it to benzoic acid — which is exactly the midterm synthesis.", "Exp 4"),
            R("Acetyl Chloride", PhysicalState.Liquid, C(0.94f,0.92f,0.85f, 0.7f), 1f, HazardType.Corrosive, true, LabwareKind.AmberBottle, GroupOrganics,
              "The acylating agent — fumes violently with water. Hood, gloves, and slow additions.", "Exp 5"),
            R("Benzoyl Chloride", PhysicalState.Liquid, C(0.93f,0.91f,0.84f, 0.7f), 1f, HazardType.Corrosive, false, LabwareKind.AmberBottle, GroupOrganics,
              "Lachrymator acyl chloride — benzamide's precursor. Eyes water; pour and recap fast.", "Exp 8"),
            // Exp 8 needs a bench source. The manuscript's "10% Sodium NITRATE solution" is
            // the documented nitrite typo; added by the ml, so a liquid solution like the
            // KMnO4 0.1%.
            R("Sodium Nitrite", PhysicalState.Liquid, C(0.94f,0.93f,0.88f), 9f, HazardType.None, false, LabwareKind.ReagentBottle, GroupTests,
              "10% sodium nitrite solution — with dilute HCl it makes nitrous acid in situ (the manual's 'nitrate' is a typo).", "Exp 8"),
            R("Glacial Acetic Acid", PhysicalState.Liquid, C(0.95f,0.95f,0.9f, 0.6f), 2.4f, HazardType.Corrosive, false, LabwareKind.ReagentBottle, GroupOrganics,
              "Water-free acetic acid — freezes at 17 °C, hence 'glacial'. Sharp vinegar bite.", "Exp 5"),
            R("Acetone", PhysicalState.Liquid, C(0.92f,0.94f,0.95f, 0.55f), 7f, HazardType.Flammable, false, LabwareKind.ReagentBottle, GroupOrganics,
              "The simplest ketone — highly flammable, and the chloroform feedstock.", "Exp 2, 6, 7"),

            // ---- Test reagents (added by drops) ---------------------------------
            R("Benedict's Reagent", PhysicalState.Liquid, C(0.2f,0.45f,0.85f, 0.8f), 10f, HazardType.None, false, LabwareKind.DropperBottle, GroupTests,
              "Deep-blue copper reagent — reducing sugars/aldehydes turn it brick red on heating.", "Exp 2"),
            R("Tollen's Reagent", PhysicalState.Liquid, C(0.85f,0.87f,0.9f, 0.7f), 10.5f, HazardType.None, false, LabwareKind.DropperBottle, GroupTests,
              "Ammoniacal silver nitrate — aldehydes plate a mirror of silver onto the glass. Prepared fresh.", "Exp 2, 6"),
            R("Schiff's Reagent", PhysicalState.Liquid, C(0.9f,0.85f,0.88f, 0.7f), 7f, HazardType.None, false, LabwareKind.DropperBottle, GroupTests,
              "Colourless until an aldehyde restores its magenta — ketones like acetone leave it unchanged.", "Exp 6"),
            R("Bromine Water", PhysicalState.Liquid, C(0.8f,0.45f,0.15f, 0.8f), 4f, HazardType.Toxic, false, LabwareKind.AmberBottle, GroupTests,
              "Orange bromine solution — decolourised by unsaturation, and it brominates acetanilide.", "Exp 5"),
            R("Silver Nitrate", PhysicalState.Liquid, C(0.9f,0.9f,0.9f, 0.7f), 6f, HazardType.None, false, LabwareKind.AmberBottle, GroupTests,
              "Light-sensitive AgNO3 — stains skin black; stored in amber glass for a reason.", "Exp 2, 6"),
            R("Alcoholic Silver Nitrate", PhysicalState.Liquid, C(0.9f,0.9f,0.88f, 0.7f), 6f, HazardType.None, false, LabwareKind.AmberBottle, GroupTests,
              "AgNO3 in alcohol — chloroform gives no precipitate with it: the negative halide test.", "Exp 7"),

            // ---- Consumables & Cold ---------------------------------------------
            R("Litmus Paper", PhysicalState.Solid, C(0.75f,0.55f,0.75f), 7f, HazardType.None, false, LabwareKind.SmallBox, GroupConsumables,
              "Strips that read pH at a glance — red below ~4.5, blue above ~8.3.", "Exp 3, 4, 8"),
            R("Matchsticks", PhysicalState.Solid, C(0.8f,0.6f,0.4f), 7f, HazardType.Flammable, false, LabwareKind.SmallBox, GroupConsumables,
              "For the combustion and flammability tests — ethanol burns blue, chloroform refuses to.", "Tutorial, Exp 3, 4, 7"),
            R("Cotton Swabs", PhysicalState.Solid, C(0.95f,0.95f,0.95f), 7f, HazardType.None, false, LabwareKind.SmallBox, GroupConsumables,
              "Plugs the fermentation tube's mouth — lets CO2 out, keeps wild microbes out.", "Exp 3, 4"),
            R("Filter Paper", PhysicalState.Solid, C(0.96f,0.96f,0.96f), 7f, HazardType.None, false, LabwareKind.SmallBox, GroupConsumables,
              "Folds into the funnel to catch crystals — acetanilide and benzoic acid land here.", "Exp 2, 5, 6"),
            R("Ice", PhysicalState.Solid, C(0.85f,0.93f,0.98f, 0.8f), 7f, HazardType.None, false, LabwareKind.IceBucket, GroupConsumables,
              "The ice bath — crash-cools reaction mixtures so the product crystallises out.", "Exp 5, 6, 8"),
        };
    }
}
