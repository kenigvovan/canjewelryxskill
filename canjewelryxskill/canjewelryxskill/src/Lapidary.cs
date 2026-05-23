using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using XLib.XLeveling;

namespace canjewelryxskill
{
    public class Lapidary : Skill
    {
        public int PannerId           { get; private set; }
        public int LightTouchId       { get; private set; }
        public int ProspectorId       { get; private set; }
        public int ResourcefulId      { get; private set; }
        public int WorkshopSpeedupId  { get; private set; }
        public int PolishedId         { get; private set; }
        public int MasterCutId        { get; private set; }
        public int MasterLapidaryId   { get; private set; }

        public Lapidary(ICoreAPI api, SkillConfig cfg)
            : base("lapidary",
                   Lang.GetUnformatted("canjewelryxskill:skill-lapidary"),
                   Lang.GetUnformatted("canjewelryxskill:group-processing"),
                   cfg.ExpBase, cfg.ExpMult, cfg.MaxLevel)
        {
            XLeveling.Instance(api)?.RegisterSkill(this);

            PannerId           = AddAbility(BuildAbility("panner",           cfg));
            LightTouchId       = AddAbility(BuildAbility("light_touch",      cfg));
            ProspectorId       = AddAbility(BuildAbility("prospector",       cfg));
            ResourcefulId      = AddAbility(BuildAbility("resourceful",      cfg));
            WorkshopSpeedupId  = AddAbility(BuildAbility("workshop_speedup", cfg));
            PolishedId         = AddAbility(BuildAbility("polished",         cfg));
            MasterCutId        = AddAbility(BuildAbility("master_cut",       cfg));
            MasterLapidaryId   = AddAbility(BuildAbility("master_lapidary",  cfg));
            SpecialisationID   = MasterLapidaryId;
        }

        // Lang keys use kebab-case, ability codes use snake_case. The default-ability fallback
        // protects against config files saved before a perk was added.
        private static Ability BuildAbility(string code, SkillConfig cfg)
        {
            if (!cfg.Abilities.TryGetValue(code, out var ab))
            {
                ab = CanJewelryXskillConfig.MakeDefaultLapidary().Abilities[code];
            }
            string langKey = code.Replace('_', '-');
            return new Ability(
                code,
                Lang.GetUnformatted("canjewelryxskill:ability-" + langKey),
                Lang.GetUnformatted("canjewelryxskill:abilitydesc-" + langKey),
                ab.MinLevel, ab.MaxTier, ab.Values, false);
        }
    }
}
