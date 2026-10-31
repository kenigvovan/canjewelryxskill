using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using canjewelry.src;
using HarmonyLib;
using canjewelry.src.integration;
using canjewelry.src.utils;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using XLib.XLeveling;

namespace canjewelryxskill
{
    public class canjewelryxskillModSystem : ModSystem
    {
        private Lapidary? _lapidary;
        private Jeweler? _jeweler;
        private canjewelry.src.canjewelry? _core;
        private CanJewelryXskillConfig _config = new();
        private Harmony? _harmony;
        private bool _xlibPresent;

        public override double ExecuteOrder() => 0.5;

        // Skills must exist before XLeveling.AssetsLoaded runs: that is where XLeveling walks
        // its registered skills once and feeds every SpecialisationID into the "specialisations"
        // LimitationRequirement. XLeveling sits at ExecuteOrder 0.15 and we sit at 0.5, so
        // creating the skills in our own AssetsLoaded missed that pass entirely — master_lapidary
        // and master_jeweler then never counted against the 1+4 specialisation limit. StartPre is
        // safe: XLeveling builds its SkillSetTemplate in its constructor. Lang isn't loaded yet
        // here, hence the deferred localization in AssetsLoaded below.
        public override void StartPre(ICoreAPI api)
        {
            // Load (or create-with-defaults) the balance config. FillMissing keeps the file
            // forwards-compatible after we add new perks.
            try
            {
                var loaded = api.LoadModConfig<CanJewelryXskillConfig>("canjewelryxskill.json");
                if (loaded != null) _config = loaded;
            }
            catch (Exception ex)
            {
                Mod.Logger.Error("Failed to load canjewelryxskill.json — using defaults. {0}", ex);
            }
            CanJewelryXskillConfig.FillMissing(_config);
            api.StoreModConfig(_config, "canjewelryxskill.json");

            // XLib ships under several mod ids depending on the fork (xlib, xlibfork,
            // xlibxaldisclasses, ...). They all expose the same XLib.XLeveling API from an
            // assembly named "xlib", so one build works with any of them — but we can't list
            // them in modinfo dependencies (there is no "or"). Detect at runtime instead and
            // keep every XLib-typed call behind this flag so the mod stays loadable without it.
            _xlibPresent = DetectXLib(api);
            if (!_xlibPresent)
            {
                Mod.Logger.Warning("No XLib variant found — Lapidary/Jeweler skills stay disabled.");
                return;
            }

            try
            {
                CreateSkills(api);
            }
            catch (Exception ex)
            {
                // An XLib fork with a diverging API surface would throw here (Missing*Exception).
                // The event handlers all bail on a null skill, so we degrade instead of taking
                // the world down during startup.
                Mod.Logger.Error("Incompatible XLib variant — skills disabled. {0}", ex);
            }
        }

        public override void Start(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client)
            {
                _harmony = new Harmony("canjewelryxskill.patches");
                _harmony.Patch(
                    typeof(CollectibleObject).GetMethod("GetHeldItemInfo"),
                    postfix: new HarmonyMethod(typeof(CANJewelryXskillPatches)
                        .GetMethod(nameof(CANJewelryXskillPatches.Postfix_GetHeldItemInfo)))
                );
            }

            if (!_xlibPresent) return;

            _core = api.ModLoader.GetModSystem<canjewelry.src.canjewelry>();
            if (_core == null)
            {
                Mod.Logger.Warning("canjewelry mod system not found — skills will not receive events.");
                return;
            }

            _core.OnEncrustBuff += OnEncrust;
            _core.OnXpAwarded   += OnXp;
            _core.OnCutGem      += OnCut;
            _core.OnGrindStep   += OnGrind;
            _core.OnGrindStageStart += OnGrindStageStart;
            _core.OnPanGem      += OnPan;
            _core.OnPanMaterialTake += OnPanMaterialTake;
            _core.OnCanPanDrop += OnCanPanDrop;

            _core.OnExtractGem  += OnExtract;
            _core.OnCanExtract  += OnCanExtract;
            _core.OnWireDrawn   += OnWireDraw;
            _core.OnCanInscribe += OnCanInscribe;
        }

