# Scene Objects

> [!warning] Generated note - do not hand-edit
> Derived from the code by `python Tools/gen-vault-reference.py`.
> To change what it says, fix the thing it is derived FROM, then re-run.

Root-level GameObjects in each scene, and every prefab instance. Use this to
find *where a thing lives* before writing a builder that spawns a duplicate.

> [!danger] The bench already exists
> A layout must NEVER stage general apparatus or a reagent bottle. Vessels BIND
> to what is already here via `Vessel.benchItem`. See [[Gotchas]].

Up: [[Home]] - [[The Lab Scene]] - [[Gotchas]]

---

## SampleScene (the lab)
<sub>`Assets/Scenes/SampleScene.unity`</sub>

### Root objects (48)

| Object | local Y |
|---|---|
| `AmbientPlayer` | 0.000 |
| `AtmosphereVfx` | 0.000 |
| `BeginButton` | 0.990 |
| `BenchApparatus` | 0.000 |
| `CutsceneMarkers` | 0.000 |
| `Directional Light` | 3.000 |
| `DistillingFlask` | 1.665 |
| `DistillingFlask_2` | 1.665 |
| `DoorApproachTrigger` | 1.200 |
| `DrJimenez` | 0.000 |
| `DressingMirror` | 1.350 |
| `EntranceSeals` | 0.000 |
| `EquipmentShelf` | 0.000 |
| `ExperimentSystems` | 0.000 |
| `ExteriorSeals` | 0.000 |
| `FillLight` | 2.500 |
| `FillLight (1)` | 2.500 |
| `FillLight (2)` | 2.500 |
| `FrontDoorSpawn` | 0.000 |
| `FumeHood_StandIn` | 0.000 |
| `GapSealWall` | 0.748 |
| `GapSealWall (1)` | 1.087 |
| `Global Volume` | 0.000 |
| `LabAlarmFixture` | 2.720 |
| `LabConsumables` | 0.000 |
| `LabDoorBlocker` | 1.188 |
| `LabDoorController` | 0.000 |
| `LabLights` | 0.000 |
| `LabMusicPlayer` | 0.000 |
| `LabNavMesh` | 0.000 |
| `LabProbes` | 0.000 |
| `LabSpeaker` | 0.910 |
| `LabThresholdTrigger` | 1.200 |
| `LockerLiners` | 0.000 |
| `ManualLayout_W512` | 0.000 |
| `MethaneStage` | 0.000 |
| `PPELocker` | 1.150 |
| `PPE_Standins` | 0.000 |
| `PharmeeAnchors` | 0.000 |
| `ProctorPoints` | 0.000 |
| `ReagentCabinets` | 0.000 |
| `ReagentShelf` | 0.000 |
| `ReviewCornerSpawn` | 0.220 |
| `ScreenFader` | 0.000 |
| `Services` | 0.407 |
| `SpawnVFX` | 0.407 |
| `WaypointMarker` | 0.000 |
| `WorldLabels` | 0.000 |

### Prefab instances (122)

