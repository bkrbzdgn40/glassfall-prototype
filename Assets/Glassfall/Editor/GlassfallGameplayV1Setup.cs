#if UNITY_EDITOR
using Glassfall.Gameplay.Builds;
using Glassfall.Gameplay.Combat;
using Glassfall.Gameplay.Droplet;
using Glassfall.Gameplay.Encounter;
using Glassfall.Gameplay.Link;
using Glassfall.Gameplay.Run;
using Glassfall.Gameplay.Rewards;
using Glassfall.Gameplay.Surface;
using Glassfall.Gameplay.Wetness;
using Glassfall.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Glassfall.EditorTools
{
    public static class GlassfallGameplayV1Setup
    {
        private const string RootName = "GameplayV1";
        private const string LinkName = "SplitLinkSystem";
        private const string ResonanceName = "ResonanceNode";
        private const string HunterName = "ArcHunter";
        private const string BuildChoiceName = "MicroBuildChoiceV1";
        private const string BroadArcHaloName = "BroadArcDamageHalo";
        private const string MicroRunName = "MicroRunV1";
        private const string NeutralSpawnerName = "NeutralDropletSpawner";
        private const string NeutralSpawnAreaName = "NeurtalSpawnArea";

        [MenuItem("Tools/Glassfall/Gameplay V1/1 - Setup Resonance Node V1")]
        public static void SetupResonanceNode()
        {
            if (!TryGetRequiredSceneObjects(
                    out GameObject arena,
                    out DropletSplitSystem splitSystem))
            {
                return;
            }

            GameObject root = EnsureChild(arena, RootName);
            SplitLinkSystem linkSystem = EnsureLinkSystem(root, splitSystem);
            ResonanceNode node = EnsureResonanceNode(root, linkSystem);

            MarkDirtyAndSelect(node.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Resonance Node V1",
                "Kurulum tamamlandı.\n\n" +
                "Hierarchy: Arena > GameplayV1 altında SplitLinkSystem ve " +
                "ResonanceNode oluşturuldu.\n\n" +
                "Ctrl+S ile sahneyi kaydet. Play'e bas, SPACE ile split ol " +
                "ve çizgiyi node'un üzerinden yaklaşık 0.8 saniye geçir.",
                "Tamam");
        }

        [MenuItem("Tools/Glassfall/Gameplay V1/2 - Setup Arc Hunter V1")]
        public static void SetupArcHunter()
        {
            if (!TryGetRequiredSceneObjects(
                    out GameObject arena,
                    out DropletSplitSystem splitSystem))
            {
                return;
            }

            GameObject root = EnsureChild(arena, RootName);
            SplitLinkSystem linkSystem = EnsureLinkSystem(root, splitSystem);
            EnsureResonanceNode(root, linkSystem);
            ArcHunter hunter = EnsureArcHunter(root, linkSystem);
            EnsureCombat(root, linkSystem, hunter);

            MarkDirtyAndSelect(hunter.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Arc Hunter V1",
                "Combat kurulumu tamamlandı.\n\n" +
                "ArcHunter, SplitLinkCombat ve gerekli referanslar otomatik " +
                "bağlandı.\n\n" +
                "Ctrl+S ile kaydet. Play'e bas. Merged halde düşman hasar " +
                "almamalı. SPACE ile split olduktan sonra düşmanı link'in " +
                "ORTA kısmıyla kes.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/3 - Enable Wetness x Link Combat V1")]
        public static void SetupWetnessLinkCombat()
        {
            if (!TryGetRequiredSceneObjects(
                    out GameObject arena,
                    out DropletSplitSystem splitSystem))
            {
                return;
            }

            WetnessField wetnessField =
                Object.FindAnyObjectByType<WetnessField>();
            if (wetnessField == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Wetness x Link kurulamadı",
                    "Scene içinde WetnessField bulunamadı.\n\n" +
                    "Prototype scene'in doğru ve güncel olduğundan emin ol.",
                    "Tamam");
                return;
            }

            GameObject root = EnsureChild(arena, RootName);
            SplitLinkSystem linkSystem = EnsureLinkSystem(root, splitSystem);
            EnsureResonanceNode(root, linkSystem);
            ArcHunter hunter = EnsureArcHunter(root, linkSystem);
            SplitLinkCombat combat = EnsureCombat(root, linkSystem, hunter);

            Undo.RecordObject(combat, "Enable Wetness x Link Combat V1");
            combat.ConfigureWetness(wetnessField);
            EditorUtility.SetDirty(combat);

            MarkDirtyAndSelect(hunter.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Wetness x Link Combat V1",
                "Kurulum tamamlandı.\n\n" +
                "SplitLinkCombat artık CPU WetnessField'i kullanıyor.\n\n" +
                "Ctrl+S ile sahneyi kaydet ve Play Mode testine geç.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/4 - Setup Micro Build Choice V1")]
        public static void SetupMicroBuildChoice()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out WetnessField wetnessField))
            {
                return;
            }

            Undo.RecordObject(combat, "Configure Micro Build Choice V1");
            combat.ConfigureWetness(wetnessField);
            combat.ResetRuntimeBuildTuning();
            EditorUtility.SetDirty(combat);

            GameObject choiceObject = EnsureChild(root, BuildChoiceName);
            MicroBuildChoiceController controller =
                GetOrAddComponent<MicroBuildChoiceController>(choiceObject);
            controller.Configure(combat);

            MicroBuildChoiceOverlay overlay =
                GetOrAddComponent<MicroBuildChoiceOverlay>(choiceObject);
            overlay.Configure(controller);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(overlay);
            MarkDirtyAndSelect(choiceObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Micro Build Choice V1",
                "Kurulum tamamlandı.\n\n" +
                "Ctrl+S ile sahneyi kaydet.\n\n" +
                "Play Mode:\n" +
                "1 = Conductive Focus\n" +
                "2 = Broad Arc\n" +
                "3 = Capacitor\n\n" +
                "Her build'i ayrı Play session'da test et.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/5 - Setup Build Readability Polish V1")]
        public static void SetupBuildReadabilityPolish()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out _))
            {
                return;
            }

            MicroBuildChoiceController controller =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            MicroBuildChoiceOverlay overlay =
                Object.FindAnyObjectByType<MicroBuildChoiceOverlay>();
            SplitLinkVisual linkVisual =
                Object.FindAnyObjectByType<SplitLinkVisual>();
            ArcHunterVisual hunterVisual =
                Object.FindAnyObjectByType<ArcHunterVisual>();

            if (controller == null ||
                overlay == null ||
                linkVisual == null ||
                hunterVisual == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Build Readability kurulamadı",
                    "Micro Build Choice V1 presentation bağımlılıkları " +
                    "bulunamadı.\n\n" +
                    "Önce 4 - Setup Micro Build Choice V1 menüsünü çalıştır.",
                    "Tamam");
                return;
            }

            GameObject haloObject =
                EnsureChild(linkVisual.gameObject, BroadArcHaloName);
            LineRenderer halo =
                GetOrAddComponent<LineRenderer>(haloObject);

            Undo.RecordObject(
                linkVisual,
                "Configure Build Readability Polish V1");
            Undo.RecordObject(
                hunterVisual,
                "Configure Build Readability Polish V1");
            Undo.RecordObject(
                overlay,
                "Configure Build Readability Polish V1");

            linkVisual.ConfigureReadability(
                controller,
                combat,
                halo);
            hunterVisual.ConfigureReadability(
                controller,
                combat);
            overlay.Configure(controller);

            EditorUtility.SetDirty(linkVisual);
            EditorUtility.SetDirty(hunterVisual);
            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(halo);

            MarkDirtyAndSelect(controller.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Build Readability Polish V1",
                "Kurulum tamamlandı.\n\n" +
                "Ctrl+S ile sahneyi kaydet.\n\n" +
                "Conductive Focus: wet hedefte cyan pulse güçlenir.\n" +
                "Broad Arc: gerçek trimmed damage bandı geniş halo olarak " +
                "görünür.\n" +
                "Capacitor: link charge oldukça sarı/beyaz tona yaklaşır ve " +
                "kalınlaşır.\n\n" +
                "Seçili build ve aktif durumu sol üstte kalıcı görünür.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/6 - Setup Micro Run V1")]
        public static void SetupMicroRun()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out _))
            {
                return;
            }

            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();

            if (buildController == null ||
                resonanceNode == null ||
                hunter == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Micro Run kurulamadı",
                    "Build choice, Resonance Node veya Arc Hunter bulunamadı.\n\n" +
                    "Önce 1-5 numaralı Gameplay V1 kurulumlarının mevcut " +
                    "sahnede tamamlandığından emin ol.",
                    "Tamam");
                return;
            }

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);
            run.Configure(
                buildController,
                resonanceNode,
                hunter,
                combat);

            MicroRunOverlay overlay =
                GetOrAddComponent<MicroRunOverlay>(runObject);
            overlay.Configure(run, buildController);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(overlay);

            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Micro Run V1",
                "Kurulum tamamlandı.\n\n" +
                "Ctrl+S ile sahneyi kaydet.\n\n" +
                "Play Mode akışı:\n" +
                "1. Build seç.\n" +
                "2. Resonance Node'u link ile charge et.\n" +
                "3. Üç Arc Hunter encounter'ını sırayla temizle.\n" +
                "4. Run Complete ekranını gör.\n\n" +
                "Aynı Arc Hunter yeniden kullanılır; yeni enemy framework " +
                "veya wave sistemi eklenmedi.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/7 - Setup Encounter Variation V1")]
        public static void SetupEncounterVariation()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out WetnessField wetnessField))
            {
                return;
            }

            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter hunter = Object.FindAnyObjectByType<ArcHunter>();

            if (buildController == null || resonanceNode == null ||
                hunter == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Encounter Variation kurulamadı",
                    "Build choice, Resonance Node veya Arc Hunter bulunamadı.\n\n" +
                    "Önce 1-6 numaralı Gameplay V1 kurulumlarını mevcut " +
                    "sahnede tamamla.",
                    "Tamam");
                return;
            }

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);
            run.Configure(
                buildController,
                resonanceNode,
                hunter,
                combat);
            run.ConfigureEncounterVariation(new[]
            {
                new Vector2(-4.0f, 2.5f),
                new Vector2(4.0f, 2.5f),
                new Vector2(0.0f, -3.0f)
            });
            run.ConfigureEncounterMovementModes(new[]
            {
                ArcHunterMovementMode.Chase,
                ArcHunterMovementMode.Strafe,
                ArcHunterMovementMode.WetRoutePressure
            });

            hunter.ConfigureWetness(wetnessField);

            MicroRunOverlay overlay =
                GetOrAddComponent<MicroRunOverlay>(runObject);
            overlay.Configure(run, buildController);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(hunter);
            EditorUtility.SetDirty(overlay);
            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Encounter Variation V1",
                "Kurulum tamamlandı.\n\n" +
                "Encounter 1: Arc Hunter / Chase\n" +
                "Encounter 2: Strafe Hunter\n" +
                "Encounter 3: Wet-Route Hunter\n\n" +
                "Aynı ArcHunter yeniden kullanılır; yeni enemy framework, " +
                "projectile veya wave sistemi eklenmedi.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/8 - Setup Mass Economy + Neutral Spawn V1")]
        public static void SetupMassEconomyNeutralSpawn()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out _))
            {
                return;
            }

            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter hunter = Object.FindAnyObjectByType<ArcHunter>();

            GameObject arena = GameObject.Find("Arena");
            if (splitSystem == null || buildController == null ||
                resonanceNode == null || hunter == null || arena == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mass Economy kurulamadı",
                    "Micro Run veya player bağımlılıkları bulunamadı.\n\n" +
                    "Önce 1-7 numaralı Gameplay V1 kurulumlarını tamamla.",
                    "Tamam");
                return;
            }

            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Glassfall/Gameplay/Droplet/Prefabs/NeutralDroplet.prefab");
            NeutralDroplet prefab = prefabObject != null
                ? prefabObject.GetComponent<NeutralDroplet>()
                : null;
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mass Economy kurulamadı",
                    "NeutralDroplet prefab bulunamadı.",
                    "Tamam");
                return;
            }

            DropletMergeSystem mergeSystem =
                splitSystem.GetComponent<DropletMergeSystem>();
            if (mergeSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mass Economy kurulamadı",
                    "Droplet üzerinde DropletMergeSystem bulunamadı.",
                    "Tamam");
                return;
            }

            NeutralDropletSpawner spawner =
                Object.FindAnyObjectByType<NeutralDropletSpawner>();
            if (spawner == null)
            {
                GameObject spawnerObject = EnsureChild(arena, NeutralSpawnerName);
                spawner = GetOrAddComponent<NeutralDropletSpawner>(spawnerObject);
            }

            GameObject areaObject = GameObject.Find(NeutralSpawnAreaName);
            if (areaObject == null)
                areaObject = GameObject.Find("NeutralSpawnArea");
            if (areaObject == null)
            {
                areaObject = EnsureChild(arena, "NeutralSpawnArea");
                BoxCollider2D createdArea =
                    GetOrAddComponent<BoxCollider2D>(areaObject);
                createdArea.isTrigger = true;
                createdArea.size = new Vector2(22f, 12f);
                EditorUtility.SetDirty(createdArea);
            }

            Collider2D spawnArea = areaObject.GetComponent<Collider2D>();
            if (spawnArea == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mass Economy kurulamadı",
                    "Neutral spawn area üzerinde Collider2D bulunamadı.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(mergeSystem, "Configure Mass Economy V1");
            mergeSystem.ConfigureNeutralCollection(
                NeutralCollectionRules.DefaultMergedMultiplier,
                NeutralCollectionRules.DefaultSplitMultiplier);

            Undo.RecordObject(spawner, "Configure Mass Economy Neutral Spawn V1");
            spawner.ConfigureRunSpawning(
                prefab,
                spawnArea,
                splitSystem,
                initialCount: 10,
                targetAlive: 10,
                maxAlive: 14,
                budget: 30,
                refillInterval: 2.5f,
                playerDistance: 1.5f,
                spacing: 0.35f,
                controlledByRun: true,
                randomizeSeed: true);

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);
            run.Configure(buildController, resonanceNode, hunter, combat);
            run.ConfigureMassEconomy(spawner);

            MicroRunOverlay overlay =
                GetOrAddComponent<MicroRunOverlay>(runObject);
            overlay.Configure(run, buildController);
            overlay.ConfigureMass(splitSystem, mergeSystem);

            EditorUtility.SetDirty(mergeSystem);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(overlay);
            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Mass Economy + Neutral Spawn V1",
                "Kurulum tamamlandı.\n\n" +
                "Merged form neutral mass'in %100'ünü toplar.\n" +
                "Split form neutral mass'in %60'ını toplar.\n" +
                "Run başında 10 neutral hedef popülasyonu kurulur.\n" +
                "Build seçiminden Run Complete'a kadar Resonance, combat ve " +
                "intermission boyunca hedefin altına düşerse yaklaşık 2.5 " +
                "saniyede bir yalnız 1 neutral yenilenir.\n" +
                "Hard cap 14, run spawn budget 30'dur.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/9 - Setup Mid-Run Reward Choice V1")]
        public static void SetupMidRunRewardChoice()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out _))
            {
                return;
            }

            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();

            if (splitSystem == null ||
                buildController == null ||
                resonanceNode == null ||
                hunter == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mid-Run Reward kurulamadı",
                    "Micro Run veya player bağımlılıkları bulunamadı.\n\n" +
                    "Önce 1-8 numaralı Gameplay V1 kurulumlarını tamamla.",
                    "Tamam");
                return;
            }

            DropletMergeSystem mergeSystem =
                splitSystem.GetComponent<DropletMergeSystem>();
            if (mergeSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mid-Run Reward kurulamadı",
                    "Droplet üzerinde DropletMergeSystem bulunamadı.",
                    "Tamam");
                return;
            }

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);
            run.Configure(
                buildController,
                resonanceNode,
                hunter,
                combat);

            MidRunRewardController rewards =
                GetOrAddComponent<MidRunRewardController>(runObject);
            rewards.Configure(splitSystem, mergeSystem, combat);
            run.ConfigureRewards(rewards);

            MicroRunOverlay runOverlay =
                GetOrAddComponent<MicroRunOverlay>(runObject);
            runOverlay.Configure(run, buildController);
            runOverlay.ConfigureMass(splitSystem, mergeSystem);
            runOverlay.ConfigureRewards(rewards);

            MidRunRewardOverlay rewardOverlay =
                GetOrAddComponent<MidRunRewardOverlay>(runObject);
            rewardOverlay.Configure(run, rewards);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(rewards);
            EditorUtility.SetDirty(runOverlay);
            EditorUtility.SetDirty(rewardOverlay);
            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Mid-Run Reward Choice V1",
                "Kurulum tamamlandı.\n\n" +
                "Encounter 1 sonrası:\n" +
                "1 = Efficient Division (%10 -> %7 split cost)\n" +
                "2 = Absorbent Core (%100 -> %125 merged collection)\n\n" +
                "Encounter 2 sonrası:\n" +
                "1 = Stable Link (daha uzun central damage segment)\n" +
                "2 = Conductive Residue (0.15 -> 0.10 conductivity threshold)\n\n" +
                "Reward seçilmeden sonraki encounter başlamaz.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/10 - Setup Merged Form Identity V1")]
        public static void SetupMergedFormIdentity()
        {
            if (!TryGetBuildDependencies(
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();

            if (splitSystem == null || hunter == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Merged Form Identity kurulamadı",
                    "Player veya ArcHunter bulunamadı.\n\n" +
                    "Önce mevcut Gameplay V1 kurulumlarının sahnede " +
                    "bulunduğundan emin ol.",
                    "Tamam");
                return;
            }

            MergedFormIdentity identity =
                GetOrAddComponent<MergedFormIdentity>(splitSystem.gameObject);

            Undo.RecordObject(identity, "Configure Merged Form Identity V1");
            identity.Configure(
                hunter,
                attractionRadius: 1.25f,
                attractionSpeed: 1.6f,
                bodyShoveSpeed: 3.2f,
                bodyShoveMassExponent: 0.35f,
                bodyShoveMaxMultiplier: 1.75f);

            EditorUtility.SetDirty(identity);
            MarkDirtyAndSelect(splitSystem.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Merged Form Identity V1",
                "Kurulum tamamlandı.\n\n" +
                "Merged halde:\n" +
                "- Yakındaki neutral dropletler kısa menzilden hafifçe çekilir.\n" +
                "- ArcHunter ile body contact enemy'yi iter.\n" +
                "- Daha yüksek mass shove'u sınırlı biçimde güçlendirir.\n\n" +
                "Split halde attraction ve body shove tamamen kapanır.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/11 - Setup Mass Shear + Enemy Threat V1")]
        public static void SetupMassShearEnemyThreat()
        {
            if (!TryGetBuildDependencies(
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();
            NeutralDropletSpawner spawner =
                Object.FindAnyObjectByType<NeutralDropletSpawner>();

            if (splitSystem == null || hunter == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mass Shear kurulamadı",
                    "Player veya ArcHunter bulunamadı.\n\n" +
                    "Önce mevcut Gameplay V1 kurulumlarının sahnede " +
                    "bulunduğundan emin ol.",
                    "Tamam");
                return;
            }

            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Glassfall/Gameplay/Droplet/Prefabs/NeutralDroplet.prefab");
            NeutralDroplet fragmentPrefab = prefabObject != null
                ? prefabObject.GetComponent<NeutralDroplet>()
                : null;
            if (fragmentPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Mass Shear kurulamadı",
                    "NeutralDroplet prefab bulunamadı.",
                    "Tamam");
                return;
            }

            MassShearSystem shear =
                GetOrAddComponent<MassShearSystem>(hunter.gameObject);

            Undo.RecordObject(shear, "Configure Mass Shear + Enemy Threat V1");
            shear.Configure(
                splitSystem,
                fragmentPrefab,
                spawner != null ? spawner.transform : null,
                mergedLoss: 0.5f,
                splitLoss: 1f,
                cooldownSeconds: 0.85f,
                contactExtraPadding: 0.16f,
                minimumMassAfterHit: 1f,
                playerKnockbackSpeed: 2.25f,
                pickupLockSeconds: 0.55f);

            EditorUtility.SetDirty(shear);
            MarkDirtyAndSelect(hunter.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Mass Shear + Enemy Threat V1",
                "Kurulum tamamlandı.\n\n" +
                "ArcHunter teması artık gerçek pressure üretir:\n" +
                "- Merged hit yaklaşık 0.5 mass koparır.\n" +
                "- Split child hit yaklaşık 1.0 mass koparır.\n" +
                "- Hit küçük knockback uygular.\n" +
                "- Kopan mass recoverable fragment olarak sahneye düşer.\n" +
                "- Recovery fragment geri alındığında kendi mass değerinin " +
                "%100'ünü verir.\n" +
                "- Tek ArcHunter cooldown'u iki split child'ın aynı anda " +
                "double-shear yemesini engeller.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/12 - Setup Surface Tension V1")]
        public static void SetupSurfaceTension()
        {
            if (!TryGetBuildDependencies(
                    out _,
                    out SplitLinkCombat combat,
                    out _))
            {
                return;
            }

            SplitLinkVisual visual =
                Object.FindAnyObjectByType<SplitLinkVisual>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            if (visual == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Surface Tension kurulamadı",
                    "SplitLinkVisual bulunamadı.\n\n" +
                    "Önce mevcut Gameplay V1 kurulumlarının sahnede " +
                    "bulunduğundan emin ol.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(combat, "Configure Surface Tension V1");
            combat.ConfigureSurfaceTension(
                closeDistance: 1.5f,
                stableDistance: 5.5f,
                fullPenaltyAtDistance: 9f,
                minimumDamageMultiplier: 0.45f);

            Undo.RecordObject(visual, "Configure Surface Tension Visual V1");
            visual.ConfigureSurfaceTension(
                minimumWidthMultiplier: 0.45f,
                wobbleAmplitude: 0.08f,
                wobbleSpeed: 18f);

            if (splitSystem != null)
            {
                Undo.RecordObject(
                    splitSystem,
                    "Configure Split Separation Limit");
                splitSystem.ConfigureSeparationLimit(
                    resistanceStartDistance: 8f,
                    maximumDistance: 10f);
                EditorUtility.SetDirty(splitSystem);
            }

            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(visual);
            MarkDirtyAndSelect(visual.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Surface Tension V1",
                "Kurulum tamamlandı.\n\n" +
                "Split separation artık üç okunur bölgeye sahiptir:\n" +
                "- CLOSE: kısa coverage, normal damage efficiency.\n" +
                "- STABLE: 1.5 - 5.5 arası normal/full efficiency.\n" +
                "- OVERSTRETCHED: 5.5 sonrasında link giderek incelir, " +
                "gerilir ve damage efficiency düşer.\n" +
                "- 9.0 separation civarında tension damage multiplier " +
                "yaklaşık x0.45 tabanına ulaşır.\n\n" +
                "8.0 sonrasında outward hareket kademeli direnç görür; " +
                "10.0 separation konservatif fiziksel üst sınırdır.\n" +
                "Elastic Membrane bu aralığı mevcut tension değerleri kadar " +
                "genişletir; inward ve tangential hareket korunur.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/13 - Setup Wet Trail Lifecycle V1")]
        public static void SetupWetTrailLifecycle()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Wet Trail Lifecycle kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            WetnessField wetnessField =
                Object.FindAnyObjectByType<WetnessField>();
            WetnessVisualRenderer visual =
                Object.FindAnyObjectByType<WetnessVisualRenderer>();

            if (wetnessField == null || visual == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Wet Trail Lifecycle kurulamadı",
                    "WetnessField veya WetnessVisualRenderer bulunamadı.\n\n" +
                    "Mevcut wetness/trail kurulumunun sahnede olduğundan emin ol.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(
                wetnessField,
                "Configure Wet Trail Lifecycle V1");
            wetnessField.ConfigureLifecycle(newDecayRate: 0.11f);

            Undo.RecordObject(
                visual,
                "Configure Wet Trail Lifecycle Visual V1");
            visual.ConfigureLifecycle(
                newFadedThreshold: 0.30f,
                newFreshThreshold: 0.65f,
                newFadedOpacityMultiplier: 0.32f,
                newFreshHighlightMultiplier: 1.25f);

            EditorUtility.SetDirty(wetnessField);
            EditorUtility.SetDirty(visual);
            MarkDirtyAndSelect(visual.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Wet Trail Lifecycle V1",
                "Kurulum tamamlandı.\n\n" +
                "Wetness artık tek scalar gameplay truth olarak kalır; " +
                "ayrı state machine yoktur.\n\n" +
                "- FRESH: wetness >= 0.65, daha parlak/okunur trail.\n" +
                "- AGING: 0.30 - 0.65 arası doğal geçiş.\n" +
                "- FADED: 0 - 0.30 arası görsel daha hızlı söner.\n" +
                "- DRY: wetness 0 olduğunda tamamen kaybolur.\n\n" +
                "Gameplay decay rate 0.11/s olarak ayarlandı. " +
                "Full wetness yaklaşık 9.1 saniyede tamamen kurur; " +
                "0.15 conductivity threshold yaklaşık 7.7 saniye korunur " +
                "(trail yenilenmezse).\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }


        [MenuItem(
            "Tools/Glassfall/Gameplay V1/14 - Setup Reward Choice V2")]
        public static void SetupRewardChoiceV2()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out WetnessField wetnessField))
            {
                return;
            }

            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();

            if (splitSystem == null ||
                buildController == null ||
                resonanceNode == null ||
                hunter == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Choice V2 kurulamadı",
                    "Micro Run veya player bağımlılıkları bulunamadı.",
                    "Tamam");
                return;
            }

            DropletMergeSystem mergeSystem =
                splitSystem.GetComponent<DropletMergeSystem>();
            MergedFormIdentity identity =
                splitSystem.GetComponent<MergedFormIdentity>();
            MassShearSystem shear =
                hunter.GetComponent<MassShearSystem>();

            if (mergeSystem == null ||
                identity == null ||
                shear == null ||
                wetnessField == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Choice V2 kurulamadı",
                    "Merged Form Identity, Mass Shear veya Wet Trail " +
                    "Lifecycle bağımlılığı eksik.\n\n" +
                    "Önce 10, 11, 12 ve 13 numaralı kurulumların sahnede " +
                    "bulunduğundan emin ol.",
                    "Tamam");
                return;
            }

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);
            run.Configure(
                buildController,
                resonanceNode,
                hunter,
                combat);

            MidRunRewardController rewards =
                GetOrAddComponent<MidRunRewardController>(runObject);
            Undo.RecordObject(rewards, "Configure Reward Choice V2");
            rewards.Configure(
                splitSystem,
                mergeSystem,
                combat,
                identity,
                shear,
                wetnessField);
            run.ConfigureRewards(rewards);

            MicroRunOverlay runOverlay =
                GetOrAddComponent<MicroRunOverlay>(runObject);
            runOverlay.Configure(run, buildController);
            runOverlay.ConfigureMass(splitSystem, mergeSystem);
            runOverlay.ConfigureRewards(rewards);

            MidRunRewardOverlay rewardOverlay =
                GetOrAddComponent<MidRunRewardOverlay>(runObject);
            rewardOverlay.Configure(run, rewards);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(rewards);
            EditorUtility.SetDirty(runOverlay);
            EditorUtility.SetDirty(rewardOverlay);
            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Reward Choice V2",
                "Kurulum tamamlandı.\n\n" +
                "Encounter 1 sonrası üç seçenek:\n" +
                "1 = Gravitational Drop (merged attraction radius +35%)\n" +
                "2 = Efficient Division (split cost %10 -> %7)\n" +
                "3 = Slow Evaporation (wet trail decay -25%)\n\n" +
                "Encounter 2 sonrası üç seçenek:\n" +
                "1 = Dense Core (merged body shove +25%)\n" +
                "2 = High Cohesion (merged mass shear -25%)\n" +
                "3 = Elastic Membrane (stable link range +20%)\n\n" +
                "Eski reward enum/değerleri backwards compatibility için " +
                "korunur fakat V2 seçim havuzunda gösterilmez.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }


        [MenuItem(
            "Tools/Glassfall/Gameplay V1/15 - Setup Reward Decision Freeze V1")]
        public static void SetupRewardDecisionFreezeV1()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out WetnessField wetnessField))
            {
                return;
            }

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);
            MidRunRewardController rewards =
                GetOrAddComponent<MidRunRewardController>(runObject);
            NeutralDropletSpawner spawner =
                Object.FindAnyObjectByType<NeutralDropletSpawner>();

            if (spawner == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Decision Freeze kurulamadı",
                    "NeutralDropletSpawner bulunamadı. Önce mevcut Micro Run / " +
                    "Mass Economy kurulumunun sahnede olduğundan emin ol.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(
                rewards,
                "Configure Reward Decision Freeze V1");
            rewards.ConfigureDecisionFreeze(true);

            Undo.RecordObject(
                run,
                "Configure Reward Decision Freeze Run Policy");
            run.ConfigureRewards(rewards);
            run.ConfigureMassEconomy(spawner);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(rewards);
            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Reward Decision Freeze V1",
                "Kurulum tamamlandı.\n\n" +
                "Reward Choice sırasında gameplay tamamen pause edilir:\n" +
                "- player / split movement ve mevcut physics motion durur\n" +
                "- enemy movement ve link damage durur\n" +
                "- trail deposition / wetness decay durur\n" +
                "- mass shear ve neutral collection durur\n" +
                "- neutral refill reward ekranında kapalıdır\n\n" +
                "1 / 2 / 3 veya kart click UI input'u çalışmaya devam eder. " +
                "Seçim yapılınca önceki time scale geri yüklenir ve run devam eder.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/16 - Setup Micro Run V2")]
        public static void SetupMicroRunV2()
        {
            if (!TryGetBuildDependencies(
                    out GameObject root,
                    out SplitLinkCombat combat,
                    out _))
            {
                return;
            }

            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            NeutralDropletSpawner spawner =
                Object.FindAnyObjectByType<NeutralDropletSpawner>();
            MidRunRewardController rewards =
                Object.FindAnyObjectByType<MidRunRewardController>();

            if (buildController == null ||
                resonanceNode == null ||
                hunter == null ||
                splitSystem == null ||
                spawner == null ||
                rewards == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Micro Run V2 kurulamadı",
                    "Build, Resonance, Arc Hunter, player, neutral spawner veya " +
                    "reward sistemi eksik.\n\n" +
                    "Mevcut Gameplay V1 kurulumlarının sahnede olduğundan " +
                    "emin ol.",
                    "Tamam");
                return;
            }

            DropletMergeSystem mergeSystem =
                splitSystem.GetComponent<DropletMergeSystem>();
            if (mergeSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Micro Run V2 kurulamadı",
                    "DropletMergeSystem bulunamadı.",
                    "Tamam");
                return;
            }

            GameObject runObject = EnsureChild(root, MicroRunName);
            MicroRunController run =
                GetOrAddComponent<MicroRunController>(runObject);

            Undo.RecordObject(run, "Configure Micro Run V2");
            run.Configure(
                buildController,
                resonanceNode,
                hunter,
                combat);
            run.ConfigureMassEconomy(spawner);
            run.ConfigureRewards(rewards);
            run.ConfigureMicroRunV2(
                splitSystem,
                preparationGain: 2.0f,
                preparationTimeout: 4.0f,
                recoveryGain: 3.5f,
                recoveryTimeout: 5.0f,
                intermissionDuration: 1.0f);

            MicroRunOverlay overlay =
                GetOrAddComponent<MicroRunOverlay>(runObject);
            Undo.RecordObject(overlay, "Configure Micro Run V2 Overlay");
            overlay.Configure(run, buildController);
            overlay.ConfigureMass(splitSystem, mergeSystem);
            overlay.ConfigureRewards(rewards);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(overlay);
            MarkDirtyAndSelect(runObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Micro Run V2",
                "Kurulum tamamlandı.\n\n" +
                "Yeni run pacing:\n" +
                "BUILD -> PREP (+2 mass veya 4s) -> RESONANCE -> " +
                "ENCOUNTER 1 -> REWARD -> RECOVERY (+3.5 mass veya 5s) -> " +
                "ENCOUNTER 2 -> REWARD -> RECOVERY -> FINAL PRESSURE -> " +
                "RUN COMPLETE.\n\n" +
                "Prep/recovery sırasında enemy ve link combat kapalıdır; " +
                "neutral refill açıktır. Merged harvest doğal olarak daha " +
                "verimli kalır. Hedef mass bulunamazsa timeout run'ı " +
                "soft-lock olmadan ilerletir.\n\n" +
                "Reward Decision Freeze korunur ve run timer artık reward " +
                "ekranında ilerlemez.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }


        private static bool TryGetBuildDependencies(
            out GameObject root,
            out SplitLinkCombat combat,
            out WetnessField wetnessField)
        {
            root = null;
            combat = null;
            wetnessField = null;

            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Gameplay V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini " +
                    "aç.\n\nBu kurulum yalnız Prototype scene üzerinde çalışır.",
                    "Tamam");
                return false;
            }

            GameObject arena = GameObject.Find("Arena");
            root = arena != null
                ? arena.transform.Find(RootName)?.gameObject
                : null;
            SplitLinkSystem linkSystem =
                Object.FindAnyObjectByType<SplitLinkSystem>();
            combat =
                Object.FindAnyObjectByType<SplitLinkCombat>();
            wetnessField =
                Object.FindAnyObjectByType<WetnessField>();

            if (arena == null || root == null || linkSystem == null ||
                combat == null || wetnessField == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Gameplay V1 kurulamadı",
                    "Gerekli gameplay bağımlılıkları bulunamadı.\n\n" +
                    "Önce 1, 2 ve 3 numaralı Gameplay V1 kurulumlarının " +
                    "mevcut sahnede tamamlandığından emin ol.",
                    "Tamam");
                return false;
            }

            return true;
        }

        private static bool IsPrototypeSceneOpen()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() &&
                activeScene.path == "Assets/Glassfall/Scenes/Prototype.unity";
        }

        private static bool TryGetRequiredSceneObjects(
            out GameObject arena,
            out DropletSplitSystem splitSystem)
        {
            arena = GameObject.Find("Arena");
            splitSystem = Object.FindAnyObjectByType<DropletSplitSystem>();

            if (arena == null || splitSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Kurulum yapılamadı",
                    "Prototype scene açık görünmüyor.\n\n" +
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini " +
                    "aç. Hierarchy'de Arena ve Droplet olduğundan emin ol.",
                    "Tamam");
                return false;
            }

            return true;
        }

        private static GameObject EnsureChild(
            GameObject parent,
            string childName)
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static SplitLinkSystem EnsureLinkSystem(
            GameObject root,
            DropletSplitSystem splitSystem)
        {
            GameObject linkObject = EnsureChild(root, LinkName);

            SplitLinkSystem linkSystem =
                GetOrAddComponent<SplitLinkSystem>(linkObject);
            linkSystem.Configure(splitSystem);

            GetOrAddComponent<LineRenderer>(linkObject);
            SplitLinkVisual visual =
                GetOrAddComponent<SplitLinkVisual>(linkObject);
            visual.Configure(linkSystem);

            EditorUtility.SetDirty(linkSystem);
            EditorUtility.SetDirty(visual);
            return linkSystem;
        }

        private static ResonanceNode EnsureResonanceNode(
            GameObject root,
            SplitLinkSystem linkSystem)
        {
            GameObject nodeObject = EnsureChild(root, ResonanceName);
            nodeObject.transform.localPosition = new Vector3(2f, 0f, 0f);

            ResonanceNode node = GetOrAddComponent<ResonanceNode>(nodeObject);
            node.Configure(linkSystem);
            GetOrAddComponent<LineRenderer>(nodeObject);
            GetOrAddComponent<ResonanceNodeVisual>(nodeObject);

            EditorUtility.SetDirty(node);
            return node;
        }

        private static ArcHunter EnsureArcHunter(
            GameObject root,
            SplitLinkSystem linkSystem)
        {
            GameObject hunterObject = EnsureChild(root, HunterName);
            hunterObject.transform.localPosition = new Vector3(-3f, 2f, 0f);

            ArcHunter hunter = GetOrAddComponent<ArcHunter>(hunterObject);
            hunter.Configure(linkSystem);
            GetOrAddComponent<LineRenderer>(hunterObject);
            GetOrAddComponent<ArcHunterVisual>(hunterObject);
            GetOrAddComponent<ArcHunterHealthReadability>(hunterObject);

            EditorUtility.SetDirty(hunter);
            return hunter;
        }

        private static SplitLinkCombat EnsureCombat(
            GameObject root,
            SplitLinkSystem linkSystem,
            ArcHunter hunter)
        {
            Transform linkTransform = root.transform.Find(LinkName);
            if (linkTransform == null)
                return null;

            SplitLinkCombat combat =
                GetOrAddComponent<SplitLinkCombat>(linkTransform.gameObject);
            combat.Configure(linkSystem, hunter);
            EditorUtility.SetDirty(combat);
            return combat;
        }

        private static T GetOrAddComponent<T>(GameObject target)
            where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null
                ? existing
                : Undo.AddComponent<T>(target);
        }

        private static void MarkDirtyAndSelect(GameObject selected)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
                EditorSceneManager.MarkSceneDirty(activeScene);

            Selection.activeGameObject = selected;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/17 - Setup Recovery Polish V1")]
        public static void SetupRecoveryPolishV1()
        {
            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();

            if (run == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Recovery Polish V1 kurulamadı",
                    "MicroRunController bulunamadı. Önce Micro Run V2 " +
                    "kurulumunun sahnede olduğundan emin ol.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(run, "Configure Recovery Polish V1");
            run.ConfigureRecoveryPolish(
                recoveryGain: 3.5f,
                recoveryTimeout: 5.0f);

            EditorUtility.SetDirty(run);
            MarkDirtyAndSelect(run.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Recovery Polish V1",
                "Kurulum tamamlandı.\n\n" +
                "Recovery hedefi +3.5 mass / 5s olarak ayarlandı. Bu, split " +
                "halde %60 collection ile recovery'yi bedavaya tamamlamayı " +
                "zorlaştırır; merged harvest ise attraction + tam collection " +
                "ile doğal olarak daha verimli kalır.\n\n" +
                "Recovery progress artık o phase içinde ulaşılan en yüksek " +
                "mass gain değerini tutar. Sonradan split cost ödemek progress'i " +
                "geri düşürmez.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }


        [MenuItem(
            "Tools/Glassfall/Gameplay V1/18 - Setup Prototype Pixel Art V1")]
        public static void SetupPrototypePixelArtV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Pixel Art V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            const string mergedPath =
                "Assets/Glassfall/Art/Sprites/Player/MergedPlayer.png";
            const string primaryPath =
                "Assets/Glassfall/Art/Sprites/Player/SplitPrimary.png";
            const string mirrorPath =
                "Assets/Glassfall/Art/Sprites/Player/SplitMirror.png";
            const string neutralPath =
                "Assets/Glassfall/Art/Sprites/Droplets/NeutralDroplet.png";
            const string fragmentPath =
                "Assets/Glassfall/Art/Sprites/Droplets/RecoverableMassFragment.png";

            ConfigurePixelSpriteImporter(mergedPath, 58f);
            ConfigurePixelSpriteImporter(primaryPath, 54f);
            ConfigurePixelSpriteImporter(mirrorPath, 54f);
            ConfigurePixelSpriteImporter(neutralPath, 22f);
            ConfigurePixelSpriteImporter(fragmentPath, 24f);

            Sprite merged = AssetDatabase.LoadAssetAtPath<Sprite>(mergedPath);
            Sprite primary = AssetDatabase.LoadAssetAtPath<Sprite>(primaryPath);
            Sprite mirror = AssetDatabase.LoadAssetAtPath<Sprite>(mirrorPath);
            Sprite neutral = AssetDatabase.LoadAssetAtPath<Sprite>(neutralPath);
            Sprite fragment = AssetDatabase.LoadAssetAtPath<Sprite>(fragmentPath);

            if (merged == null || primary == null || mirror == null ||
                neutral == null || fragment == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Pixel Art V1 kurulamadı",
                    "Pixel sprite dosyalarından biri Unity tarafından Sprite " +
                    "olarak import edilemedi. Console'u kontrol et.",
                    "Tamam");
                return;
            }

            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            if (splitSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Pixel Art V1 kurulamadı",
                    "Scene içinde DropletSplitSystem bulunamadı.",
                    "Tamam");
                return;
            }

            GameObject player = splitSystem.gameObject;
            Transform visualRoot = player.transform.Find("VisualRoot");
            SpriteRenderer playerRenderer = visualRoot != null
                ? visualRoot.GetComponent<SpriteRenderer>()
                : null;
            if (playerRenderer == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Pixel Art V1 kurulamadı",
                    "Droplet > VisualRoot üzerinde SpriteRenderer bulunamadı.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(playerRenderer, "Configure Pixel Player Renderer");
            playerRenderer.sprite = merged;
            playerRenderer.color = Color.white;
            EditorUtility.SetDirty(playerRenderer);

            DisableLegacyStaticRenderer(visualRoot, "Rim");
            DisableLegacyStaticRenderer(visualRoot, "Highlight");

            DropletPixelArtPresentation playerPixels =
                GetOrAddComponent<DropletPixelArtPresentation>(player);
            Undo.RecordObject(playerPixels, "Configure Player Pixel Art V1");
            playerPixels.Configure(playerRenderer, merged, primary, mirror);
            EditorUtility.SetDirty(playerPixels);

            const string neutralPrefabPath =
                "Assets/Glassfall/Gameplay/Droplet/Prefabs/NeutralDroplet.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
                neutralPrefabPath);
            try
            {
                SpriteRenderer pickupRenderer =
                    prefabRoot.GetComponent<SpriteRenderer>();
                if (pickupRenderer == null)
                    pickupRenderer = prefabRoot.AddComponent<SpriteRenderer>();

                pickupRenderer.sprite = neutral;
                pickupRenderer.color = Color.white;

                PickupPixelArtPresentation pickupPixels =
                    prefabRoot.GetComponent<PickupPixelArtPresentation>();
                if (pickupPixels == null)
                {
                    pickupPixels =
                        prefabRoot.AddComponent<PickupPixelArtPresentation>();
                }

                pickupPixels.Configure(
                    pickupRenderer,
                    neutral,
                    fragment);

                EditorUtility.SetDirty(pickupRenderer);
                EditorUtility.SetDirty(pickupPixels);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, neutralPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            MarkDirtyAndSelect(player);

            EditorUtility.DisplayDialog(
                "Glassfall - Prototype Pixel Art V1",
                "Kurulum tamamlandı.\n\n" +
                "- Merged / Split Primary / Split Mirror sprite'ları runtime " +
                "role göre otomatik değişir.\n" +
                "- Neutral droplet ve recoverable fragment ayrı sprite kullanır.\n" +
                "- Mass-radius scaling ve gameplay physics aynen korunur.\n" +
                "- Eski statik Rim / Highlight katmanları kapatıldı; hareket, " +
                "collect, split ve re-merge scale feedback'i korunur.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }


        [MenuItem(
            "Tools/Glassfall/Gameplay V1/19 - Setup Supporting Pixel Art V1")]
        public static void SetupSupportingPixelArtV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Supporting Pixel Art V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            AssetDatabase.Refresh();

            const string hunterPath =
                "Assets/Glassfall/Art/Sprites/Enemies/ArcHunter.png";
            const string nodePath =
                "Assets/Glassfall/Art/Sprites/Objectives/ResonanceNode.png";
            const string dirtyPath =
                "Assets/Glassfall/Art/Sprites/Environment/DirtyGlassPatch.png";

            // PPU values are chosen so the authored visible silhouettes roughly
            // match the already-validated gameplay interaction radii.
            ConfigurePixelSpriteImporter(hunterPath, 88f);
            ConfigurePixelSpriteImporter(nodePath, 44f);
            ConfigurePixelSpriteImporter(dirtyPath, 128f);

            Sprite hunterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(hunterPath);
            Sprite nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(nodePath);
            Sprite dirtySprite = AssetDatabase.LoadAssetAtPath<Sprite>(dirtyPath);

            if (hunterSprite == null || nodeSprite == null ||
                dirtySprite == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Supporting Pixel Art V1 kurulamadı",
                    "Supporting sprite dosyalarından biri Unity tarafından " +
                    "Sprite olarak import edilemedi. Console'u kontrol et.",
                    "Tamam");
                return;
            }

            GameObject arena = GameObject.Find("Arena");
            ArcHunter hunter = Object.FindAnyObjectByType<ArcHunter>();
            ResonanceNode node = Object.FindAnyObjectByType<ResonanceNode>();
            SurfaceZone dirtyZone = Object.FindAnyObjectByType<SurfaceZone>();

            if (arena == null || hunter == null || node == null || dirtyZone == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Supporting Pixel Art V1 kurulamadı",
                    "Arena, ArcHunter, ResonanceNode veya SurfaceZone bulunamadı. " +
                    "Önce önceki Gameplay V1 setup adımlarının sahnede olduğundan " +
                    "emin ol.",
                    "Tamam");
                return;
            }

            EnsurePixelSpriteChild(
                hunter.gameObject,
                "PixelSprite",
                hunterSprite,
                sortingOrder: 4);
            EnsurePixelSpriteChild(
                node.gameObject,
                "PixelSprite",
                nodeSprite,
                sortingOrder: 3);

            // Keep the existing LineRenderer-based pulses/readability layer.
            // It remains above the authored sprite and therefore still shows
            // conductivity, hit and resonance-state feedback.
            LineRenderer hunterLine = hunter.GetComponent<LineRenderer>();
            if (hunterLine != null)
            {
                Undo.RecordObject(hunterLine, "Layer Hunter Pixel Art V1");
                hunterLine.sortingOrder = 5;
                EditorUtility.SetDirty(hunterLine);
            }

            LineRenderer nodeLine = node.GetComponent<LineRenderer>();
            if (nodeLine != null)
            {
                Undo.RecordObject(nodeLine, "Layer Resonance Pixel Art V1");
                nodeLine.sortingOrder = 4;
                EditorUtility.SetDirty(nodeLine);
            }

            SpriteRenderer dirtyRenderer = dirtyZone.GetComponent<SpriteRenderer>();
            SurfaceZoneVisual dirtyVisual = dirtyZone.GetComponent<SurfaceZoneVisual>();
            if (dirtyRenderer == null || dirtyVisual == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Supporting Pixel Art V1 kurulamadı",
                    "DirtyGlass SurfaceZone üzerinde SpriteRenderer veya " +
                    "SurfaceZoneVisual bulunamadı.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(dirtyRenderer, "Configure Dirty Glass Pixel Art V1");
            dirtyRenderer.sprite = dirtySprite;
            dirtyRenderer.color = Color.white;
            dirtyRenderer.drawMode = SpriteDrawMode.Simple;
            dirtyRenderer.sortingOrder = -3;
            EditorUtility.SetDirty(dirtyRenderer);

            SerializedObject dirtySerialized = new SerializedObject(dirtyVisual);
            dirtySerialized.FindProperty("dirtyTint").colorValue = Color.white;
            dirtySerialized.FindProperty("opacity").floatValue = 0.68f;
            dirtySerialized.FindProperty("noiseStrength").floatValue = 0.04f;
            dirtySerialized.FindProperty("edgeSoftness").floatValue = 0.02f;
            dirtySerialized.FindProperty("sortingOrder").intValue = -3;
            dirtySerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(dirtyVisual);

            MarkDirtyAndSelect(hunter.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Supporting Pixel Art V1",
                "Kurulum tamamlandı.\n\n" +
                "- Arc Hunter artık authored orange pixel sprite kullanır.\n" +
                "- Resonance Node authored objective sprite kullanır.\n" +
                "- DirtyGlass zone authored grime sprite kullanır; mevcut " +
                "gameplay collider ve surface effect aynen korunur.\n" +
                "- World surface background ayrı fogged-window katmanından gelir; " +
                "tiled GlassFloor presentation kullanılmaz.\n" +
                "- Hunter/Node LineRenderer feedback katmanları korunur.\n\n" +
                "Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/20 - Setup Combat Readability Polish V1")]
        public static void SetupCombatReadabilityPolishV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Readability Polish V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            WetnessVisualRenderer wetnessVisual =
                Object.FindAnyObjectByType<WetnessVisualRenderer>();
            ArcHunterVisual hunterVisual =
                Object.FindAnyObjectByType<ArcHunterVisual>();
            ResonanceNodeVisual nodeVisual =
                Object.FindAnyObjectByType<ResonanceNodeVisual>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            ArcHunter hunter = Object.FindAnyObjectByType<ArcHunter>();
            ResonanceNode node = Object.FindAnyObjectByType<ResonanceNode>();
            GameObject arena = GameObject.Find("Arena");

            if (wetnessVisual == null || hunterVisual == null ||
                nodeVisual == null || splitSystem == null ||
                hunter == null || node == null || arena == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Readability Polish V1 kurulamadı",
                    "Önceki Gameplay V1 kurulumlarının (özellikle 18 ve 19) " +
                    "Prototype sahnesinde mevcut olduğundan emin ol.",
                    "Tamam");
                return;
            }

            // DropletVisual adds this presentation component at runtime, so it
            // may not exist in the edit-time scene yet. Reuse it when present
            // and add it once to the authoritative player object otherwise.
            DropletPresentationFeedback playerFeedback =
                GetOrAddComponent<DropletPresentationFeedback>(
                    splitSystem.gameObject);

            SpriteRenderer hunterSprite = FindChildSpriteRenderer(hunter);
            SpriteRenderer nodeSprite = FindChildSpriteRenderer(node);

            if (hunterSprite == null || nodeSprite == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Readability Polish V1 kurulamadı",
                    "ArcHunter veya ResonanceNode authored renderer'ı bulunamadı. " +
                    "Önce Setup 19'u çalıştır.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(wetnessVisual, "Configure Wet Trail Readability V1");
            wetnessVisual.ConfigureReadabilityProfile(
                newWetTint: new Color(0.16f, 0.42f, 0.62f, 1f),
                newHighlightColor: new Color(0.72f, 0.90f, 0.98f, 1f),
                newOpacity: 0.38f,
                newHighlightStrength: 0.18f,
                newResponseExponent: 0.92f,
                newNeighborBlend: 0.46f,
                newEdgeSoftness: 1.55f,
                newVisualRefreshInterval: 0.05f,
                newSortingOrder: -2);
            wetnessVisual.ConfigureLifecycle(
                newFadedThreshold: 0.32f,
                newFreshThreshold: 0.70f,
                newFadedOpacityMultiplier: 0.06f,
                newFreshHighlightMultiplier: 1.10f,
                newAgingOpacityMultiplier: 0.22f);

            Undo.RecordObject(hunterVisual, "Configure Arc Hunter Readability V1");
            hunterVisual.ConfigureCombatReadability(
                hunterSprite,
                newRingWidth: 0.065f,
                newRingAlphaMultiplier: 0.58f,
                newRingSortingOrder: 4,
                newSpriteSortingOrder: 5);

            Undo.RecordObject(nodeVisual, "Configure Resonance Node Readability V1");
            nodeVisual.ConfigureWorldObjectReadability(
                nodeSprite,
                newRingWidth: 0.055f,
                newRingAlphaMultiplier: 0.62f,
                newRingSortingOrder: 3,
                newSpriteSortingOrder: 4);

            Undo.RecordObject(
                playerFeedback,
                "Configure Mass Shear Feedback V1");
            playerFeedback.ConfigureCombatReadability(
                compressionScale: 0.82f,
                reboundScale: 1.06f,
                pulseDuration: 0.18f);

            EditorUtility.SetDirty(wetnessVisual);
            EditorUtility.SetDirty(hunterVisual);
            EditorUtility.SetDirty(nodeVisual);
            EditorUtility.SetDirty(hunterSprite);
            EditorUtility.SetDirty(nodeSprite);
            EditorUtility.SetDirty(playerFeedback);

            const string neutralPrefabPath =
                "Assets/Glassfall/Gameplay/Droplet/Prefabs/NeutralDroplet.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(neutralPrefabPath) == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Readability Polish V1 kurulamadı",
                    "NeutralDroplet prefab'ı bulunamadı; Setup 19 sonuçları " +
                    "korunarak işlem durduruldu.",
                    "Tamam");
                return;
            }

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(neutralPrefabPath);
            if (prefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Readability Polish V1 kurulamadı",
                    "NeutralDroplet prefab'ı yüklenemedi; işlem durduruldu.",
                    "Tamam");
                return;
            }

            try
            {
                PickupPixelArtPresentation pickupFeedback =
                    prefabRoot.GetComponent<PickupPixelArtPresentation>();
                if (pickupFeedback != null)
                {
                    pickupFeedback.ConfigureRecoveryFeedback(
                        new Color(0.62f, 0.95f, 1f, 1f),
                        flashDuration: 0.22f);
                    EditorUtility.SetDirty(pickupFeedback);
                    PrefabUtility.SaveAsPrefabAsset(
                        prefabRoot,
                        neutralPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            MarkDirtyAndSelect(hunter.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Combat Readability Polish V1",
                "Kurulum tamamlandı.\n\n" +
                "- Fresh / aging / faded wet trail hierarchy'si sakinleştirildi.\n" +
                "- Tiled floor pattern kullanılmaz; world-space glass background korunur.\n" +
                "- Arc Hunter ve Resonance Node sprite'ları feedback halkalarının " +
                "önüne alındı.\n" +
                "- Link hit, mass shear ve fragment spawn için kısa presentation " +
                "feedback eklendi.\n\n" +
                "Gameplay tuning değerleri değiştirilmedi. Ctrl+S ile sahneyi kaydet, " +
                "ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/21 - Setup Encounter & Run Presentation V1")]
        public static void SetupEncounterAndRunPresentationV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Encounter & Run Presentation V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            MidRunRewardController rewards =
                Object.FindAnyObjectByType<MidRunRewardController>();

            if (run == null || rewards == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Encounter & Run Presentation V1 kurulamadı",
                    "Önce Micro Run V2 ve Mid-Run Reward Choice kurulumlarını " +
                    "tamamla (özellikle 9 ve 16).",
                    "Tamam");
                return;
            }

            MicroRunOverlay runOverlay =
                GetOrAddComponent<MicroRunOverlay>(run.gameObject);
            MidRunRewardOverlay rewardOverlay =
                GetOrAddComponent<MidRunRewardOverlay>(run.gameObject);
            MicroBuildChoiceController builds =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            DropletMergeSystem mergeSystem = splitSystem != null
                ? splitSystem.GetComponent<DropletMergeSystem>()
                : null;

            Undo.RecordObject(run, "Configure Encounter Completion Beat V1");
            run.ConfigureEncounterPresentation(0.80f);

            Undo.RecordObject(runOverlay, "Configure Run Presentation V1");
            runOverlay.Configure(run, builds);
            runOverlay.ConfigureMass(splitSystem, mergeSystem);
            runOverlay.ConfigureRewards(rewards);
            runOverlay.ConfigurePresentation(
                completionBeatSeconds: 0.80f,
                finalIntroSeconds: 0.65f,
                completeFadeSeconds: 0.18f);

            Undo.RecordObject(
                rewardOverlay,
                "Configure Reward Presentation V1");
            rewardOverlay.Configure(run, rewards);
            rewardOverlay.ConfigurePresentation(0.22f);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(runOverlay);
            EditorUtility.SetDirty(rewardOverlay);
            MarkDirtyAndSelect(run.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Encounter & Run Presentation V1",
                "Kurulum tamamlandı.\n\n" +
                "- Encounter tamamlanınca 0.80 saniyelik okunabilir bir completion beat " +
                "gösterilir.\n" +
                "- Reward kartları 0.22 saniyede fade-in olur; gameplay freeze ve " +
                "ödül içeriği korunur.\n" +
                "- Final pressure encounter için ayrı bir intro ve vurgu katmanı " +
                "kullanılır.\n" +
                "- Run Complete paneli kısa fade-in ile build, hunt sayısı ve süreyi " +
                "gösterir.\n\n" +
                "Gameplay balance değerleri değiştirilmedi. Ctrl+S ile sahneyi " +
                "kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/22 - Setup Core Integrity & Failure State V1")]
        public static void SetupCoreIntegrityAndFailureStateV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Core Integrity & Failure State V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            ArcHunter hunter = Object.FindAnyObjectByType<ArcHunter>();
            MassShearSystem shear =
                Object.FindAnyObjectByType<MassShearSystem>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            DropletMergeSystem mergeSystem = splitSystem != null
                ? splitSystem.GetComponent<DropletMergeSystem>()
                : null;
            DropletPixelArtPresentation playerPresentation =
                splitSystem != null
                    ? splitSystem.GetComponent<DropletPixelArtPresentation>()
                    : null;
            SplitLinkSystem linkSystem =
                Object.FindAnyObjectByType<SplitLinkSystem>();

            GameObject boundsObject = GameObject.Find("NeurtalSpawnArea");
            if (boundsObject == null)
                boundsObject = GameObject.Find("NeutralSpawnArea");
            Collider2D playableBounds = boundsObject != null
                ? boundsObject.GetComponent<Collider2D>()
                : null;

            if (run == null || hunter == null || shear == null ||
                splitSystem == null || mergeSystem == null ||
                playerPresentation == null || linkSystem == null ||
                playableBounds == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Core Integrity & Failure State V1 kurulamadı",
                    "Micro Run, Arc Hunter, Mass Shear, player merge/presentation, " +
                    "Split Link veya mevcut NeurtalSpawnArea bounds kurulumu bulunamadı. Önce " +
                    "Gameplay V1 kurulumlarını tamamla.",
                    "Tamam");
                return;
            }

            ContaminationSystem contamination =
                GetOrAddComponent<ContaminationSystem>(run.gameObject);

            Undo.RecordObject(contamination, "Configure Contamination Failure V1");
            contamination.Configure(
                mergedGain: 0.18f,
                splitGain: 0.23f,
                neutralCleanse: 0.04f,
                recoveryCleanse: 0.24f,
                taintedAt: 0.12f,
                corruptedAt: 0.38f,
                criticalAt: 0.72f,
                failedAt: 1f);
            contamination.ConfigurePresentationBeats(
                hitFlashDuration: 0.18f,
                cleansePulseDuration: 0.38f);

            Undo.RecordObject(hunter, "Configure Arc Hunter Combat AI V2");
            hunter.Configure(linkSystem);
            hunter.ConfigureArenaContainment(
                playableBounds,
                padding: 0.18f);
            hunter.ConfigureCombatCycle(
                preferredDistance: 2.4f,
                telegraphDuration: 0.55f,
                attackDashSpeed: 6f,
                attackMaxDistance: 3.2f,
                recoverDuration: 0.6f,
                repositionDuration: 0.7f,
                attackCooldown: 1f,
                avoidanceRadius: 0.55f);
            hunter.ConfigureMassShear(shear);

            Undo.RecordObject(shear, "Configure Arc Hunter Attack-Only Mass Shear");
            shear.SetContactThreatEnabled(false);
            shear.ConfigureContamination(contamination);

            Undo.RecordObject(mergeSystem, "Configure Neutral Contamination Cleanse");
            mergeSystem.ConfigureContamination(contamination);

            Undo.RecordObject(
                playerPresentation,
                "Configure Player Contamination Readability");
            playerPresentation.ConfigureContamination(
                contamination,
                taintedStrength: 0.22f,
                corruptedStrength: 0.48f,
                criticalStrength: 0.72f);

            Undo.RecordObject(run, "Configure Contamination Run Failure V1");
            run.ConfigureContaminationFailure(
                contamination,
                collapseBeatSeconds: 0.65f);

            EditorUtility.SetDirty(contamination);
            EditorUtility.SetDirty(hunter);
            EditorUtility.SetDirty(shear);
            EditorUtility.SetDirty(mergeSystem);
            EditorUtility.SetDirty(playerPresentation);
            EditorUtility.SetDirty(run);
            MarkDirtyAndSelect(hunter.gameObject);

            EditorUtility.DisplayDialog(
                "Glassfall - Core Integrity & Failure State V1",
                "Kurulum tamamlandı.\n\n" +
                "- Arc Hunter mevcut NeurtalSpawnArea bounds içinde tutulur; " +
                "sprite/interaction footprint için 0.18 padding kullanılır.\n" +
                "- Hunter Hunt → Target Lock → Telegraph (0.55 sn) → " +
                "Commit Attack → Recover → Reposition döngüsünü kullanır.\n" +
                "- Dash 6 hız / 3.2 maksimum mesafe ile sabitlenir; " +
                "link avoidance yalnız hunt/reposition aşamalarında aktiftir.\n" +
                "- Mass Shear yalnızca saldırı impact'inde uygulanır; normal body overlap " +
                "artık otomatik shear üretmez.\n" +
                "- Arc Hunter hit: merged +0.18, split +0.23 contamination.\n" +
                "- Neutral pickup -0.04; tamamlanan Recovery hedefi -0.24 purification " +
                "ve en az bir contamination tier geri çekilmesi sağlar.\n" +
                "- Cleanse pulse 0.38 sn; body tint Tainted 0.22 / Corrupted 0.48 / Critical 0.72.\n" +
                "- Tainted 0.12, Corrupted 0.38, Critical 0.72, failure 1.00.\n" +
                "- Collapse beat 0.65 saniyedir; ardından CONTAMINATED / RUN FAILED gösterilir.\n\n" +
                "Mass body/resource economy olarak korunur; eski mass-critical death " +
                "condition kaldırıldı. Ctrl+S ile sahneyi kaydet, ardından Play'e bas.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/23 - Setup First World Background V1")]
        public static void SetupFirstWorldBackgroundV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - First World Background V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            AssetDatabase.Refresh();

            const string videoPath =
                "Assets/Glassfall/Art/Backgrounds/FoggedWindowWorldLoop.mp4";
            const string videoBlurShaderPath =
                "Assets/Glassfall/Presentation/VideoBackgroundBlur.shader";

            VideoClip backgroundClip =
                AssetDatabase.LoadAssetAtPath<VideoClip>(videoPath);
            Shader videoBlurShader =
                AssetDatabase.LoadAssetAtPath<Shader>(videoBlurShaderPath);
            Camera mainCamera = Camera.main;
            GameObject arena = GameObject.Find("Arena");
            GameObject boundsObject = GameObject.Find("NeurtalSpawnArea") ??
                GameObject.Find("NeutralSpawnArea");
            Collider2D worldBounds = boundsObject != null
                ? boundsObject.GetComponent<Collider2D>()
                : null;
            MicroRunController runController =
                Object.FindAnyObjectByType<MicroRunController>();

            if (backgroundClip == null || videoBlurShader == null ||
                mainCamera == null || arena == null || runController == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - First World Background V1 kurulamadı",
                    "FoggedWindowWorldLoop.mp4, VideoBackgroundBlur shader, Main Camera, " +
                    "Arena veya MicroRunController bulunamadı. Asset import tamamlandıktan " +
                    "sonra tekrar dene.",
                    "Tamam");
                return;
            }

            GameObject backgroundRoot =
                EnsureChild(arena, "FoggedWindowWorldV1");
            Undo.RecordObject(
                backgroundRoot.transform,
                "Configure Fogged Window World V1");
            backgroundRoot.transform.localPosition = Vector3.zero;
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one;

            // The Flow video now contains the wet pixel-art glass itself. Remove every
            // legacy haze/fog/streak sprite so there is a single visual source of truth.
            RemoveObsoleteBackgroundLayers(backgroundRoot);

            FoggedWindowWorldBackground background =
                GetOrAddComponent<FoggedWindowWorldBackground>(backgroundRoot);
            VideoPlayer videoPlayer =
                GetOrAddComponent<VideoPlayer>(backgroundRoot);

            GameObject videoSurface =
                EnsureChild(backgroundRoot, "FoggedWindowVideoSurface");
            MeshFilter videoSurfaceFilter =
                GetOrAddComponent<MeshFilter>(videoSurface);
            MeshRenderer videoSurfaceRenderer =
                GetOrAddComponent<MeshRenderer>(videoSurface);

            Undo.RecordObject(background, "Configure First World Background V1");
            background.Configure(
                backgroundClip,
                mainCamera,
                runController,
                videoOpacity: 1f,
                videoPlaybackEnabled: true,
                refreshInterval: 0.033f);
            background.ConfigureVideoSurface(
                videoSurfaceRenderer,
                videoSurfaceFilter,
                videoBlurShader,
                brightness: 1f,
                saturation: 1f,
                tint: Color.white,
                sortingOrder: -1100,
                coverage: 1.035f,
                worldZ: 6.2f);
            background.ConfigureProgressiveBlur(
                startRadiusPixels: 8f,
                startMix: 0.98f,
                midRadiusPixels: 4f,
                midMix: 0.78f,
                endRadiusPixels: 1.2f,
                endMix: 0.30f,
                transitionSpeed: 2.6f);
            background.ConfigureWorldProgressBinding(0);
            if (worldBounds != null)
            {
                background.ConfigureWorldSurface(
                    worldBounds,
                    BackgroundSurfaceFitMode.WorldBounds,
                    coverage: 1.04f);
            }

            Undo.RecordObject(videoPlayer, "Configure Fogged Background Video V1");
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = backgroundClip;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = videoSurfaceRenderer;
            videoPlayer.targetMaterialProperty = "_MainTex";
            videoPlayer.targetCamera = null;
            videoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            videoPlayer.targetCameraAlpha = 1f;
            videoPlayer.isLooping = true;
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.enabled = true;

            EditorUtility.SetDirty(background);
            EditorUtility.SetDirty(videoPlayer);
            EditorUtility.SetDirty(videoSurfaceFilter);
            EditorUtility.SetDirty(videoSurfaceRenderer);
            MarkDirtyAndSelect(backgroundRoot);

            EditorUtility.DisplayDialog(
                "Glassfall - First World Background V1",
                @"Kurulum tamamlandı.

- Flow'dan gelen FoggedWindowWorldLoop.mp4 tek çevre/cam görsel katmanıdır.
- Eski far/mid/near blur sprite'ları, fog overlay ve streak overlay kaldırıldı.
- Videoya renk tonu, saturation düşürme veya ekstra sis uygulanmaz.
- Blur yalnızca video surface shader'ında uygulanır.
- Blur mevcut MicroRun ilerlemesine bağlı 3 faz kullanır:
  1) Başlangıç: güçlü blur (8.0 px / 0.98 mix)
  2) İlk üçte bir tamamlandıktan sonra: orta blur (4.0 px / 0.78 mix)
  3) Son üçte birlik bölüm: hafif blur (1.2 px / 0.30 mix)