        // The skills carry lang keys until here — lang files are only guaranteed to be loaded by
        // AssetsLoaded, and the skills themselves have to be built much earlier (see StartPre).
        // Our ExecuteOrder 0.5 puts this after XSkills (0.25) does the same pass on its skills,
        // so group names resolve to identical strings and both land in the same GUI tab.
        public override void AssetsLoaded(ICoreAPI api)
        {
            if (!_xlibPresent) return;
            try
            {
                LocalizeSkills();
            }
            catch (Exception ex)
            {
                Mod.Logger.Error("Failed to localize skill names — raw lang keys will show. {0}", ex);
            }
        }

        // Kept out of StartPre and un-inlined: Lapidary/Jeweler derive from XLib's Skill, so
        // JIT-ing a method that constructs them would try to resolve xlib.dll. Without this the
        // _xlibPresent guard above would come too late and we'd throw instead of degrading.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CreateSkills(ICoreAPI api)
        {
            try
            {
                _lapidary = new Lapidary(api, _config.Lapidary);
                _jeweler  = new Jeweler(api,  _config.Jeweler);
            }
            catch
            {
                _lapidary = null;
                _jeweler  = null;
                throw;
            }
        }

        // Same reasoning as CreateSkills: touching Skill/Ability types needs xlib resolved.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void LocalizeSkills()
        {
            foreach (Skill? skill in new Skill?[] { _lapidary, _jeweler })
            {
                if (skill == null) continue;
                skill.DisplayName = Lang.GetUnformatted(skill.DisplayName);
                skill.Group       = Lang.GetUnformatted(skill.Group);
                foreach (Ability ability in skill.Abilities)
                {
                    ability.DisplayName = Lang.GetUnformatted(ability.DisplayName);
                    ability.Description = Lang.GetUnformatted(ability.Description);
                }
            }
        }