| Instance | position override |
|---|---|
| `Beaker_500mL_2` | x=-0.3784001, y=1.2139001, z=-3.902 |
| `CoatModel` | x=0.606696, y=967.75775, z=-2.0856857 |
| `DeliveryTube` | x=-0.93926454, y=1.6577972, z=-3.7725186 |
| `Environment` | x=3.3458002, y=0.3169999, z=3.7009997 |
| `Eq_Balance` | x=0.7838999, y=1.2197001, z=-3.8407 |
| `Eq_Beaker_100mL` | x=-0.40210003, y=1.2174001, z=-3.7647996 |
| `Eq_Beaker_500mL` | x=-0.27170008, y=1.2139001, z=-3.902 |
| `Eq_Dropper_1` | x=-0.97346216, y=0.9200598, z=-3.7309382 |
| `Eq_Dropper_2` | x=-0.912, y=0.92, z=-3.735 |
| `Eq_Dropper_3` | x=-0.85282964, y=0.91994256, z=-3.7389145 |
| `Eq_Dropper_4` | x=-0.7951526, y=0.9198878, z=-3.7427316 |
| `Eq_Funnel` | x=0.29159993, y=1.2680383, z=-3.8309999 |
| `Eq_GlassRod` | x=0.42479992, y=1.2202001, z=-3.6913 |
| `Eq_GraduatedCylinder_50mL` | x=-0.77820015, y=1.2207, z=-3.8539 |
| `Eq_PorcelainSpatula` | x=-0.697, y=0.9130976, z=-3.711 |
| `Eq_Scoopula` | x=-0.64155734, y=0.9131, z=-3.7188835 |
| `Eq_TestTubeBrush` | x=-1.0530001, y=1.2291468, z=-3.9480999 |
| `Eq_TestTubeRack` | x=-1.6462, y=1.2174001, z=-3.8309999 |
| `Eq_WashBottle` | x=-0.96990013, y=1.2174001, z=-3.8309999 |
| `Eq_WatchGlass` | x=-0.036800086, y=1.2174001, z=-3.9101 |
| `ErlenmeyerFlask_400mL_2` | x=-0.15130007, y=1.2174001, z=-3.7661 |
| `ErlenmeyerFlask_400mL_3` | x=-0.15130007, y=1.2174001, z=-3.8932 |
| `Experiment_Tube_Table_Kit_Holder_1` | x=-1.832, y=0.91, z=-4.016 |
| `Experiment_Tube_Table_Kit_Holder_2` | x=-1.628, y=0.91, z=-3.884 |
| `Experiment_Tube_Table_Kit_Holder_3` | x=-1.3924, y=0.91, z=-3.847 |
| `Experiment_Tube_Table_Kit_Holder_4` | x=-1.1656, y=0.91, z=-3.846 |
| `Floor (1)` | x=-2.0385463, y=0.11698693, z=-2.9930677 |
| `FlorenceFlask` | x=-0.6760001, y=1.6444, z=-3.842 |
| `FumeHoodModel` | x=-0.331, y=1.175, z=0 |
| `FumeHoodOpenModel` | x=-0, y=0.0233, z=-0.0137 |
| `Funnel_2` | x=0.18209994, y=1.2680383, z=-3.8309999 |
| `GlassRod_2` | x=0.38179994, y=1.2202001, z=-3.6913 |
| `GloveModel_L` | x=0, y=8.441778, z=-0.58333284 |
| `GloveModel_R` | x=0.222222, y=8.441778, z=0.5833348 |
| `GogglesModel` | x=-0.2000001, y=5.2891498, z=-1.1666657 |
| `GraduatedCylinder_50mL_2` | x=-0.85150015, y=1.2207, z=-3.8539 |
| `HandVisual_L` | x=0, y=-0.03, z=-0.06 |
| `HandVisual_R` | x=0, y=-0.03, z=-0.06 |
| `JimenezModel` | x=-0.010523621, y=0.851, z=-0.0013455013 |
| `LabCoatDisplay` | x=4.2400007, y=0.584, z=0.7020546 |
| `Matchstick` | x=1.9600025, y=9.800999, z=14.05 |
| `PlayerAvatar` | x=0, y=0.8539318, z=0 |
| `Prop_burner` | x=-0.32039988, y=0.92640007, z=-3.6808999 |
| `Prop_collection-tube` | x=-1.0173998, y=0.92640007, z=-3.7009003 |
| `Prop_glass-tube` | x=-1.109, y=0.9234, z=-3.6623003 |
| `Prop_reagent-jar` | x=-1.1923999, y=0.92640007, z=-3.6948998 |
| `Raw_Acetaldehyde` | x=-0.2249999, y=-0.08200002, z=-0.23799992 |
| `Raw_Acetone` | x=-0.2249999, y=1.108, z=-0.23799992 |
| `Raw_AcetylChloride` | x=-0.2249999, y=1.108, z=-0.81799984 |
| `Raw_AlcoholicSilverNitrate` | x=-0.2249999, y=1.4580002, z=-0.3829999 |
| `Raw_AmmoniaSolution` | x=-0.30700016, y=0.30799997, z=-0.48599982 |
| `Raw_AmmoniumPhosphate` | x=-0.30700016, y=0.30799997, z=-0.34099984 |
| `Raw_AnhydrousCalciumChloride` | x=-0.30700016, y=0.708, z=-0.6309998 |
| `Raw_Aniline` | x=-0.2249999, y=0.708, z=-0.092999935 |
| `Raw_Aspirin` | x=-0.2249999, y=0.708, z=-0.3829999 |
| `Raw_Benedict'sReagent` | x=-0.2249999, y=1.108, z=-0.092999935 |
| `Raw_Benzaldehyde` | x=-0.2249999, y=0.708, z=0.052000284 |
| `Raw_BenzoicAcid` | x=-0.2249999, y=0.708, z=-0.23799992 |
| `Raw_BenzoylChloride` | x=-0.2249999, y=1.108, z=-0.67299986 |
| `Raw_BenzylAlcohol` | x=-0.2249999, y=0.30799997, z=-0.5279999 |
| `Raw_BleachingPowder` | x=-0.30700016, y=0.708, z=-0.776 |
| `Raw_BromineWater` | x=-0.2249999, y=1.4580002, z=-0.67299986 |
| `Raw_BrownSugar` | x=-0.2249999, y=0.30799997, z=0.052000284 |
| `Raw_CalciumAcetate` | x=-0.30700016, y=0.30799997, z=-0.051 |
| `Raw_ConcentratedHydrochloricAcid` | x=-0.30700016, y=-0.08200002, z=-0.48599982 |
| `Raw_CottonSwabs` | x=3.995, y=0.9409, z=-4.5351 |
| `Raw_DilutedAceticAcid` | x=-0.2249999, y=0.30799997, z=-0.092999935 |
| `Raw_DilutedHydrochloricAcid` | x=-0.30700016, y=-0.08200002, z=-0.19599998 |
| `Raw_DistilledWater` | x=-0.30700016, y=1.108, z=-0.921 |
| `Raw_Ethanol` | x=-0.2249999, y=-0.08200002, z=-0.5279999 |
| `Raw_FerricChloride10%` | x=-0.2249999, y=-0.08200002, z=-0.81799984 |
| `Raw_FilterPaper` | x=3.9974, y=0.9409001, z=-4.3854 |
| `Raw_GlacialAceticAcid` | x=-0.2249999, y=1.108, z=-0.3829999 |
| `Raw_Glycerol` | x=-0.2249999, y=0.30799997, z=-0.3829999 |
| `Raw_HydrochloricAcid0.1N` | x=-0.30700016, y=-0.08200002, z=-0.34099984 |
| `Raw_HydrochloricAcid6N` | x=-0.30700016, y=-0.08200002, z=-0.6309998 |
| `Raw_IceBucket` | x=1.3500006, y=6.595, z=9.700001 |
| `Raw_Limewater` | x=-0.30700016, y=0.30799997, z=-0.19599998 |
| `Raw_LitmusPaper` | x=3.9913478, y=0.93678004, z=-4.7554975 |
| `Raw_Matchsticks` | x=3.9766974, y=0.9409, z=-4.660761 |
| `Raw_Methanol` | x=-0.2249999, y=-0.08200002, z=-0.3829999 |
| `Raw_MixedFruitJuice` | x=-0.2249999, y=0.708, z=-0.67299986 |
| `Raw_Phenol` | x=-0.2249999, y=0.30799997, z=-0.23799992 |
| `Raw_PotassiumDichromate` | x=-0.30700016, y=0.708, z=-0.48599982 |
| `Raw_PotassiumHydroxide10%` | x=-0.30700016, y=0.30799997, z=-0.776 |
| `Raw_PotassiumIodide10%` | x=-0.30700016, y=0.708, z=-0.34099984 |
| `Raw_PotassiumPermanganate0.1%` | x=-0.30700016, y=0.708, z=-0.051 |
| `Raw_PropylAlcohol` | x=-0.2249999, y=0.30799997, z=-0.67299986 |
| `Raw_SalicylicAcid` | x=-0.2249999, y=0.708, z=-0.5279999 |
| `Raw_Schiff'sReagent` | x=-0.2249999, y=1.4580002, z=-0.81799984 |
| `Raw_SilverNitrate` | x=-0.2249999, y=1.4580002, z=-0.5279999 |
| `Raw_SodiumAcetate` | x=-0.30700016, y=0.708, z=-0.921 |
| `Raw_SodiumBicarbonate10%` | x=-0.30700016, y=0.30799997, z=-0.6309998 |
| `Raw_SodiumBisulfite` | x=-0.2249999, y=-0.08200002, z=-0.67299986 |
| `Raw_SodiumHydroxide10%` | x=-0.30700016, y=-0.08200002, z=-0.051 |
| `Raw_SodiumHydroxide6N` | x=-0.30700016, y=0.30799997, z=-0.921 |
| `Raw_SodiumHypochlorite` | x=-0.30700016, y=0.708, z=-0.19599998 |
| `Raw_SodiumNitrite` | x=-0.2249999, y=1.108, z=-0.5279999 |
| `Raw_SulfuricAcid` | x=-0.30700016, y=-0.08200002, z=-0.921 |
| `Raw_SulfuricAcid6N` | x=-0.30700016, y=-0.08200002, z=-0.776 |
| `Raw_Tollen'sReagent` | x=-0.2249999, y=1.108, z=0.052000284 |
| `Raw_Yeast` | x=-0.2249999, y=0.708, z=-0.81799984 |
| `Raw_n-ButylAlcohol` | x=-0.2249999, y=-0.08200002, z=-0.092999935 |
| `Raw_sec-ButylAlcohol` | x=-0.2249999, y=-0.08200002, z=0.052000284 |
| `Raw_tert-ButylAlcohol` | x=-0.2249999, y=0.30799997, z=-0.81799984 |
| `RubberStopper` | x=-82.69, y=16.32, z=41.99 |
| `RubberStopper_2` | x=-82.63, y=16.21, z=45.95 |
| `Table_2 (2)` | x=8.285, y=-2.204298, z=2.755 |
| `Template_Raw_CottonSwabs` | x=4.1963134, y=2.041, z=-2.94 |
| `Template_Raw_LitmusPaper` | x=4.1963134, y=2.04, z=-3.23 |
| `Template_Raw_Matchsticks` | x=4.1963134, y=2.0419998, z=-3.085 |
| `TestTubeRack_2` | x=-1.4212, y=1.2174001, z=-3.8309999 |
| `TestTubeRack_3` | x=-1.6418998, y=1.564, z=-3.8309999 |
| `TestTubeRack_4` | x=-1.1967999, y=1.564, z=-3.8309999 |
| `TestTubeRack_5` | x=-1.4167, y=1.564, z=-3.8309999 |
| `TunnelingVignette` | x=0, y=0, z=0 |
| `Wall` | x=3.7799997, y=-2.235, z=3.1115642 |
| `WatchGlass_2` | x=-0.031200081, y=1.2174001, z=-3.754 |
| `WatchModel` | x=0, y=0, z=0 |
| `WaterBath` | x=-1.1946001, y=1.245, z=-3.8115 |
| `XR Device Simulator` | x=0, y=0, z=0 |
| `XR Origin (XR Rig)` | x=-1.354, y=0.088, z=2.226 |

---

## MainMenu (the cube room)
<sub>`Assets/Scenes/MainMenu.unity`</sub>

### Root objects (11)

| Object | local Y |
|---|---|
| `Directional Light` | 0.000 |
| `EventSystem` | 0.000 |
| `Main Camera` | 1.500 |
| `MainMenuController` | 0.000 |
| `MenuCubeRoom` | 0.000 |
| `MenuMusicPlayer` | 0.000 |
| `MenuRoom` | 0.000 |
| `ScreenFader` | 0.000 |
| `Services` | 0.000 |
| `SpawnVFX` | 0.000 |
| `~RoomFx` | 0.000 |

### Prefab instances (4)

| Instance | position override |
|---|---|
| `HandVisual_L` | x=0, y=-0.03, z=-0.06 |
| `HandVisual_R` | x=0, y=-0.03, z=-0.06 |
| `XR Device Simulator` | x=0, y=0, z=0 |
| `XR Origin (XR Rig)` | x=-0.00000017484555, y=0, z=0.5 |
