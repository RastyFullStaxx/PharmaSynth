using System.Collections.Generic;
using System.Text;

/// Knowledge base behind the hover-inspector pane: pointing at a
/// piece of equipment, a reagent bottle or an NPC pops a card with its name and a
/// short, learn-as-you-play blurb — "what it's for + how to use it" for apparatus,
/// trivia + hazard for reagents. Pure + tested; the resolver (HoverInspector) feeds
/// it a chemical name / prop name and gets back a ready-to-display entry.
public enum LabInfoCategory { Equipment, Reagent, Person }

public sealed class LabInfoEntry
{
    public readonly string Title;
    public readonly LabInfoCategory Category;
    public readonly string Body;
    public LabInfoEntry(string title, LabInfoCategory cat, string body)
    { Title = title; Category = cat; Body = body; }
}

public static class LabInfoDatabase
{
    // ---- reagents: exact-name lookup (candidate = ChemicalData.chemicalName) -------
    private static readonly Dictionary<string, LabInfoEntry> _reagents = new Dictionary<string, LabInfoEntry>();
    // ---- equipment: ordered token → entry, first substring match wins (specific first)
    private static readonly List<KeyValuePair<string, LabInfoEntry>> _equip = new List<KeyValuePair<string, LabInfoEntry>>();
    private static bool _built;

    /// Normalise to lowercase alphanumerics so "Beaker_100mL" and "100 mL Beaker"
    /// both reduce to a comparable key.
    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// Reagent card by chemical name. Always returns an entry (authored, else a
    /// safe generic) so every bottle teaches at least the safety basics.
    public static LabInfoEntry Reagent(string chemicalName)
    {
        Build();
        if (_reagents.TryGetValue(Norm(chemicalName), out var e)) return e;
        string title = string.IsNullOrEmpty(chemicalName) ? "Reagent" : chemicalName;
        // The manuscript materials catalog carries blurbs for the raw-reagent
        // stock (batch H) — authored entries above still win.
        string catalogBlurb = RawReagentCatalog.BlurbFor(chemicalName);
        if (!string.IsNullOrEmpty(catalogBlurb))
            return new LabInfoEntry(title, LabInfoCategory.Reagent, catalogBlurb);
        return new LabInfoEntry(title, LabInfoCategory.Reagent,
            "A laboratory reagent. Handle with gloves and goggles, keep the bottle capped, and pour only what the procedure calls for.");
    }

    /// Equipment card by prop/prefab/display name — null if we have nothing authored
    /// (so random static geometry doesn't pop an empty card).
    public static LabInfoEntry Equipment(string candidate)
    {
        Build();
        string n = Norm(candidate);
        if (n.Length == 0) return null;
        foreach (var kv in _equip)
            if (n.Contains(kv.Key)) return kv.Value;
        return null;
    }

    public static LabInfoEntry Person(bool pharmee)
    {
        Build();
        return pharmee
            ? new LabInfoEntry("Pharmee", LabInfoCategory.Person,
                "Your robot lab guide. He hands out the procedure, cheers you on and flags mistakes — poke him any time to talk or to end the lab tour.")
            : new LabInfoEntry("Dr. Jimenez", LabInfoCategory.Person,
                "The examiner. He proctors the graded run and records your work, but gives no hints — plan your steps before you start.");
    }

    private static void AddReagent(string name, string body)
        => _reagents[Norm(name)] = new LabInfoEntry(name, LabInfoCategory.Reagent, body);

    private static void AddEquip(string token, string title, string body)
        => _equip.Add(new KeyValuePair<string, LabInfoEntry>(token, new LabInfoEntry(title, LabInfoCategory.Equipment, body)));

    // Test hooks.
    public static int ReagentCount { get { Build(); return _reagents.Count; } }
    public static int EquipmentCount { get { Build(); return _equip.Count; } }