        private static bool DetectXLib(ICoreAPI api)
        {
            foreach (var mod in api.ModLoader.Mods)
            {
                string? id = mod.Info?.ModID;
                if (id != null && id.StartsWith("xlib", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Items/blocks aren't in the registry yet during Start(); resolving drop codes there
        // logs spurious "Failed resolving..." warnings. AssetsFinalized runs after items are
        // registered but before block.OnLoaded reads runtimeExtraPanDrops in CANBlockPan.
        public override void AssetsFinalize(ICoreAPI api)
        {
            if (_core == null) return;
            RegisterProspectorDrops(api);
        }

        public override void Dispose()
        {
            _harmony?.UnpatchAll("canjewelryxskill.patches");
            if (_core == null) return;
            _core.OnEncrustBuff -= OnEncrust;
            _core.OnXpAwarded   -= OnXp;
            _core.OnCutGem      -= OnCut;
            _core.OnGrindStep   -= OnGrind;
            _core.OnGrindStageStart -= OnGrindStageStart;
            _core.OnPanGem      -= OnPan;
            _core.OnPanMaterialTake -= OnPanMaterialTake;
            _core.OnCanPanDrop -= OnCanPanDrop;
            _core.OnExtractGem  -= OnExtract;
            _core.OnCanExtract  -= OnCanExtract;
            _core.OnWireDrawn   -= OnWireDraw;
            _core.OnCanInscribe -= OnCanInscribe;
        }

        private void OnEncrust(EncrustEvent e)
        {
            if (e.Player == null || _jeweler == null) return;

            var steady = FindAbility(e.Player, "jeweler", _jeweler.SteadyHandId);
            if (steady != null && steady.Tier > 0)
            {
                float frac = steady.FValue(0);
                // Store on socket so OnExtract can reverse it and prevent compounding on re-encrust.
                e.Jewelry?.Attributes
                    ?.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)
                    ?.GetTreeAttribute("slot" + e.SocketIndex)
                    ?.SetFloat("steadyHandFraction", frac);
                e.Value *= 1f + frac;
            }

            ApplySynergistOnEncrust(e);
        }

        // Synergist bookkeeping: each socket carries `synergistFraction` while the bonus is
        // baked into its stored buff values. We retroactively bump matching sockets when a new
        // matching gem joins, and we strip the bonus when the matching count drops below 2.
        // Fires happen per-buff (e.g. baguette has 2), so flag-skipping makes ops idempotent.
        private void ApplySynergistOnEncrust(EncrustEvent e)
        {
            var synergist = FindAbility(e.Player, "jeweler", _jeweler.SynergistId);
            bool synergistActive = synergist != null && synergist.Tier > 0;

            string newGemType = e.Gem?.Collectible?.Variant?["gemtype"];
            if (string.IsNullOrEmpty(newGemType)) return;

            ITreeAttribute encTree = e.Jewelry?.Attributes?.GetTreeAttribute("canencrusted");
            if (encTree == null) return;

            ITreeAttribute currentSocket = encTree.GetTreeAttribute("slot" + e.SocketIndex);

            // Read the slot's old gem-type before core overwrites it. Empty string means
            // first-time encrust, otherwise we may need to roll back the old type's synergy.
            string oldGemType = currentSocket?.GetString("gemtype", "") ?? "";
            bool isSwapDifferent = !string.IsNullOrEmpty(oldGemType) && oldGemType != newGemType;

            // The current socket's old buff values are about to be overwritten by core, so any
            // prior synergistFraction on it is meaningless — clear it before re-applying.
            currentSocket?.RemoveAttribute("synergistFraction");

            if (synergistActive)
            {
                float fraction = synergist.FValue(0);
                int newMatches = CountMatchingExcept(encTree, newGemType, e.SocketIndex);
                // newMatches counts OTHER sockets of newGemType. After this encrust the total
                // becomes newMatches + 1; synergy needs >=2 → newMatches >= 1.
                if (newMatches >= 1)
                {
                    e.Value *= 1f + fraction;
                    currentSocket?.SetFloat("synergistFraction", fraction);

                    for (int i = 0; i < 8; i++)
                    {
                        if (i == e.SocketIndex) continue;
                        var slot = encTree.GetTreeAttribute("slot" + i);
                        if (slot == null) continue;
                        if (slot.GetString("gemtype", "") != newGemType) continue;
                        if (slot.GetFloat("synergistFraction", 0f) > 0f) continue; // already bumped
                        ApplySynergyToSocket(slot, fraction);
                    }
                }
            }

            // If this is a swap to a different gemtype, the OLD type may now have <2 sockets
            // and any synergy on the remaining ones needs to be peeled off.
            if (isSwapDifferent)
            {
                int oldRemaining = CountMatchingExcept(encTree, oldGemType, e.SocketIndex);
                if (oldRemaining < 2)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (i == e.SocketIndex) continue;
                        var slot = encTree.GetTreeAttribute("slot" + i);
                        if (slot == null) continue;
                        if (slot.GetString("gemtype", "") != oldGemType) continue;
                        RemoveSynergyFromSocket(slot);
                    }
                }
            }
        }

        private static void NormalizeExtractedGem(ExtractEvent e)
        {
            var socket = e.Jewelry?.Attributes
                ?.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)
                ?.GetTreeAttribute("slot" + e.SocketIndex);
            if (socket == null) return;

            float steady  = socket.GetFloat("steadyHandFraction", 0f);
            float synergy = socket.GetFloat("synergistFraction",  0f);
            float divisor = (1f + steady) * (1f + synergy);
            if (divisor <= 1f) return;

            var vals = (e.Gem?.Attributes?.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE)
                           ?[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute)?.value;
            if (vals == null) return;
            for (int i = 0; i < vals.Length; i++)
                vals[i] /= divisor;
        }

        private void ApplySynergistOnExtract(ExtractEvent e)
        {
            string removedGemType = e.Gem?.Collectible?.Variant?["gemtype"];
            if (string.IsNullOrEmpty(removedGemType)) return;

            ITreeAttribute encTree = e.Jewelry?.Attributes?.GetTreeAttribute("canencrusted");
            if (encTree == null) return;

            // Post-extract count = matching siblings excluding the slot being wiped.
            int remaining = CountMatchingExcept(encTree, removedGemType, e.SocketIndex);
            if (remaining >= 2) return;

            for (int i = 0; i < 8; i++)
            {
                if (i == e.SocketIndex) continue;
                var slot = encTree.GetTreeAttribute("slot" + i);
                if (slot == null) continue;
                if (slot.GetString("gemtype", "") != removedGemType) continue;
                RemoveSynergyFromSocket(slot);
            }
        }

