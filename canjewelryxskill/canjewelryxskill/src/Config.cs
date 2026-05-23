using System.Collections.Generic;
using Newtonsoft.Json;

namespace canjewelryxskill
{
    public class AbilityConfig
    {
        public int MinLevel { get; set; }
        public int MaxTier { get; set; }
        public int[] Values { get; set; }
    }

    public class SkillConfig
    {
        public int ExpBase { get; set; }
        public float ExpMult { get; set; }
        public int MaxLevel { get; set; }
        public Dictionary<string, AbilityConfig> Abilities { get; set; } = new();
        public Dictionary<string, float> XpRewards { get; set; } = new();
    }

    // Drops the companion mod registers into core's panning table at startup. Each entry
    // becomes a CANPanningDrop with `requiresPerk` set to a perk code (default the Prospector
    // perk), so the entry is hidden from players who haven't unlocked it. Admins customise
    // this in canjewelryxskill.json — entries propagate to core in-memory only, the core
    // canjewelry.json stays untouched.
    public class ProspectorDropEntry
    {
        public string Code { get; set; }
        public string Type { get; set; } = "Item";   // "Item" or "Block"
        public float ChanceMin { get; set; } = 0.10f;
        public float ChanceMax { get; set; } = 0.25f;
        public string RequiresPerk { get; set; } = "lapidary.prospector";
    }

    public class ProspectorMaterialEntry
    {
        public List<ProspectorDropEntry> Drops { get; set; } = new();
    }

    public class CanJewelryXskillConfig
    {
        [JsonIgnore] public SkillConfig Lapidary { get; set; } = MakeDefaultLapidary();
        [JsonIgnore] public SkillConfig Jeweler  { get; set; } = MakeDefaultJeweler();
        public Dictionary<string, ProspectorMaterialEntry> AdditionalPannables { get; set; } = MakeDefaultAdditionalPannables();

        // Default pannable extensions unlocked by the Prospector perk. Minimal sane set —
        // server admins customise via JSON. Wildcard codes (with `*`) work via core's
        // WildcardUtil.Match against the panned material's block code.
        public static Dictionary<string, ProspectorMaterialEntry> MakeDefaultAdditionalPannables() => new()
        {
            ["game:rock-bauxite"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-amethyst", ChanceMin = 0.10f, ChanceMax = 0.20f },
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-citrine",  ChanceMin = 0.05f, ChanceMax = 0.12f },
                },
            },
            ["game:rock-andesite"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-quartz",   ChanceMin = 0.15f, ChanceMax = 0.25f },
                },
            },
            ["game:rock-limestone"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-fluorite",  ChanceMin = 0.08f, ChanceMax = 0.15f },
                },
            },
            ["game:rock-marble"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-corundum",  ChanceMin = 0.06f, ChanceMax = 0.12f },
                },
            },
            ["game:rock-serpentinite"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-emerald",   ChanceMin = 0.04f, ChanceMax = 0.10f },
                },
            },
            ["game:rock-rhyolite"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-amethyst",  ChanceMin = 0.08f, ChanceMax = 0.15f },
                },
            },
            ["game:rock-basalt"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-olivine",   ChanceMin = 0.10f, ChanceMax = 0.18f },
                },
            },
            ["game:rock-granite"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-fluorite",  ChanceMin = 0.04f, ChanceMax = 0.10f },
                },
            },
            ["game:rock-chalk"] = new()
            {
                Drops = new()
                {
                    new ProspectorDropEntry { Code = "canjewelry:gem-rough-chipped-lapislazuli", ChanceMin = 0.04f, ChanceMax = 0.08f },
                },
            },
        };

        public static SkillConfig MakeDefaultLapidary() => new()
        {
            ExpBase = 50,
            ExpMult = 1.1f,
            MaxLevel = 20,
            Abilities = new()
            {
                ["panner"]            = new() { MinLevel = 1,  MaxTier = 3, Values = new[] { 10, 20, 30 } },
                ["light_touch"]       = new() { MinLevel = 1,  MaxTier = 3, Values = new[] { 1, 2, 3 } },
                ["prospector"]        = new() { MinLevel = 5,  MaxTier = 1, Values = new[] { 1 } },
                ["resourceful"]       = new() { MinLevel = 1,  MaxTier = 3, Values = new[] { 5, 10, 15 } },
                ["workshop_speedup"]  = new() { MinLevel = 5,  MaxTier = 3, Values = new[] { 20, 35, 50 } },
                ["polished"]          = new() { MinLevel = 5,  MaxTier = 3, Values = new[] { 1, 2, 4 } },
                ["master_cut"]        = new() { MinLevel = 10, MaxTier = 3, Values = new[] { 3, 5, 8 } },
                ["master_lapidary"]   = new() { MinLevel = 5,  MaxTier = 1, Values = new[] { 25 } },
            },
            XpRewards = new()
            {
                ["cut"]    = 5f,
                ["grind"]  = 3f,
                ["panGem"] = 2f,
            },
        };

        public static SkillConfig MakeDefaultJeweler() => new()
        {
            ExpBase = 50,
            ExpMult = 1.1f,
            MaxLevel = 25,
            Abilities = new()
            {
                ["steady_hand"]     = new() { MinLevel = 1,  MaxTier = 3, Values = new[] { 3, 6, 9 } },
                ["inscriber"]       = new() { MinLevel = 1,  MaxTier = 1, Values = new[] { 1 } },
                ["wire_worker"]     = new() { MinLevel = 5,  MaxTier = 3, Values = new[] { 10, 20, 30 } },
                ["synergist"]       = new() { MinLevel = 10, MaxTier = 3, Values = new[] { 5, 10, 20 } },
                ["gem_extractor"]   = new() { MinLevel = 10, MaxTier = 3, Values = new[] { 30, 55, 80 } },
                ["mount_saver"]     = new() { MinLevel = 20, MaxTier = 1, Values = new[] { 25 } },
                ["delicate_touch"]   = new() { MinLevel = 5,  MaxTier = 3, Values = new[] { 15, 30, 50 } },
                ["ironclad_setting"] = new() { MinLevel = 5,  MaxTier = 3, Values = new[] { 3, 6, 10 } },
                ["extract_license"]  = new() { MinLevel = 1,  MaxTier = 1, Values = new[] { 1 } },
                ["master_jeweler"]   = new() { MinLevel = 5,  MaxTier = 1, Values = new[] { 40 } },
            },
            XpRewards = new()
            {
                ["encrust"]   = 8f,
                ["addSocket"] = 2f,
                ["extract"]   = 4f,
            },
        };

        // Fills in any ability or xpReward that the player's saved JSON is missing — keeps the
        // config forwards-compatible after we add new perks without losing user tweaks.
        public static void FillMissing(CanJewelryXskillConfig cfg)
        {
            FillSkillDefaults(cfg.Lapidary, MakeDefaultLapidary());
            FillSkillDefaults(cfg.Jeweler,  MakeDefaultJeweler());
        }

        private static void FillSkillDefaults(SkillConfig target, SkillConfig defaults)
        {
            foreach (var kv in defaults.Abilities)
            {
                if (!target.Abilities.ContainsKey(kv.Key)) target.Abilities[kv.Key] = kv.Value;
            }
            foreach (var kv in defaults.XpRewards)
            {
                if (!target.XpRewards.ContainsKey(kv.Key)) target.XpRewards[kv.Key] = kv.Value;
            }
        }
    }
}