    private static void Build()
    {
        if (_built) return;
        _built = true;

        // ---- EQUIPMENT (order: multi-word / specific tokens BEFORE their prefixes) ----
        AddEquip("graduatedcylinder", "Graduated Cylinder",
            "Measures liquid volume accurately — far more precise than a beaker's marks. Stand it on the bench, pour to just below the mark, then read the BOTTOM of the curved surface (the meniscus) at eye level. Use it whenever a step names an exact volume.");
        AddEquip("erlenmeyer", "Erlenmeyer Flask",
            "Cone-shaped reaction flask. The narrow neck lets you swirl vigorously to mix without splashing and slows evaporation of volatile solvents. First choice for running a reaction, collecting a filtrate, or titrating.");
        AddEquip("distillingflask", "Distilling Flask",
            "Round-bottom flask with a side arm for distillation: the mixture boils here, vapour escapes through the arm toward the condenser, and the purified liquid collects beyond. The heart of most syntheses in this course.");
        AddEquip("testtuberack", "Test-Tube Rack",
            "Holds several test tubes upright at once so samples react, settle or cool hands-free. Line your confirmatory tests up in it and label positions so you don't mix up which tube is which.");
        AddEquip("testtubeholder", "Test-Tube Holder",
            "Spring clamp for gripping a test tube while you heat it over a flame — keeps fingers clear of the heat. Clip it near the mouth and keep the open end pointed away from yourself and others.");
        AddEquip("testtubebrush", "Test-Tube Brush",
            "Bristle brush for scrubbing the inside of test tubes and narrow glassware. Clean and rinse between reagents so leftover residue can't spoil the next test.");
        AddEquip("testtube", "Test Tube",
            "Holds a small sample for a reaction or confirmatory test. Add only a few millilitres, heat gently along the SIDE (not the base) while shaking, and always point the mouth away from people.");
        AddEquip("washbottle", "Wash Bottle",
            "Squeeze bottle for RINSING only — clean glassware, wash the last drops of a solution into a flask, knock crystals off a rod. When a procedure says 'add distilled water', that means the Distilled Water reagent bottle, not this.");
        AddEquip("watchglass", "Watch Glass",
            "Shallow glass disc. Cover a beaker to stop splashing, evaporate a few drops to reveal a solid, or hold a sample on the balance. Handy for eyeing a colour change against a white background.");
        AddEquip("beaker", "Beaker",
            "Wide-mouth workhorse for holding, mixing, dissolving and heating liquids. Its printed marks are rough guides only — pour into a graduated cylinder whenever a step needs an accurate volume.");
        AddEquip("bunsenburner", "Bunsen Burner",
            "Gas burner for strong heat. Light it with the striker, then open the air collar until the flame turns blue and quiet — that hot blue cone is for real heating; a yellow flame is cool and sooty. Close the collar or the gas to stop.");
        AddEquip("alcoholburner", "Alcohol Burner",
            "Spirit lamp giving a gentle, steady flame for mild warming. Light the wick with a match; put it out by capping it — NEVER blow it out. Keep other flammable liquids well clear.");
        AddEquip("cruribletongs", "Crucible Tongs", "Long tongs for lifting a hot crucible or evaporating dish. Metal stays cool at the far end — never grab hot glass or ceramic by hand.");
        AddEquip("crucibletongs", "Crucible Tongs", "Long tongs for lifting a hot crucible or evaporating dish. Metal stays cool at the far end — never grab hot glass or ceramic by hand.");
        AddEquip("crucible", "Crucible",
            "Ceramic cup that shrugs off very high heat — used to ignite, dry or fuse a solid to constant weight. It looks the same hot or cold, so always move it with tongs and let it cool on wire gauze.");
        AddEquip("claytriangle", "Clay Triangle", "Sits on a tripod or iron ring and cradles a crucible over a flame for strong, direct heating.");
        AddEquip("evaporatingdish", "Evaporating Dish",
            "Shallow porcelain dish for boiling off solvent to leave the dissolved solid (your product) behind. Heat slowly near the end so the residue doesn't spit or scorch.");
        AddEquip("glassrod", "Stirring Rod",
            "Solid glass rod. Stir to dissolve or mix evenly, and pour liquid DOWN the rod so it runs into a funnel or flask without splashing. Also used to touch a drop onto litmus paper.");
        AddEquip("dropper", "Dropper", "Adds liquid one drop at a time for small, precise additions — perfect when a test needs 'a few drops' of a reagent. Don't let the tip touch the sample, or you'll contaminate the bottle.");
        AddEquip("scoopula", "Scoopula", "Curved scoop for transferring powdered or crystalline solids. Keep it clean and DRY between chemicals so nothing cross-contaminates.");
        AddEquip("spatula", "Porcelain Spatula", "Porcelain spatula — transfers small amounts of solid reagent onto the balance or into a flask. Wipe it clean between chemicals to avoid mixing reagents.");
        AddEquip("forceps", "Forceps", "Fine tweezers for handling crystals, small solids or strips of paper without touching them with your fingers (which adds moisture and oils).");
        AddEquip("motar", "Mortar & Pestle", "The bowl (mortar) and grinder (pestle) crush lumps into a fine, even powder — a bigger surface area means the solid dissolves and reacts faster.");
        AddEquip("mortar", "Mortar & Pestle", "The bowl (mortar) and grinder (pestle) crush lumps into a fine, even powder — a bigger surface area means the solid dissolves and reacts faster.");
        AddEquip("pestle", "Mortar & Pestle", "The pestle grinds a solid against the mortar bowl into a fine powder, so it dissolves and reacts faster.");
        AddEquip("retortstand", "Retort Stand", "The tall support rod that anchors your setup. Clamps, rings and gauze attach to it to hold flasks, condensers and funnels steady above the bench.");
        AddEquip("ironring", "Iron Ring", "Clamps to the stand to support a funnel during filtration, or wire gauze and a flask above a burner.");
        AddEquip("wiregauze", "Wire Gauze", "Metal mesh laid on a tripod or ring. It spreads the flame's heat evenly so glassware warms gently instead of cracking from a hot spot.");
        AddEquip("tripod", "Tripod", "Three-legged stand that holds glassware over a burner. Rest wire gauze on top first so the heat spreads evenly.");
        AddEquip("funnel", "Funnel", "Guides liquid into a narrow neck and, fitted with folded filter paper, separates solid from liquid by gravity filtration. Wet the paper first so it clings to the cone.");
        AddEquip("balance", "Balance", "Weighs reagents and product. Place the empty container on the pan and TARE (zero) it, then add reagent until you reach the target mass.");
        AddEquip("vial", "Reagent Vial", "Small capped bottle that stores or dispenses a reagent or your finished product. Keep it capped between pours so nothing spills or evaporates.");
        AddEquip("labspeaker", "Lab Speaker", "Plays the lab's ambient music and gets louder as you approach — a handy audio landmark for this corner of the room.");
        AddEquip("speaker", "Lab Speaker", "Plays the lab's ambient music and gets louder as you approach — a handy audio landmark for this corner.");
        AddEquip("litmus", "Litmus Paper", "pH test strip. Touch a drop of solution to it: it turns RED in acid and BLUE in base (violet when neutral). Used to check acidity and to catch an alkaline gas like ammonia coming off a reaction.");
        AddEquip("matchstick", "Matchstick", "Strike it to make a small flame for lighting a burner, or to run a combustion test — a glowing or lit splint reacts differently to flammable vapours (e.g. it goes out over non-flammable chloroform).");
        AddEquip("filterpaper", "Filter Paper", "A paper disc folded into a cone in the funnel. Liquid passes through while solid crystals stay behind — the standard way to collect or purify your product.");
        AddEquip("cottonswab", "Cotton Swab", "Loose plug of cotton used to stopper a fermentation tube — it lets gas escape but keeps airborne contaminants out during wine-making.");
        AddEquip("icebucket", "Ice Bath", "A bucket of ice water. Lower a flask into it to crash-cool a reaction so the product crystallises out — cold makes crystals form faster and purer.");
        AddEquip("icebath", "Ice Bath", "A bucket of ice water. Lower a flask into it to crash-cool a reaction so the product crystallises out — cold makes crystals form faster and purer.");
        AddEquip("separatory", "Separatory Funnel", "Pear-shaped funnel with a tap for separating two liquid layers that don't mix. Let them settle, then drain the lower layer off through the tap.");
        AddEquip("florence", "Florence Flask", "Round-bottomed boiling flask. Its even curve heats uniformly, so it's used for distillation and reactions that must reach a steady boil.");
        AddEquip("thermometer", "Thermometer", "Reads the temperature of a bath or mixture. Watch it so you heat to the range a step specifies and never overshoot — overheating ruins the product.");
        // Distillation set + methane splint.
        AddEquip("condenser", "Condenser", "Liebig condenser: hot vapour passes through the inner tube while cold water flows around the outer jacket, cooling the vapour back to liquid. Clamp it sloping down from the flask so the distillate runs into the receiver.");
        AddEquip("deliverytube", "Delivery Tube", "Bent glass tube (with rubber) that carries an evolved gas or vapour from a stoppered flask to where it's collected — e.g. leading methane over water, or CO2 into limewater.");
        AddEquip("waterbath", "Water Bath", "A bath of hot water that heats a flask GENTLY and evenly, below water's boiling point — used for warming volatile or flammable mixtures where a direct flame would be dangerous.");
        AddEquip("utilityclamp", "Utility Clamp", "Three-prong clamp on a boss head that grips a flask, condenser or tube and holds it steady on the retort stand. Tighten just enough to hold — over-tightening cracks the glass.");
        AddEquip("aspirator", "Aspirator", "Water-driven suction pump that pulls a vacuum for filtration, speeding up how fast liquid draws through the filter paper.");
        AddEquip("rubberstopper", "Rubber Stopper", "Bored rubber bung that seals a flask while letting a delivery tube or thermometer through the hole — keeps the evolved gas heading where you want it.");
        AddEquip("stopper", "Rubber Stopper", "Bored rubber bung that seals a flask while letting a delivery tube or thermometer through the hole.");
        AddEquip("splint", "Wooden Splint", "A thin wooden stick you light to test a collected gas: held to the mouth of the tube, methane ignites with a small pop and a clean blue flame — that confirms the gas.");
        AddEquip("fumehood", "Fume Hood", "Ventilated open alcove that draws toxic fumes away from you — nothing to open or switch on. Do every step with aniline, chloroform, ammonia or acid chlorides with the VESSEL inside it; its label shows a check mark while you're protected.");
        // PPE wearables (locker displays).
        AddEquip("labcoat", "Lab Coat", "Protects your clothes and skin from splashes and stray reagent. One of the three required PPE pieces — put it on at the locker; the door stays locked until all three are worn.");
        AddEquip("coat", "Lab Coat", "Protects your clothes and skin from splashes and stray reagent. One of the three required PPE pieces — put it on at the locker; the door stays locked until all three are worn.");
        AddEquip("goggle", "Safety Goggles", "Shield your eyes from splashes, fumes and flying glass. Required PPE — wear them the WHOLE time you work, not just while pouring.");
        AddEquip("glove", "Nitrile Gloves", "Protect your hands from corrosive and toxic reagents. Required PPE — pull on a pair before touching any chemical, and change them if they get splashed.");
        AddEquip("clothes", "Casual Clothes", "Your everyday outfit, seen in the mirror. Pull the lab coat, goggles and gloves on over it at the locker before an experiment will start.");

        // ---- REAGENTS (chemical-name keyed: what it is + how it's used + hazard) -------
        // Raw materials & test reagents.
        AddReagent("Sodium Hydroxide", "Caustic soda, a strong base (lye). Dissolving it in water releases heat, and it hydrolyses amides — it's what frees ammonia from benzamide. Very corrosive to skin and eyes: gloves and goggles are essential.");
        AddReagent("Sodium Acetate", "The sodium salt of acetic acid. Heated with soda lime it loses CO2 and gives off methane — the feedstock pair for the tutorial synthesis. Low hazard, but keep it dry.");
        AddReagent("Soda Lime", "A mix of sodium and calcium hydroxide. Ground with sodium acetate and heated strongly, it drives off methane gas. A caustic base — avoid skin contact.");
        AddReagent("Glacial Acetic Acid", "Nearly pure acetic acid (>99%) — 'glacial' because it freezes just below room temperature. It's the acid solvent/catalyst in the acetanilide prep. Pungent and corrosive; use in the hood.");
        AddReagent("Potassium Permanganate 0.1%", "Deep-purple strong oxidiser. It oxidises benzaldehyde all the way to benzoic acid; as it reacts the purple fades and brown manganese solid appears. Stains skin — handle with gloves.");
        AddReagent("Benzaldehyde", "Oily liquid with an almond smell. Potassium permanganate oxidises it to benzoic acid in the midterm. Volatile and an irritant — keep it in the hood and capped.");
        AddReagent("Acetyl Chloride", "A fiercely reactive acetylating agent that fumes in moist air (releasing HCl). It converts aniline into acetanilide. Corrosive; add it carefully in the fume hood.");
        AddReagent("Aniline", "Oily aromatic amine, toxic and readily absorbed through skin. Acetylation 'caps' it as the safer solid acetanilide. Always glove up and work in the hood.");
        AddReagent("Sulfuric Acid", "Dense, strongly acidic and dehydrating — a common catalyst (e.g. for aspirin). Golden rule: add acid TO water, never water to acid, or it can boil and spit. Causes severe burns.");
        AddReagent("Hydrochloric Acid 6N", "A strong acid that fumes slightly. Supplies acid and chloride for neutralisations and confirmatory tests, and dissolves many carbonates with fizzing. Corrosive to skin and eyes.");
        AddReagent("Sodium Nitrite", "Mixed with acid it makes nitrous acid in place for the nitrous-acid test on benzamide (nitrogen gas bubbles off). Toxic if swallowed — keep it labelled and capped.");
        AddReagent("Ammonia Solution", "Aqueous ammonia — sharp-smelling weak base. It turns red litmus blue, so a whiff that blues damp litmus confirms an amide or a base is releasing ammonia. Use in the hood.");
        AddReagent("Salicylic Acid", "White solid, the starting material for aspirin — acetylation converts it to acetylsalicylic acid. It also gives a violet colour with ferric chloride (a phenol test).");
        AddReagent("Ferric Chloride 10%", "Iron(III) test reagent. A few drops give a violet–purple colour with phenols such as salicylic acid, so it tells unreacted starting material from finished aspirin.");
        AddReagent("Silver Nitrate", "Test reagent for halides — forms a white or cream precipitate (e.g. silver chloride). Light turns spills black, and it stains skin; keep it in its amber bottle.");
        AddReagent("Bromine Water", "Orange bromine solution. It's decolourised (loses its colour) by unsaturated or easily-oxidised compounds, so fading confirms them. Toxic and irritating — hood only.");
        AddReagent("Sodium Hypochlorite", "Household-strength bleach — an oxidiser and chlorinating agent used in iodoform-type tests and to make chloroform. Never mix it with acid (releases toxic chlorine gas).");
        AddReagent("Schiff's Reagent", "A colourless dye reagent that turns magenta with ALDEHYDES but stays clear with ketones — so it's the test that tells acetone (a ketone, negative) from an aldehyde.");
        AddReagent("Limewater", "Clear calcium hydroxide solution. Bubbling carbon dioxide through it turns it milky white — the classic confirmatory test for CO2 from fermentation or a carbonate.");
        AddReagent("Yeast", "Living fungus that ferments sugars into ethanol and carbon dioxide. It's the biological engine of the wine-making module — keep it warm, not hot, or you kill it.");
        AddReagent("Mixed Fruit Juice", "Mixed non-grape fruit juice (the manuscript excludes grapes) - the sugar source yeast ferments into wine (ethanol). Its dissolved sugars are the 'food' that drives the reaction.");
        AddReagent("Sodium Carbonate", "Washing soda — a mild base and a by-product of the methane synthesis. Fizzes with acid, releasing CO2.");
        AddReagent("Calcium Carbonate", "Chalk or limestone. Treated with acid it fizzes and releases carbon dioxide, which you can confirm with limewater.");
        AddReagent("Carbon Dioxide", "Colourless gas that turns limewater milky — the standard confirmatory test. Given off by fermentation and by carbonates meeting acid.");
        AddReagent("Manganese Dioxide", "A dark oxidiser and catalyst. It speeds the breakdown of peroxides and helps oxidise organics.");
        AddReagent("Iodoform", "Pale-yellow solid with a sharp antiseptic smell — the positive result of the iodoform test for methyl ketones and ethanol.");

        // Ready-made products (the shelf 'shortcut' reagents — hidden in normal
        // play, shown in demo). Each card teaches how it's really made + how to
        // confirm it, so hovering the finished product still educates.
        AddReagent("Ethanol", "Drinking alcohol and a versatile solvent — the target of the ethyl-alcohol module, made by fermenting sugar with yeast. Highly flammable; keep it away from every flame.");
        AddReagent("Benzoic Acid", "White crystalline aromatic acid and food preservative — the midterm product, made by oxidising benzaldehyde with potassium permanganate. It's only weakly soluble, so it crystallises out on cooling.");
        AddReagent("Acetanilide", "White amide solid, the acetylation product of aniline (once a fever medicine). Confirm it by its sharp melting point; recrystallise from hot water to purify.");
        AddReagent("Acetone", "Volatile, highly flammable solvent (propanone) and the midterm product from dry-distilling calcium acetate. Because it's a KETONE it is Schiff-NEGATIVE (stays colourless) — that's how you tell it from an aldehyde.");
        AddReagent("Chloroform", "Dense, sweet-smelling solvent (CHCl3), made here by reacting acetone with bleaching powder. A suspected carcinogen and NON-flammable — a lit splint goes out over it. Use only in the fume hood.");
        AddReagent("Benzamide", "The simplest aromatic amide — the final-period product from benzoyl chloride and ammonia. Boil it with alkali and it hydrolyses, releasing ammonia that blues damp litmus (its confirmatory test).");
        AddReagent("Aspirin", "Acetylsalicylic acid, a common pain reliever — made by acetylating salicylic acid with acetic anhydride and an acid catalyst. Unlike its starting material it gives NO violet with ferric chloride, which confirms the reaction worked.");
        AddReagent("Wine", "The fermented product of the wine-making module — grape sugar turned to ethanol by yeast, with CO2 bubbling off through the fermentation lock.");
    }
}