        private void OnCanInscribe(CanInscribeEvent e)
        {
            if (e.Player == null || _jeweler == null) return;

            // Inscriber is a one-tier "unlock" perk; absence of investment means no inscribing.
            // The Allowed default is true server-side — denying here is the sole effect.
            var inscriber = FindAbility(e.Player, "jeweler", _jeweler.InscriberId);
            if (inscriber == null || inscriber.Tier == 0)
            {
                e.Allowed = false;
            }
        }

        private void OnCanExtract(CanExtractEvent e)
        {
            if (e.Player == null || _jeweler == null) return;

            // ExtractLicense is a one-tier unlock perk — mirrors Inscriber. Fires client-side
            // in the jeweler GUI to hide the Extract button for unskilled players.
            var license = FindAbility(e.Player, "jeweler", _jeweler.ExtractLicenseId);
            if (license == null || license.Tier == 0)
            {
                e.Allowed = false;
            }
        }

        private void OnWireDraw(WireDrawEvent e)
        {
            if (e.Player == null || _jeweler == null || e.Output == null) return;

            // WireWorker — chance for +1 wire-hank in the output stack. Pattern matches Panner:
            // tier % roll, +1 on success → averages to the spec's "+10/20/30% yield" wording.
            var wire = FindAbility(e.Player, "jeweler", _jeweler.WireWorkerId);
            if (wire != null && wire.Tier > 0)
                e.Output.StackSize += wire.Tier;

            // No XP for wire drawing — it's an upstream metalworking step, not a jeweler skill action.
        }