- Faz değişimlerinde blur aniden zıplamaz; yaklaşık yumuşak bir geçişle azalır.
- İlerleme kaynağı mevcut MicroRunController'dır; ayrı progression sistemi eklenmedi.
- Gameplay ve UI katmanları blur almaz.

Ctrl+S ile sahneyi kaydet, Console'u temizle ve Play ile doğrula.",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/23B - Setup First World Layout V1")]
        public static void SetupFirstWorldLayoutV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - First World Layout V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            AssetDatabase.Refresh();

            Camera mainCamera = Camera.main;
            GameObject arena = GameObject.Find("Arena");
            GameObject boundsObject = GameObject.Find("NeurtalSpawnArea") ??
                GameObject.Find("NeutralSpawnArea");
            BoxCollider2D worldBounds = boundsObject != null
                ? boundsObject.GetComponent<BoxCollider2D>()
                : null;
            MicroRunController runController =
                Object.FindAnyObjectByType<MicroRunController>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            ArcHunter arcHunter =
                Object.FindAnyObjectByType<ArcHunter>();
            NeutralDropletSpawner neutralSpawner =
                Object.FindAnyObjectByType<NeutralDropletSpawner>();
            WetnessField wetnessField =
                Object.FindAnyObjectByType<WetnessField>();
            FoggedWindowWorldBackground background =
                Object.FindAnyObjectByType<FoggedWindowWorldBackground>();

            if (mainCamera == null || arena == null || worldBounds == null ||
                runController == null || splitSystem == null || resonanceNode == null ||
                arcHunter == null || neutralSpawner == null || wetnessField == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - First World Layout V1 kurulamadı",
                    "Main Camera, Arena, NeurtalSpawnArea, MicroRunController, DropletSplitSystem, ResonanceNode, ArcHunter, NeutralDropletSpawner veya WetnessField bulunamadı.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(boundsObject.transform, "Configure First World Bounds Transform");
            boundsObject.transform.localPosition = Vector3.zero;
            boundsObject.transform.localRotation = Quaternion.identity;
            boundsObject.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(boundsObject.transform);

            Undo.RecordObject(worldBounds, "Configure First World Bounds V1");
            worldBounds.isTrigger = true;
            worldBounds.offset = Vector2.zero;
            worldBounds.size = new Vector2(42f, 24f);
            EditorUtility.SetDirty(worldBounds);

            ConfigurePlayableBoundaryWalls(arena, worldBounds);

            Undo.RecordObject(neutralSpawner, "Configure First World Neutral Spawn Bounds");
            neutralSpawner.ConfigureSpawnArea(worldBounds);
            EditorUtility.SetDirty(neutralSpawner);

            Undo.RecordObject(wetnessField, "Configure First World Wetness Bounds");
            wetnessField.ConfigureWorldBounds(worldBounds);
            EditorUtility.SetDirty(wetnessField);

            RemoveTiledGlassFloorPresentation(arena);
            const string dirtyGlassLandmarkPath =
                "Assets/Glassfall/Art/Sprites/Environment/DirtyGlassPatchSoft.png";
            ConfigureDirtyGlassLandmarkImporter(dirtyGlassLandmarkPath, 128f);
            Sprite dirtyGlassSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                dirtyGlassLandmarkPath);
            if (dirtyGlassSprite != null)
                ConfigureDirtyGlassLandmarks(arena, dirtyGlassSprite);

            Vector2[] encounterRoute =
            {
                new Vector2(-11.5f, -5.5f),
                new Vector2(0.0f, 7.0f),
                new Vector2(13.0f, 1.8f)
            };

            Undo.RecordObject(runController, "Configure First World Route V1");
            runController.ConfigureEncounterVariation(encounterRoute);
            runController.ConfigureMicroRunV2(
                splitSystem,
                preparationGain: 2.0f,
                preparationTimeout: 4.0f,
                recoveryGain: 3.5f,
                recoveryTimeout: 6.0f,
                intermissionDuration: 1.25f);
            EditorUtility.SetDirty(runController);

            Undo.RecordObject(resonanceNode.transform, "Position First Resonance Anchor");
            resonanceNode.transform.position = new Vector3(-15f, 0f, 0f);
            EditorUtility.SetDirty(resonanceNode);

            Undo.RecordObject(arcHunter, "Configure First World Hunter Bounds");
            arcHunter.ConfigureArenaContainment(worldBounds, padding: 0.18f);
            EditorUtility.SetDirty(arcHunter);

            Undo.RecordObject(mainCamera, "Configure First World Camera");
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5.8f;
            WorldCameraFollow follow =
                GetOrAddComponent<WorldCameraFollow>(mainCamera.gameObject);
            follow.Configure(
                splitSystem,
                worldBounds,
                sharpness: 6.5f,
                offset: Vector2.zero,
                midpointBias: 1f);
            EditorUtility.SetDirty(mainCamera);
            EditorUtility.SetDirty(follow);

            if (background != null)
            {
                Undo.RecordObject(background, "Configure Background For First World Layout V1");
                background.ConfigureWorldSurface(
                    worldBounds,
                    BackgroundSurfaceFitMode.WorldBounds,
                    coverage: 1.04f);
                EditorUtility.SetDirty(background);
            }

            GameObject routeRoot = EnsureChild(arena, "FirstWorldRouteV1");
            routeRoot.transform.localPosition = Vector3.zero;
            routeRoot.transform.localRotation = Quaternion.identity;
            routeRoot.transform.localScale = Vector3.one;
            ConfigureRouteMarker(routeRoot, "Start", new Vector2(-16f, -8f));
            ConfigureRouteMarker(routeRoot, "AnchorA", new Vector2(-15f, 0f));
            ConfigureRouteMarker(routeRoot, "AnchorB", new Vector2(0f, 8f));
            ConfigureRouteMarker(routeRoot, "AnchorC", new Vector2(14f, 2f));

            MarkDirtyAndSelect(routeRoot);

            EditorUtility.DisplayDialog(
                "Glassfall - First World Layout V1",
                @"Kurulum tamamlandı.

- Playable world NeurtalSpawnArea üzerinde 42 x 24'e genişletildi.
- Eski prototype duvar collider'ları canonical world bounds kenarlarına taşındı.
- Merged ve split player artık geniş world içinde fiziksel olarak hareket edebilir.
- Neutral spawning ve WetnessField aynı canonical bounds'a bağlandı.
- Tiled GlassFloor presentation kaldırıldı; fogged-window background clean glass base olarak kaldı.
- DirtyGlass yalnızca lokal, deterministic world landmark kümeleri olarak dağıtıldı.
- Encounter route üç fiziksel anchor noktasına yayıldı.
- Resonance objective ilk dünya anchor'ına taşındı.
- Main Camera artık player'ı takip ediyor; split halde midpoint'e odaklanıyor ve world bounds içinde clamp oluyor.
- Fogged background artık kamera viewport'una değil, geniş dünya bounds'una fit oluyor.
- Current run flow korunur; yalnızca dünya üzerinde mekânsal olarak dağıtılır.

Doğrulama:
1) Kamera world içinde akıcı takip ediyor mu?
2) Arka plan kamerayla sabit kaymak yerine dünya üzerinde sabit duruyor mu?
3) Encounter 1/2/3 farklı bölgelerde spawn oluyor mu?
4) Window Stabilized tamamlanma paneli geliyor mu?",
                "Tamam");
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/24 - Setup World Navigation & Encounter Pacing V1")]
        public static void SetupWorldNavigationAndEncounterPacingV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - World Navigation V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            Camera mainCamera = Camera.main;
            GameObject arena = GameObject.Find("Arena");
            MicroRunController runController =
                Object.FindAnyObjectByType<MicroRunController>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            ArcHunter arcHunter = Object.FindAnyObjectByType<ArcHunter>();
            ResonanceNode resonanceNode =
                Object.FindAnyObjectByType<ResonanceNode>();
            BoxCollider2D worldBounds = null;
            GameObject boundsObject = GameObject.Find("NeurtalSpawnArea") ??
                GameObject.Find("NeutralSpawnArea");
            if (boundsObject != null)
                worldBounds = boundsObject.GetComponent<BoxCollider2D>();

            if (mainCamera == null || arena == null || runController == null ||
                splitSystem == null || arcHunter == null ||
                resonanceNode == null || worldBounds == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - World Navigation V1 kurulamadı",
                    "Main Camera, Arena, world bounds, MicroRunController, DropletSplitSystem, ResonanceNode veya ArcHunter bulunamadı.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(runController, "Configure World Navigation Pacing");
            runController.ConfigureWorldNavigation(3.75f);
            runController.ConfigureWorldProgressScope(1, 3);
            EditorUtility.SetDirty(runController);

            // Resolve directly from the authored player object so this editor
            // setup does not depend on DropletSplitSystem.Awake having run.
            DropletController primary =
                splitSystem.GetComponent<DropletController>();
            if (primary != null)
            {
                Undo.RecordObject(primary, "Configure Merged Travel Pacing");
                primary.ConfigureMergedMovement(0.90f);
                EditorUtility.SetDirty(primary);
            }

            GameObject root = EnsureChild(arena, RootName);
            GameObject navigationObject = EnsureChild(
                root,
                "WorldNavigationV1");
            navigationObject.transform.localPosition = Vector3.zero;
            navigationObject.transform.localRotation = Quaternion.identity;
            navigationObject.transform.localScale = Vector3.one;

            WorldNavigationOverlay overlay =
                GetOrAddComponent<WorldNavigationOverlay>(navigationObject);
            Transform routeRoot = arena.transform.Find("FirstWorldRouteV1");
            Transform[] routeTargets =
            {
                routeRoot != null ? routeRoot.Find("AnchorA") : null,
                routeRoot != null ? routeRoot.Find("AnchorB") : null,
                routeRoot != null ? routeRoot.Find("AnchorC") : null
            };
            Undo.RecordObject(overlay, "Configure World Navigation Overlay");
            overlay.Configure(
                mainCamera,
                runController,
                splitSystem,
                arcHunter,
                resonanceNode,
                routeTargets);
            overlay.ConfigureReadability(
                width: 104f,
                height: 38f,
                padding: 58f,
                spacing: 46f,
                fontSize: 15,
                panelAlpha: 0.82f);
            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(navigationObject);

            MarkDirtyAndSelect(navigationObject);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - World Navigation & Encounter Pacing V1",
                    "Kurulum tamamlandı.\n\n" +
                    "- Off-screen enemy, objective ve split Primary/Mirror göstergeleri bağlandı.\n" +
                    "- Encounter artık oyuncu ilgili dünya bölgesine yaklaşınca aktive olur.\n" +
                    "- Merged travel speed konservatif olarak %10 azaltıldı; split hızı korunur.\n" +
                    "- FirstWorldRouteV1 anchor'ları hedef sunumuna bağlandı.\n\n" +
                    "Ctrl+S ile sahneyi kaydet, ardından Play Mode'da doğrula.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25 - Setup Reward Pool Expansion V1")]
        public static void SetupRewardPoolExpansionV1()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Pool Expansion V1 kurulamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            MidRunRewardController rewards =
                Object.FindAnyObjectByType<MidRunRewardController>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            SplitLinkCombat combat =
                Object.FindAnyObjectByType<SplitLinkCombat>();
            MergedFormIdentity identity =
                Object.FindAnyObjectByType<MergedFormIdentity>();
            MassShearSystem shear =
                Object.FindAnyObjectByType<MassShearSystem>();
            WetnessField wetnessField =
                Object.FindAnyObjectByType<WetnessField>();
            ContaminationSystem contamination =
                Object.FindAnyObjectByType<ContaminationSystem>();

            if (run == null || rewards == null || splitSystem == null ||
                combat == null || identity == null || shear == null ||
                wetnessField == null || contamination == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Pool Expansion V1 kurulamadı",
                    "M25 için MicroRun, reward controller, player split, combat, " +
                    "MergedFormIdentity, MassShearSystem, WetnessField ve " +
                    "ContaminationSystem birlikte bulunmalı.",
                    "Tamam");
                return;
            }

            DropletMergeSystem mergeSystem =
                splitSystem.GetComponent<DropletMergeSystem>();
            if (mergeSystem == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Pool Expansion V1 kurulamadı",
                    "DropletMergeSystem bulunamadı.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(rewards, "Configure Reward Pool Expansion V1");
            rewards.Configure(
                splitSystem,
                mergeSystem,
                combat,
                identity,
                shear,
                wetnessField,
                contamination);
            rewards.ConfigureRewardPool(
                historyLength: 3,
                avoidChoiceSetRepeats: true);

            run.ConfigureRewards(rewards);
            MicroRunOverlay runOverlay =
                GetOrAddComponent<MicroRunOverlay>(run.gameObject);
            runOverlay.ConfigureRewards(rewards);

            MidRunRewardOverlay rewardOverlay =
                GetOrAddComponent<MidRunRewardOverlay>(run.gameObject);
            rewardOverlay.Configure(run, rewards);

            EditorUtility.SetDirty(run);
            EditorUtility.SetDirty(rewards);
            EditorUtility.SetDirty(runOverlay);
            EditorUtility.SetDirty(rewardOverlay);
            MarkDirtyAndSelect(run.gameObject);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Reward Pool Expansion V1",
                    "Kurulum tamamlandı.\n\n" +
                    "- 10 mevcut core knob'una bağlı reward havuzu aktif.\n" +
                    "- Runtime seçimler eligible havuzdan random üretilir; eski " +
                    "fixed Step 1/2 helper'ları kullanılmaz.\n" +
                    "- Link/Split, Merged/Mass, Wetness/Conductivity ve " +
                    "Recovery/Resonance kategorileri bağlandı.\n" +
                    "- One-time reward'lar run boyunca persistent tutulur; kısa history yalnız offer tekrarını azaltır.\n" +
                    "- Aynı üçlü choice mümkün olduğunca yeniden üretilmez.\n" +
                    "- Reward controller ve mevcut reward overlay reuse edildi.\n\n" +
                    "Ctrl+S ile sahneyi kaydet, ardından Play Mode'da doğrula.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5 - Apply Vertical Slice Lock")]
        public static void ApplyVerticalSliceLock()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Vertical Slice Lock uygulanamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            MicroRunOverlay runOverlay =
                Object.FindAnyObjectByType<MicroRunOverlay>();
            MidRunRewardController rewards =
                Object.FindAnyObjectByType<MidRunRewardController>();
            DropletSplitSystem splitSystem =
                Object.FindAnyObjectByType<DropletSplitSystem>();
            ArcHunterVisual hunterVisual =
                Object.FindAnyObjectByType<ArcHunterVisual>();
            FoggedWindowWorldBackground background =
                Object.FindAnyObjectByType<FoggedWindowWorldBackground>();
            VideoPlayer backgroundVideo = background != null
                ? background.GetComponent<VideoPlayer>()
                : null;

            DropletController primary = splitSystem != null
                ? splitSystem.GetComponent<DropletController>()
                : null;

            if (run == null || runOverlay == null || rewards == null ||
                splitSystem == null || primary == null || hunterVisual == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Vertical Slice Lock uygulanamadı",
                    "MicroRunController, MicroRunOverlay, MidRunRewardController, " +
                    "DropletSplitSystem/DropletController veya ArcHunterVisual eksik.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(primary, "Lock Merged Movement Tuning");
            primary.ConfigureMergedMovement(0.90f);
            EditorUtility.SetDirty(primary);

            Undo.RecordObject(run, "Lock World 1 Run Tuning");
            run.ConfigureEncounterPresentation(0.80f);
            run.ConfigureWorldNavigation(3.75f);
            run.ConfigureWorldProgressScope(1, 3);
            run.ConfigureEncounterMovementModes(new[]
            {
                ArcHunterMovementMode.Chase,
                ArcHunterMovementMode.Strafe,
                ArcHunterMovementMode.WetRoutePressure
            });
            EditorUtility.SetDirty(run);

            Undo.RecordObject(runOverlay, "Lock Run Presentation Tuning");
            runOverlay.ConfigurePresentation(
                completionBeatSeconds: 0.80f,
                finalIntroSeconds: 0.65f,
                completeFadeSeconds: 0.18f);
            EditorUtility.SetDirty(runOverlay);

            Undo.RecordObject(rewards, "Lock Reward Pool Runtime State");
            rewards.ConfigureRewardPool(
                historyLength: 3,
                avoidChoiceSetRepeats: true);
            EditorUtility.SetDirty(rewards);

            Undo.RecordObject(hunterVisual, "Lock Combat Readability");
            hunterVisual.ConfigureHealthReadability(
                new Color(0.42f, 0.58f, 0.62f, 1f),
                tintStrength: 0.50f,
                minimumAlpha: 0.68f);
            hunterVisual.ConfigureConductionOutputReadability(
                enabled: true,
                smoothTime: 0.22f,
                holdSeconds: 0.15f,
                fadeSeconds: 0.70f);
            EditorUtility.SetDirty(hunterVisual);

            if (backgroundVideo != null)
            {
                Undo.RecordObject(backgroundVideo, "Lock Video Startup Readiness");
                backgroundVideo.playOnAwake = false;
                backgroundVideo.waitForFirstFrame = true;
                EditorUtility.SetDirty(backgroundVideo);
            }

            MarkDirtyAndSelect(run.gameObject);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5 Vertical Slice Lock",
                    "Lock uygulandı.\n\n" +
                    "- Merged movement 0.90x scene tuning'e yazıldı.\n" +
                    "- Encounter completion beat 0.80s tek runtime kaynağıyla hizalandı.\n" +
                    "- World 1 progress scope 1..3 olarak kilitlendi.\n" +
                    "- Encounter movement mode dizisi Chase / Strafe / Wet-Route olarak yazıldı.\n" +
                    "- Reward controller 2-step limitinden bağımsız çalışır; one-time state run boyunca korunur.\n" +
                    "- CONDUCTION output ve health degradation feedback aktif.\n" +
                    "- Background VideoPlayer playOnAwake kapalı; ilk frame readiness script tarafından yönetilir.\n\n" +
                    "Ctrl+S ile sahneyi kaydet. Sonra Play Mode smoke test yap.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5B - Apply World Segment Boundary")]
        public static void ApplyWorldSegmentBoundary()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - World Segment Boundary uygulanamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            WorldNavigationOverlay navigation =
                Object.FindAnyObjectByType<WorldNavigationOverlay>();
            FoggedWindowWorldBackground background =
                Object.FindAnyObjectByType<FoggedWindowWorldBackground>();
            GameObject arena = GameObject.Find("Arena");
            Transform routeRoot = arena != null
                ? arena.transform.Find("FirstWorldRouteV1")
                : null;
            Transform anchorA = routeRoot != null
                ? routeRoot.Find("AnchorA")
                : null;
            Transform anchorB = routeRoot != null
                ? routeRoot.Find("AnchorB")
                : null;
            Transform anchorC = routeRoot != null
                ? routeRoot.Find("AnchorC")
                : null;

            if (run == null || navigation == null || background == null ||
                routeRoot == null || anchorA == null || anchorB == null ||
                anchorC == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - World Segment Boundary uygulanamadı",
                    "MicroRunController, WorldNavigationOverlay, " +
                    "FoggedWindowWorldBackground veya FirstWorldRouteV1 " +
                    "AnchorA/B/C eksik.",
                    "Tamam");
                return;
            }

            Undo.RecordObject(run, "Configure World 1 Segment Boundary");
            run.ConfigureSingleWorldSegment("FOGGED_WINDOW");
            EditorUtility.SetDirty(run);

            Undo.RecordObject(navigation, "Bind World 1 Navigation Route");
            navigation.ConfigureWorldRoute(
                0,
                new[] { anchorA, anchorB, anchorC });
            EditorUtility.SetDirty(navigation);

            Undo.RecordObject(background, "Bind Fogged Background To World 1");
            background.ConfigureWorldProgressBinding(0);
            EditorUtility.SetDirty(background);

            MarkDirtyAndSelect(run.gameObject);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5B World Segment Boundary",
                    "Boundary uygulandı.\n\n" +
                    "- Mevcut serialized encounter route/movement tuning, FOGGED_WINDOW segmentine taşındı.\n" +
                    "- Run-global encounter sayısı ile world-local progress ayrıldı.\n" +
                    "- WorldNavigationOverlay yalnız World 1 route targetlarını World 1 sırasında kullanır.\n" +
                    "- FoggedWindowWorldBackground yalnız World 1 progressine tepki verir; sonraki world segmentinde yeniden blur resetlemez.\n" +
                    "- M26/Neon content veya world transition davranışı eklenmedi.\n\n" +
                    "Ctrl+S ile sahneyi kaydet. Ardından mevcut 3 encounter runını Play Mode'da tekrar doğrula.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5C - Apply Combat Feedback Lock")]
        public static void ApplyCombatFeedbackLock()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Feedback Lock uygulanamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            ArcHunter hunter = Object.FindAnyObjectByType<ArcHunter>();
            SplitLinkCombat combat =
                Object.FindAnyObjectByType<SplitLinkCombat>();
            ArcHunterVisual hunterVisual =
                Object.FindAnyObjectByType<ArcHunterVisual>();
            MicroBuildChoiceController buildController =
                Object.FindAnyObjectByType<MicroBuildChoiceController>();

            if (hunter == null || combat == null || hunterVisual == null ||
                buildController == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Feedback Lock uygulanamadı",
                    "ArcHunter, SplitLinkCombat, ArcHunterVisual veya " +
                    "MicroBuildChoiceController eksik.",
                    "Tamam");
                return;
            }

            ArcHunterHealthReadability healthReadability =
                GetOrAddComponent<ArcHunterHealthReadability>(
                    hunter.gameObject);

            Undo.RecordObject(
                hunterVisual,
                "Lock M25.5C Conduction Feedback");
            hunterVisual.ConfigureReadability(
                buildController,
                combat);
            hunterVisual.ConfigureHealthReadability(
                new Color(0.42f, 0.58f, 0.62f, 1f),
                tintStrength: 0.50f,
                minimumAlpha: 0.68f);
            hunterVisual.ConfigureConductionOutputReadability(
                enabled: true,
                smoothTime: 0.22f,
                holdSeconds: 0.15f,
                fadeSeconds: 0.70f);
            EditorUtility.SetDirty(hunterVisual);

            Undo.RecordObject(
                healthReadability,
                "Lock M25.5C Health Readability");
            healthReadability.Configure(
                hunter,
                padding: 0.09f,
                width: 0.025f,
                arcDegrees: 270f,
                newSortingOrder: 6);
            EditorUtility.SetDirty(healthReadability);

            MarkDirtyAndSelect(hunter.gameObject);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5C Combat Feedback Lock",
                    "Combat feedback kilitlendi.\n\n" +
                    "- ArcHunterVisual, build ve SplitLinkCombat referanslarına " +
                    "explicit bağlandı.\n" +
                    "- CONDUCTION değeri yalnız authoritative applied-output " +
                    "state aktifken görünür.\n" +
                    "- Health01 için ana combat-state ring'inden ayrı, küçük " +
                    "270 derece depletion arc eklendi.\n" +
                    "- Mevcut body degradation feedback korundu.\n" +
                    "- Damage formülü kopyalanmadı; yeni gameplay damage sistemi " +
                    "eklenmedi.\n\n" +
                    "Ctrl+S ile sahneyi kaydet. Ardından Play Mode'da 3 build ile " +
                    "kısa combat smoke testi yap.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/25.5D - Apply Combat Audio Vocabulary")]
        public static void ApplyCombatAudioVocabulary()
        {
            if (!IsPrototypeSceneOpen())
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Audio Vocabulary uygulanamadı",
                    "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                    "Tamam");
                return;
            }

            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            SplitLinkCombat combat =
                Object.FindAnyObjectByType<SplitLinkCombat>();
            ArcHunter hunter =
                Object.FindAnyObjectByType<ArcHunter>();
            ContaminationSystem contamination =
                Object.FindAnyObjectByType<ContaminationSystem>();
            MidRunRewardController rewards =
                Object.FindAnyObjectByType<MidRunRewardController>();

            if (run == null || combat == null || hunter == null ||
                contamination == null || rewards == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Audio Vocabulary uygulanamadı",
                    "MicroRunController, SplitLinkCombat, ArcHunter, " +
                    "ContaminationSystem veya MidRunRewardController eksik.",
                    "Tamam");
                return;
            }

            const string audioRootPath =
                "Assets/Glassfall/Audio/SFX/Prototype/M25_5D_Provisional/";

            AudioClip conduction = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "link_conduction_loop.wav");
            AudioClip telegraph = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "hunter_telegraph.wav");
            AudioClip commit = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "hunter_commit.wav");
            AudioClip contaminationHit = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "contamination_hit.wav");
            AudioClip cleanse = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "cleanse_resonance.wav");
            AudioClip encounterClear = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "encounter_clear.wav");
            AudioClip rewardSelect = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "reward_select.wav");
            AudioClip windowStabilized = AssetDatabase.LoadAssetAtPath<AudioClip>(
                audioRootPath + "window_stabilized.wav");

            if (conduction == null || telegraph == null || commit == null ||
                contaminationHit == null || cleanse == null ||
                encounterClear == null || rewardSelect == null ||
                windowStabilized == null)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Combat Audio Vocabulary uygulanamadı",
                    "M25.5D provisional WAV assetlerinden en az biri import " +
                    "edilemedi. Assets/Glassfall/Audio/SFX/Prototype/" +
                    "M25_5D_Provisional klasörünü kontrol et ve Unity importu " +
                    "tamamlandıktan sonra menüyü yeniden çalıştır.",
                    "Tamam");
                return;
            }

            CombatAudioFeedback feedback =
                GetOrAddComponent<CombatAudioFeedback>(run.gameObject);

            GameObject audioRoot =
                EnsureChild(run.gameObject, "M25_5D_Audio");
            GameObject gameplayAudioObject =
                EnsureChild(audioRoot, "GameplayOneShots");
            GameObject conductionAudioObject =
                EnsureChild(audioRoot, "ConductionLoop");
            GameObject hunterAudioObject =
                EnsureChild(hunter.gameObject, "M25_5D_HunterAudio");

            AudioSource gameplaySource =
                GetOrAddComponent<AudioSource>(gameplayAudioObject);
            AudioSource conductionSource =
                GetOrAddComponent<AudioSource>(conductionAudioObject);
            AudioSource hunterSource =
                GetOrAddComponent<AudioSource>(hunterAudioObject);

            Undo.RecordObject(
                gameplaySource,
                "Configure M25.5D Gameplay Audio Source");
            gameplaySource.playOnAwake = false;
            gameplaySource.loop = false;
            gameplaySource.spatialBlend = 0f;
            gameplaySource.dopplerLevel = 0f;
            gameplaySource.volume = 1f;
            gameplaySource.pitch = 1f;
            EditorUtility.SetDirty(gameplaySource);

            Undo.RecordObject(
                hunterSource,
                "Configure M25.5D Hunter Audio Source");
            hunterSource.playOnAwake = false;
            hunterSource.loop = false;
            hunterSource.spatialBlend = 0f;
            hunterSource.dopplerLevel = 0f;
            hunterSource.volume = 1f;
            hunterSource.pitch = 1f;
            EditorUtility.SetDirty(hunterSource);

            Undo.RecordObject(
                conductionSource,
                "Configure M25.5D Conduction Audio Source");
            conductionSource.playOnAwake = false;
            conductionSource.loop = true;
            conductionSource.spatialBlend = 0f;
            conductionSource.dopplerLevel = 0f;
            conductionSource.volume = 0f;
            conductionSource.pitch = 1f;
            conductionSource.clip = conduction;
            EditorUtility.SetDirty(conductionSource);

            Undo.RecordObject(
                feedback,
                "Configure M25.5D Combat Audio Feedback");
            feedback.ConfigureReferences(
                combat,
                hunter,
                contamination,
                rewards,
                run);
            feedback.ConfigureAudioSources(
                gameplaySource,
                hunterSource,
                conductionSource);
            feedback.ConfigureClips(
                conduction,
                telegraph,
                commit,
                contaminationHit,
                cleanse,
                encounterClear,
                rewardSelect,
                windowStabilized);
            feedback.ConfigureMix();
            EditorUtility.SetDirty(feedback);

            MarkDirtyAndSelect(audioRoot);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - M25.5D Combat Audio Vocabulary",
                    "Provisional combat audio vocabulary bağlandı.\n\n" +
                    "- Link conduction: tek loop AudioSource; authoritative " +
                    "link-hit state ile fade in/out, conductivity ile volume/" +
                    "pitch response.\n" +
                    "- Arc Hunter Telegraph ve Commit: state transition " +
                    "başına tek one-shot.\n" +
                    "- Contamination hit ve cleanse: presentation pulse " +
                    "edge'lerinden debounce edilmiş one-shot.\n" +
                    "- Encounter clear, reward select ve WINDOW STABILIZED " +
                    "ayrı cue'lara bağlı.\n" +
                    "- Final completion'da encounter-clear ile completion " +
                    "sesinin üst üste binmesi engellendi.\n" +
                    "- WAV'lar provisional ve local-synthesized; production " +
                    "asset değildir.\n\n" +
                    "Ctrl+S ile sahneyi kaydet. Sonra Play Mode'da tam bir run " +
                    "ile audio smoke testi yap.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/26 - Setup Neon Window World V1")]
        public static void SetupNeonWindowWorldV1()
        {
            if (!TrySetupNeonWindowWorldV1(showDialogs: true))
                return;

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Neon Window World V1",
                    "M26 kuruldu.\n\n" +
                    "- FOGGED_WINDOW ve NEON_WINDOW üçer encounter olarak bağlandı.\n" +
                    "- World 1 route ve movement tuning'i korunarak reuse edildi.\n" +
                    "- Neon encounter sırası Strafe / Wet-Route / Chase olarak ayarlandı.\n" +
                    "- World handoff wetness'i temizler; reward, contamination ve mass state korunur.\n" +
                    "- Fogged ve Neon presentation root'ları mevcut run segmentine göre switch edilir.\n" +
                    "- Neon backdrop/reflection ve minimum ambience/transition sting runtime procedural üretilir.\n\n" +
                    "Ctrl+S ile sahneyi kaydet ve tam iki-world run doğrulaması yap.",
                    "Tamam");
            }
        }

        [MenuItem(
            "Tools/Glassfall/Gameplay V1/27.1 - Apply Neon Video Background")]
        public static void ApplyNeonVideoBackground()
        {
            if (!TryApplyNeonVideoBackground(showDialogs: true))
                return;

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Glassfall - Neon Video Background",
                    "Neon Window video background bağlandı.\n\n" +
                    "- Video yalnız NEON_WINDOW aktifken hazırlanır ve oynar.\n" +
                    "- İlk decoded frame gelene kadar video yüzeyi gizlidir.\n" +
                    "- Decode/prepare hatasında mevcut procedural Neon görünümü korunur.\n" +
                    "- Fogged Window ile aynı VideoBackgroundBlur shader/tuning zinciri kullanılır.\n" +
                    "- Video sesi kapalı ve loop açıktır.\n\n" +
                    "Ctrl+S ile sahneyi kaydet ve Fogged → Neon geçişini Play Mode'da doğrula.",
                    "Tamam");
            }
        }

        public static void ApplyNeonVideoBackgroundBatch()
        {
            const string scenePath =
                "Assets/Glassfall/Scenes/Prototype.unity";
            if (!IsPrototypeSceneOpen())
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (!TryApplyNeonVideoBackground(showDialogs: false))
            {
                throw new System.InvalidOperationException(
                    "Neon video background setup could not resolve required dependencies.");
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new System.InvalidOperationException(
                    "Neon video background setup could not save Prototype.unity.");
            }
        }

        private static bool TryApplyNeonVideoBackground(bool showDialogs)
        {
            if (!IsPrototypeSceneOpen())
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Glassfall - Neon Video Background kurulamadı",
                        "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                        "Tamam");
                }
                return false;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            const string videoPath =
                "Assets/Glassfall/Art/NeonWindow/NeonWindowBackgroundLoop.mp4";
            const string shaderPath =
                "Assets/Glassfall/Presentation/VideoBackgroundBlur.shader";

            VideoClip videoClip =
                AssetDatabase.LoadAssetAtPath<VideoClip>(videoPath);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            GameObject arena = GameObject.Find("Arena");
            Transform neonTransform = arena != null
                ? arena.transform.Find("NeonWindowWorldV1")
                : null;
            GameObject neonRoot = neonTransform != null
                ? neonTransform.gameObject
                : null;
            Camera mainCamera = Camera.main;
            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            GameObject boundsObject = GameObject.Find("NeurtalSpawnArea") ??
                GameObject.Find("NeutralSpawnArea");
            Collider2D worldBounds = boundsObject != null
                ? boundsObject.GetComponent<Collider2D>()
                : null;

            if (videoClip == null || shader == null || arena == null ||
                neonRoot == null || mainCamera == null || run == null ||
                worldBounds == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Glassfall - Neon Video Background kurulamadı",
                        "NeonWindowBackgroundLoop.mp4, VideoBackgroundBlur shader, " +
                        "Arena/NeonWindowWorldV1, Main Camera, canonical world bounds " +
                        "veya MicroRunController bulunamadı.",
                        "Tamam");
                }
                return false;
            }

            NeonWindowWorldPresentation proceduralPresentation =
                neonRoot.GetComponent<NeonWindowWorldPresentation>();
            if (proceduralPresentation == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Glassfall - Neon Video Background kurulamadı",
                        "Mevcut NeonWindowWorldPresentation bulunamadı. " +
                        "Önce Setup 26'yı çalıştır.",
                        "Tamam");
                }
                return false;
            }

            GameObject surface = EnsureChild(
                neonRoot,
                "NeonWindowVideoSurface");
            Undo.RecordObject(
                surface.transform,
                "Configure Neon Video Surface");
            surface.transform.localPosition = Vector3.zero;
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = Vector3.one;

            MeshFilter meshFilter = GetOrAddComponent<MeshFilter>(surface);
            MeshRenderer meshRenderer =
                GetOrAddComponent<MeshRenderer>(surface);
            NeonWindowVideoBackground videoBackground =
                GetOrAddComponent<NeonWindowVideoBackground>(neonRoot);
            NeonWindowGlassOverlay legacyGlassOverlay =
                neonRoot.GetComponent<NeonWindowGlassOverlay>();
            if (legacyGlassOverlay != null)
                Undo.DestroyObjectImmediate(legacyGlassOverlay);
            Transform legacyGlassSurface = neonRoot.transform.Find(
                "NeonWindowPixelGlassOverlaySurface");
            if (legacyGlassSurface != null)
                Undo.DestroyObjectImmediate(legacyGlassSurface.gameObject);
            VideoPlayer videoPlayer = GetOrAddComponent<VideoPlayer>(neonRoot);

            Undo.RecordObject(
                videoBackground,
                "Configure Neon Video Background");
            videoBackground.Configure(
                videoClip,
                mainCamera,
                worldBounds,
                run,
                meshRenderer,
                meshFilter,
                shader,
                videoOpacity: 0.70f,
                videoBrightness: 0.86f,
                videoSaturation: 0.90f,
                videoTint: new Color(0.90f, 0.96f, 1f, 1f),
                videoSortingOrder: -1091,
                videoWorldZ: 6.0f,
                coverage: 0.995f);
            videoBackground.ConfigureFoggedGlassTreatment(
                enabled: true,
                worldSegmentIndex: 1);

            Undo.RecordObject(videoPlayer, "Configure Neon Video Player");
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = meshRenderer;
            videoPlayer.targetMaterialProperty = "_MainTex";
            videoPlayer.targetCamera = null;
            videoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            videoPlayer.isLooping = true;
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.enabled = true;

            meshRenderer.enabled = false;
            EditorUtility.SetDirty(videoBackground);
            EditorUtility.SetDirty(videoPlayer);
            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(meshRenderer);
            EditorUtility.SetDirty(neonRoot);
            MarkDirtyAndSelect(neonRoot);
            return true;
        }

        public static void SetupNeonWindowWorldV1Batch()
        {
            const string scenePath =
                "Assets/Glassfall/Scenes/Prototype.unity";
            if (!IsPrototypeSceneOpen())
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (!TrySetupNeonWindowWorldV1(showDialogs: false))
                throw new System.InvalidOperationException(
                    "M26 Neon Window setup could not resolve required scene dependencies.");

            if (!EditorSceneManager.SaveOpenScenes())
                throw new System.InvalidOperationException(
                    "M26 Neon Window setup could not save Prototype.unity.");
        }

        private static bool TrySetupNeonWindowWorldV1(bool showDialogs)
        {
            if (!IsPrototypeSceneOpen())
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Glassfall - Neon Window World V1 kurulamadı",
                        "Önce Assets/Glassfall/Scenes/Prototype.unity sahnesini aç.",
                        "Tamam");
                }
                return false;
            }

            GameObject arena = GameObject.Find("Arena");
            GameObject boundsObject = GameObject.Find("NeurtalSpawnArea") ??
                GameObject.Find("NeutralSpawnArea");
            Collider2D worldBounds = boundsObject != null
                ? boundsObject.GetComponent<Collider2D>()
                : null;
            Camera mainCamera = Camera.main;
            MicroRunController run =
                Object.FindAnyObjectByType<MicroRunController>();
            WetnessField wetnessField =
                Object.FindAnyObjectByType<WetnessField>();
            WorldNavigationOverlay navigation =
                Object.FindAnyObjectByType<WorldNavigationOverlay>();
            FoggedWindowWorldBackground foggedBackground =
                Object.FindAnyObjectByType<FoggedWindowWorldBackground>();
            GameObject foggedRoot = foggedBackground != null
                ? foggedBackground.gameObject
                : arena != null
                    ? arena.transform.Find("FoggedWindowWorldV1")?.gameObject
                    : null;

            if (arena == null || worldBounds == null || mainCamera == null ||
                run == null || wetnessField == null || navigation == null ||
                foggedRoot == null)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Glassfall - Neon Window World V1 kurulamadı",
                        "Arena, canonical world bounds, Main Camera, MicroRunController, " +
                        "WetnessField, WorldNavigationOverlay veya FoggedWindowWorldV1 eksik.",
                        "Tamam");
                }
                return false;
            }

            if (!run.TryGetWorldSegmentCopy(
                    0,
                    out MicroRunWorldSegment existingFogged))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Glassfall - Neon Window World V1 kurulamadı",
                        "Mevcut FOGGED_WINDOW segmenti bulunamadı. Önce 25.5B world " +
                        "segment boundary setup'ını çalıştır.",
                        "Tamam");
                }
                return false;
            }

            MicroRunWorldSegment foggedSegment = new MicroRunWorldSegment(
                "FOGGED_WINDOW",
                existingFogged.CopyEncounterPositions(),
                existingFogged.CopyMovementModes());
            MicroRunWorldSegment neonSegment = new MicroRunWorldSegment(
                "NEON_WINDOW",
                new[]
                {
                    new Vector2(-12.0f, 6.2f),
                    new Vector2(6.8f, -6.4f),
                    new Vector2(14.2f, 5.5f)
                },
                new[]
                {
                    ArcHunterMovementMode.Strafe,
                    ArcHunterMovementMode.WetRoutePressure,
                    ArcHunterMovementMode.Chase
                });

            Undo.RecordObject(run, "Configure M26 World Segments");
            run.ConfigureWorldSegments(new[] { foggedSegment, neonSegment });
            run.ConfigureWorldHandoff(wetnessField);
            EditorUtility.SetDirty(run);

            Undo.RecordObject(
                foggedBackground,
                "Keep Fogged Presentation Bound To World 1");
            foggedBackground.ConfigureWorldProgressBinding(0);
            EditorUtility.SetDirty(foggedBackground);

            GameObject neonRoot = EnsureChild(arena, "NeonWindowWorldV1");
            Undo.RecordObject(neonRoot.transform, "Configure Neon Window Root");
            neonRoot.transform.localPosition = Vector3.zero;
            neonRoot.transform.localRotation = Quaternion.identity;
            neonRoot.transform.localScale = Vector3.one;

            GameObject ambienceObject = EnsureChild(
                neonRoot,
                "NeonAmbience");
            GameObject transitionObject = EnsureChild(
                neonRoot,
                "NeonTransition");
            AudioSource ambienceSource =
                GetOrAddComponent<AudioSource>(ambienceObject);
            AudioSource transitionSource =
                GetOrAddComponent<AudioSource>(transitionObject);
            ConfigureNeonAudioSource(ambienceSource, loop: true);
            ConfigureNeonAudioSource(transitionSource, loop: false);

            NeonWindowWorldPresentation neonPresentation =
                GetOrAddComponent<NeonWindowWorldPresentation>(neonRoot);
            Undo.RecordObject(
                neonPresentation,
                "Configure Neon Window Presentation");
            neonPresentation.Configure(
                mainCamera,
                worldBounds,
                run,
                ambienceSource,
                transitionSource);
            EditorUtility.SetDirty(neonPresentation);

            WorldPresentationRouter router =
                GetOrAddComponent<WorldPresentationRouter>(run.gameObject);
            Undo.RecordObject(router, "Configure M26 Presentation Router");
            router.Configure(
                run,
                foggedRoot,
                neonRoot,
                foggedSegmentIndex: 0,
                neonSegmentIndex: 1);
            EditorUtility.SetDirty(router);

            if (!Application.isPlaying)
            {
                foggedRoot.SetActive(true);
                neonRoot.SetActive(false);
            }

            EditorUtility.SetDirty(ambienceSource);
            EditorUtility.SetDirty(transitionSource);
            EditorUtility.SetDirty(neonRoot);
            MarkDirtyAndSelect(run.gameObject);
            return true;
        }

        private static void ConfigureNeonAudioSource(
            AudioSource source,
            bool loop)
        {
            if (source == null)
                return;

            Undo.RecordObject(source, "Configure Neon Window Audio Source");
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.pitch = 1f;
        }

        private static void RemoveTiledGlassFloorPresentation(GameObject arena)
        {
            if (arena == null)
                return;

            Transform floor = arena.transform.Find("GlassFloorPixelV1");
            if (floor != null)
                Undo.DestroyObjectImmediate(floor.gameObject);
        }

        private static void ConfigureDirtyGlassLandmarks(
            GameObject arena,
            Sprite dirtyGlassSprite)
        {
            GameObject surfaceRoot = EnsureChild(
                arena,
                "FirstWorldSurfaceV1");
            surfaceRoot.transform.localPosition = Vector3.zero;
            surfaceRoot.transform.localRotation = Quaternion.identity;
            surfaceRoot.transform.localScale = Vector3.one;

            GameObject landmarkRoot = EnsureChild(
                surfaceRoot,
                "DirtyGlassLandmarks");
            landmarkRoot.transform.localPosition = Vector3.zero;
            landmarkRoot.transform.localRotation = Quaternion.identity;
            landmarkRoot.transform.localScale = Vector3.one;

            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_A_01",
                dirtyGlassSprite,
                new Vector2(-14.2f, -1.4f),
                new Vector2(1.48f, 0.90f),
                -12f,
                0.10f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_A_02",
                dirtyGlassSprite,
                new Vector2(-12.2f, 1.9f),
                new Vector2(0.90f, 0.68f),
                18f,
                0.085f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_B_01",
                dirtyGlassSprite,
                new Vector2(-3.6f, 6.2f),
                new Vector2(1.32f, 0.86f),
                8f,
                0.11f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_B_02",
                dirtyGlassSprite,
                new Vector2(1.4f, 7.1f),
                new Vector2(0.86f, 0.64f),
                -20f,
                0.09f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_B_03",
                dirtyGlassSprite,
                new Vector2(4.3f, 5.6f),
                new Vector2(0.75f, 0.75f),
                30f,
                0.08f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_C_01",
                dirtyGlassSprite,
                new Vector2(11.2f, 1.9f),
                new Vector2(1.48f, 0.95f),
                -15f,
                0.12f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_C_02",
                dirtyGlassSprite,
                new Vector2(14.2f, 3.6f),
                new Vector2(0.86f, 0.62f),
                22f,
                0.095f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_C_03",
                dirtyGlassSprite,
                new Vector2(13.3f, -0.5f),
                new Vector2(0.73f, 0.55f),
                -8f,
                0.085f);
            ConfigureDirtyGlassLandmark(
                landmarkRoot,
                "Dirty_Transition_01",
                dirtyGlassSprite,
                new Vector2(-7.1f, -4.8f),
                new Vector2(0.80f, 0.58f),
                35f,
                0.075f);

            EditorUtility.SetDirty(surfaceRoot);
            EditorUtility.SetDirty(landmarkRoot);
        }

        private static void ConfigureDirtyGlassLandmark(
            GameObject parent,
            string landmarkName,
            Sprite sprite,
            Vector2 position,
            Vector2 scale,
            float rotationDegrees,
            float opacity)
        {
            GameObject landmark = EnsureChild(parent, landmarkName);
            Undo.RecordObject(landmark.transform, "Configure " + landmarkName);
            landmark.transform.localPosition =
                new Vector3(position.x, position.y, 0f);
            landmark.transform.localRotation =
                Quaternion.Euler(0f, 0f, rotationDegrees);
            landmark.transform.localScale =
                new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer renderer =
                GetOrAddComponent<SpriteRenderer>(landmark);
            Undo.RecordObject(renderer, "Configure " + landmarkName + " Renderer");
            renderer.sprite = sprite;
            renderer.color = new Color(
                0.80f,
                0.88f,
                0.90f,
                Mathf.Clamp01(opacity));
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = -12;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.enabled = true;

            EditorUtility.SetDirty(landmark);
            EditorUtility.SetDirty(landmark.transform);
            EditorUtility.SetDirty(renderer);
        }

        private static void ConfigureDirtyGlassLandmarkImporter(
            string assetPath,
            float pixelsPerUnit)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            bool changed =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !importer.alphaIsTransparency ||
                importer.wrapMode != TextureWrapMode.Clamp;

            if (!changed)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static void ConfigurePlayableBoundaryWalls(
            GameObject arena,
            BoxCollider2D worldBounds)
        {
            const float wallThickness = 0.5f;

            Bounds bounds = worldBounds.bounds;
            float halfWidth = bounds.extents.x;
            float halfHeight = bounds.extents.y;
            Vector2 center = bounds.center;

            ConfigurePlayableBoundaryWall(
                arena,
                "Wall_Left",
                new Vector2(center.x - halfWidth - wallThickness * 0.5f, center.y),
                new Vector2(wallThickness, bounds.size.y + wallThickness));
            ConfigurePlayableBoundaryWall(
                arena,
                "Wall_Right",
                new Vector2(center.x + halfWidth + wallThickness * 0.5f, center.y),
                new Vector2(wallThickness, bounds.size.y + wallThickness));
            ConfigurePlayableBoundaryWall(
                arena,
                "Wall_Bottom",
                new Vector2(center.x, center.y - halfHeight - wallThickness * 0.5f),
                new Vector2(bounds.size.x + wallThickness * 2f, wallThickness));
            ConfigurePlayableBoundaryWall(
                arena,
                "Wall_Top",
                new Vector2(center.x, center.y + halfHeight + wallThickness * 0.5f),
                new Vector2(bounds.size.x + wallThickness * 2f, wallThickness));
        }

        private static void ConfigurePlayableBoundaryWall(
            GameObject arena,
            string canonicalName,
            Vector2 worldPosition,
            Vector2 worldSize)
        {
            GameObject wall = FindDirectChildByTrimmedName(
                arena.transform,
                canonicalName);
            if (wall == null)
                wall = EnsureChild(arena, canonicalName);

            Undo.RecordObject(wall, "Configure " + canonicalName);
            wall.name = canonicalName;
            wall.SetActive(true);

            int environmentLayer = LayerMask.NameToLayer("Enviroment");
            if (environmentLayer >= 0)
                wall.layer = environmentLayer;

            Transform wallTransform = wall.transform;
            Undo.RecordObject(wallTransform, "Position " + canonicalName);
            wallTransform.position = new Vector3(
                worldPosition.x,
                worldPosition.y,
                wallTransform.position.z);
            wallTransform.rotation = Quaternion.identity;

            Vector3 parentScale = wallTransform.parent != null
                ? wallTransform.parent.lossyScale
                : Vector3.one;
            wallTransform.localScale = new Vector3(
                worldSize.x / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
                worldSize.y / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
                1f);

            BoxCollider2D wallCollider =
                GetOrAddComponent<BoxCollider2D>(wall);
            Undo.RecordObject(wallCollider, "Configure " + canonicalName + " Collider");
            wallCollider.enabled = true;
            wallCollider.isTrigger = false;
            wallCollider.offset = Vector2.zero;
            wallCollider.size = Vector2.one;

            EditorUtility.SetDirty(wall);
            EditorUtility.SetDirty(wallTransform);
            EditorUtility.SetDirty(wallCollider);
        }

        private static GameObject FindDirectChildByTrimmedName(
            Transform parent,
            string expectedName)
        {
            if (parent == null)
                return null;

            foreach (Transform child in parent)
            {
                if (string.Equals(
                        child.name.Trim(),
                        expectedName,
                        System.StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static void ConfigureRouteMarker(
            GameObject parent,
            string markerName,
            Vector2 position)
        {
            GameObject marker = EnsureChild(parent, markerName);
            Undo.RecordObject(marker.transform, "Configure Route Marker " + markerName);
            marker.transform.localPosition = new Vector3(position.x, position.y, 0f);
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(marker.transform);
        }

        private static void RemoveObsoleteBackgroundLayers(GameObject backgroundRoot)
        {
            string[] obsoleteChildren =
            {
                "FoggedWindowFarBlur",
                "FoggedWindowMidBlur",
                "FoggedWindowNearBlur",
                "FoggedGlassOverlay",
                "FoggedGlassStreakOverlay"
            };

            foreach (string childName in obsoleteChildren)
            {
                Transform child = backgroundRoot.transform.Find(childName);
                if (child != null)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static SpriteRenderer FindChildSpriteRenderer(Component parent)
        {
            Transform child = parent.transform.Find("PixelSprite");
            return child != null
                ? child.GetComponent<SpriteRenderer>()
                : null;
        }

        private static SpriteRenderer EnsurePixelSpriteChild(
            GameObject parent,
            string childName,
            Sprite sprite,
            int sortingOrder)
        {
            GameObject child = EnsureChild(parent, childName);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(child);
            Undo.RecordObject(renderer, "Configure " + parent.name + " Pixel Sprite");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = sortingOrder;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            EditorUtility.SetDirty(renderer);
            return renderer;
        }

        private static void ConfigurePixelSpriteImporter(
            string assetPath,
            float pixelsPerUnit,
            bool fullRect = false)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            TextureImporterSettings spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            bool needsFullRect =
                fullRect && spriteSettings.spriteMeshType != SpriteMeshType.FullRect;

            bool changed =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !importer.alphaIsTransparency ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                needsFullRect;

            if (!changed)
                return;

            if (needsFullRect)
            {
                spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(spriteSettings);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static void DisableLegacyStaticRenderer(
            Transform visualRoot,
            string childName)
        {
            if (visualRoot == null)
                return;

            Transform child = visualRoot.Find(childName);
            if (child == null)
                return;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null || !renderer.enabled)
                return;

            Undo.RecordObject(renderer, "Disable Legacy Droplet " + childName);
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }
}
#endif