        private static int CountMatchingExcept(ITreeAttribute encTree, string gemType, int excludeIndex)
        {
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                if (i == excludeIndex) continue;
                var slot = encTree.GetTreeAttribute("slot" + i);
                if (slot == null) continue;
                if (slot.GetString("gemtype", "") == gemType) count++;
            }
            return count;
        }

        private static void ApplySynergyToSocket(ITreeAttribute slotTree, float fraction)
        {
            var values = (slotTree["encrustablebuffsvalues"] as FloatArrayAttribute)?.value;
            if (values != null)
            {
                for (int j = 0; j < values.Length; j++) values[j] *= 1f + fraction;
            }
            slotTree.SetFloat("synergistFraction", fraction);
        }

        private static void RemoveSynergyFromSocket(ITreeAttribute slotTree)
        {
            float fraction = slotTree.GetFloat("synergistFraction", 0f);
            if (fraction <= 0f) return;
            var values = (slotTree["encrustablebuffsvalues"] as FloatArrayAttribute)?.value;
            if (values != null)
            {
                for (int j = 0; j < values.Length; j++) values[j] /= 1f + fraction;
            }
            slotTree.RemoveAttribute("synergistFraction");
        }

        private void OnCut(CutEvent e)
        {
            if (e.Player == null || _lapidary == null) return;

            // Resourceful — chance to recover a smaller rough gem from the offcuts.
            // Downgrade by one size: normal→flawed, flawed→chipped, chipped→(nothing).
            var resourceful = FindAbility(e.Player, "lapidary", _lapidary.ResourcefulId);
            if (resourceful != null && resourceful.Tier > 0
                && e.Player.Entity.World.Rand.NextDouble() < resourceful.FValue(0))
            {
                ItemStack? downgraded = TryDowngradeRough(e.RoughGem, e.GemSize, e.Player.Entity.World);
                if (downgraded != null)
                {
                    e.RoughGem = downgraded;
                    e.ConsumeRough = false;
                }
            }

            // Award lapidary XP — companion-side, since core has no skill model.
            float xp = GetXp(_config.Lapidary, "cut");
            var master = FindAbility(e.Player, "lapidary", _lapidary.MasterLapidaryId);
            if (master != null && master.Tier > 0) xp *= 1f + master.FValue(0);

            var skillSet = XLeveling.Instance(e.Player.Entity.Api)?.IXLevelingAPI
                                    ?.GetPlayerSkillSet(e.Player);
            skillSet?.FindSkill("lapidary", false)?.AddExperience(xp, true);
        }

        private void OnGrind(GrindEvent e)
        {
            if (e.Player == null || _lapidary == null) return;

            // Polished — boost the multiplier applied to the buff at each grind stage.
            var polished = FindAbility(e.Player, "lapidary", _lapidary.PolishedId);
            if (polished != null && polished.Tier > 0)
            {
                e.Multiplier *= 1f + polished.FValue(0);
            }

            // MasterCut — extra multiplier for mastered cut types.
            // Tier 1: round only. Tier 2: round + pear. Tier 3: all types.
            var masterCut = FindAbility(e.Player, "lapidary", _lapidary.MasterCutId);
            if (masterCut != null && masterCut.Tier > 0)
            {
                var cutTree = e.Gem?.Attributes?.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE);
                string cutType = cutTree?.GetString(CANJWConstants.CUTTING_TYPE, "") ?? "";
                bool applies = masterCut.Tier switch
                {
                    1 => cutType == CANJWConstants.CUTTING_ROUND,
                    2 => cutType == CANJWConstants.CUTTING_ROUND || cutType == CANJWConstants.CUTTING_PEAR,
                    _ => !string.IsNullOrEmpty(cutType),
                };
                if (applies) e.Multiplier *= 1f + masterCut.FValue(0);
            }

            // XP per completed grind stage. Smaller than cut since grinding fires twice.
            float xp = GetXp(_config.Lapidary, "grind");
            var master = FindAbility(e.Player, "lapidary", _lapidary.MasterLapidaryId);
            if (master != null && master.Tier > 0) xp *= 1f + master.FValue(0);

            var skillSet = XLeveling.Instance(e.Player.Entity.Api)?.IXLevelingAPI
                                    ?.GetPlayerSkillSet(e.Player);
            skillSet?.FindSkill("lapidary", false)?.AddExperience(xp, true);
        }

        private void OnGrindStageStart(GrindStageStartEvent e)
        {
            if (e.Player == null || _lapidary == null) return;

            // Workshop Speedup — shortens grinder stages by tier %. Floored at 1 in core helper.
            var speedup = FindAbility(e.Player, "lapidary", _lapidary.WorkshopSpeedupId);
            if (speedup != null && speedup.Tier > 0)
            {
                e.Counter = (int)Math.Round(e.Counter * (1f - speedup.FValue(0)));
            }
        }

        // Permission gate for perk-restricted drops in core's panning table. Maps the perk
        // code on each drop to a Lapidary/Jeweler ability check; unknown codes default to
        // false (deny) so a typo doesn't accidentally unlock everything.
        private void OnCanPanDrop(CanPanDropEvent e)
        {
            if (e.Player == null) return;
            switch (e.PerkCode)
            {
                case "lapidary.prospector":
                    if (_lapidary == null) return;
                    var p = FindAbility(e.Player, "lapidary", _lapidary.ProspectorId);
                    if (p != null && p.Tier > 0) e.Allowed = true;
                    break;
                // Future perks key off the same dispatch.
            }
        }

        // Build CANPanningDrop[] from companion config and push into core's panning table.
        // The drops carry a `requiresPerk` field so core's existing per-drop filter shows
        // them only to players who satisfy the gate.
        private void RegisterProspectorDrops(ICoreAPI api)
        {
            var core = canjewelry.src.canjewelry.Instance;
            if (core == null || _config.AdditionalPannables == null) return;

            foreach (var kv in _config.AdditionalPannables)
            {
                var drops = new List<CANPanningDrop>();
                foreach (var dc in kv.Value.Drops)
                {
                    if (string.IsNullOrEmpty(dc.Code)) continue;
                    drops.Add(new CANPanningDrop
                    {
                        Code = new AssetLocation(dc.Code),
                        Type = dc.Type == "Block" ? EnumItemClass.Block : EnumItemClass.Item,
                        StackSize = 1,
                        Chance = NatFloat.createUniform(dc.ChanceMin, dc.ChanceMax),
                        requiresPerk = string.IsNullOrEmpty(dc.RequiresPerk) ? "lapidary.prospector" : dc.RequiresPerk,
                    });
                }
                if (drops.Count > 0)
                    core.RegisterPanDrops(api.World, kv.Key, drops.ToArray());
            }
        }

        private void OnPanMaterialTake(PanTakeEvent e)
        {
            if (e.Player == null || _lapidary == null) return;

            // Light Touch — flat -N to consumption per pan-fill. Values stored as raw ints
            // (1/2/3); FValue * 100 round-trips back to that since Ability.FValue divides by 100.
            var lt = FindAbility(e.Player, "lapidary", _lapidary.LightTouchId);
            if (lt != null && lt.Tier > 0)
            {
                int reduce = (int)Math.Round(lt.FValue(0) * 100f);
                e.Amount -= reduce;
            }
            // Floor handled in core (Math.Max(1, ev.Amount)).
        }

        private void OnPan(PanEvent e)
        {
            if (e.Player == null || _lapidary == null || e.Drop?.Item == null) return;

            // Only act when the rolled drop is a rough gem; other panning drops (ore, stone) are ignored.
            if (e.Drop.Item.Variant?["refinestate"] != "rough") return;

            // Panner — chance for +1 yield on a gem drop. With base stackSize=1 and tier %, this
            // averages out to the spec's "+10/20/30% yield" wording.
            var panner = FindAbility(e.Player, "lapidary", _lapidary.PannerId);
            if (panner != null && panner.Tier > 0
                && e.Player.Entity.World.Rand.NextDouble() < panner.FValue(0))
            {
                e.Drop.StackSize += 1;
            }

            // XP for finding a gem in the pan. Smaller than cut/grind since pans are quick.
            float xp = GetXp(_config.Lapidary, "panGem");
            var master = FindAbility(e.Player, "lapidary", _lapidary.MasterLapidaryId);
            if (master != null && master.Tier > 0) xp *= 1f + master.FValue(0);

            var skillSet = XLeveling.Instance(e.Player.Entity.Api)?.IXLevelingAPI
                                    ?.GetPlayerSkillSet(e.Player);
            skillSet?.FindSkill("lapidary", false)?.AddExperience(xp, true);
        }

        private void OnExtract(ExtractEvent e)
        {
            if (e.Player == null || _jeweler == null) return;

            // DelicateTouch — if the extraction roll fails and the gem would be lost, gives a
            // chance to recover a smaller version (ReturnedDowngraded) instead of nothing.
            var delicate = FindAbility(e.Player, "jeweler", _jeweler.DelicateTouchId);
            if (delicate != null && delicate.Tier > 0)
                e.DowngradeChance = Math.Min(1f, e.DowngradeChance + delicate.FValue(0));

            // IroncladSetting — lowers JewelryBreakChance so the piece survives extraction more often.
            var ironclad = FindAbility(e.Player, "jeweler", _jeweler.IroncladSettingId);
            if (ironclad != null && ironclad.Tier > 0)
                e.JewelryBreakChance = Math.Max(0f, e.JewelryBreakChance - ironclad.FValue(0));

            // GemExtractor — raises the effective ExtractionChance as an independent second-chance
            // roll: combined = 1 - (1-base)*(1-perk). The ExtractionChance roll happens after the
            // event, so we fold the second chance into ExtractionChance here.
            var extractor = FindAbility(e.Player, "jeweler", _jeweler.GemExtractorId);
            if (extractor != null && extractor.Tier > 0)
                e.ExtractionChance = 1f - (1f - e.ExtractionChance) * (1f - extractor.FValue(0));

            // MountSaver — only useful when something else (e.g. a future risk perk) marked
            // the jewelry for damage or destruction. Restore it to Intact.
            if (e.JewelryOutcome != JewelryOutcome.Intact)
            {
                var mount = FindAbility(e.Player, "jeweler", _jeweler.MountSaverId);
                if (mount != null && mount.Tier > 0
                    && e.Player.Entity.World.Rand.NextDouble() < mount.FValue(0))
                {
                    e.JewelryOutcome = JewelryOutcome.Intact;
                }
            }

            // Strip encrust-time skill multipliers from the gem's buff values so re-encrusting
            // doesn't compound them. Must run before socket attributes are wiped by core.
            NormalizeExtractedGem(e);

            // Synergist rollback: if the extract drops the matching-gem count below 2 on the
            // jewelry, peel synergy off the remaining sockets. Runs before XP so a malformed
            // event or unrelated XP gating doesn't skip the rollback.
            ApplySynergistOnExtract(e);

            // XP — only on a fresh gem; recycled (already-extracted) gems give nothing.
            // Mirrors the encrust gate (anti-farming).
            bool tainted = e.Gem?.Attributes?.GetBool("wasExtracted", false) ?? false;
            if (tainted) return;

            float xp = GetXp(_config.Jeweler, "extract");
            var master = FindAbility(e.Player, "jeweler", _jeweler.MasterJewelerId);
            if (master != null && master.Tier > 0) xp *= 1f + master.FValue(0);

            var skillSet = XLeveling.Instance(e.Player.Entity.Api)?.IXLevelingAPI
                                    ?.GetPlayerSkillSet(e.Player);
            skillSet?.FindSkill("jeweler", false)?.AddExperience(xp, true);
        }

        private static ItemStack? TryDowngradeRough(ItemStack? sourceRough, string? gemSize, IWorldAccessor world)
        {
            if (sourceRough?.Collectible == null) return null;

            string? smaller = gemSize switch
            {
                "normal" => "flawed",
                "flawed" => "chipped",
                _        => null,
            };
            if (smaller == null) return null;

            string? gemType = sourceRough.Collectible.Variant?["gemtype"];
            if (string.IsNullOrEmpty(gemType)) return null;

            Item? item = world.GetItem(AssetLocation.Create("canjewelry:gem-rough-" + smaller + "-" + gemType));
            return item == null ? null : new ItemStack(item);
        }

        private void OnXp(CANXpEvent e)
        {
            if (e.Player == null) return;

            string? targetSkill = e.Action switch
            {
                "encrust"   => "jeweler",
                "addSocket" => "jeweler",
                "cut"       => "lapidary",
                "grind"     => "lapidary",
                "panGem"    => "lapidary",
                _           => null,
            };
            if (targetSkill == null) return;

            // Config override → falls back to core's suggested e.Xp if action is missing.
            SkillConfig skillCfg = targetSkill == "lapidary" ? _config.Lapidary : _config.Jeweler;
            float xp = skillCfg.XpRewards.TryGetValue(e.Action, out var configured) ? configured : e.Xp;
            if (xp <= 0f) return;

            // Specialisation multiplier — master_lapidary / master_jeweler grant +25% on their
            // discipline's actions. Companion-fired XP already applies this inline, so keeping
            // the pattern uniform here means encrust/addSocket finally honor specialisation too.
            int? specId = targetSkill == "lapidary" ? _lapidary?.MasterLapidaryId : _jeweler?.MasterJewelerId;
            if (specId.HasValue)
            {
                var spec = FindAbility(e.Player, targetSkill, specId.Value);
                if (spec != null && spec.Tier > 0) xp *= 1f + spec.FValue(0);
            }

            var skillSet = XLeveling.Instance(e.Player.Entity.Api)?.IXLevelingAPI
                                    ?.GetPlayerSkillSet(e.Player);
            skillSet?.FindSkill(targetSkill, false)?.AddExperience(xp, true);
        }

        private static float GetXp(SkillConfig cfg, string action)
        {
            return cfg.XpRewards.TryGetValue(action, out var v) ? v : 0f;
        }

        private static PlayerAbility? FindAbility(IPlayer player, string skillName, int abilityId)
        {
            var skillSet = XLeveling.Instance(player.Entity.Api)?.IXLevelingAPI
                                    ?.GetPlayerSkillSet(player);
            var skill = skillSet?.FindSkill(skillName, false);
            return skill?[abilityId];
        }
    }
}
