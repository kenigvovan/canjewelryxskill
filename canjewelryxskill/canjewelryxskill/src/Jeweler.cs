using Vintagestory.API.Common;
using XLib.XLeveling;

namespace canjewelryxskill
{
    public class Jeweler : Skill
    {
        public int SteadyHandId     { get; private set; }
        public int InscriberId      { get; private set; }
        public int WireWorkerId     { get; private set; }
        public int SynergistId      { get; private set; }
        public int GemExtractorId   { get; private set; }
        public int MountSaverId     { get; private set; }
        public int DelicateTouchId    { get; private set; }
        public int IroncladSettingId  { get; private set; }
        public int ExtractLicenseId   { get; private set; }
        public int MasterJewelerId    { get; private set; }

        public Jeweler(ICoreAPI api, SkillConfig cfg)
            // Lang keys, not resolved strings — see the note in Lapidary's constructor.
            : base("jeweler",
                   "canjewelryxskill:skill-jeweler",
                   "canjewelryxskill:group-crafting",
                   cfg.ExpBase, cfg.ExpMult, cfg.MaxLevel)
        {
            XLeveling.Instance(api)?.RegisterSkill(this);
            api.Logger.Event("[canjewelryxskill] RegisterSkill(jeweler) side={0} id={1}", api.Side, Id);

            SteadyHandId     = AddAbility(BuildAbility("steady_hand",     cfg));
            InscriberId      = AddAbility(BuildAbility("inscriber",       cfg));
            WireWorkerId     = AddAbility(BuildAbility("wire_worker",     cfg));
            SynergistId      = AddAbility(BuildAbility("synergist",       cfg));
            GemExtractorId   = AddAbility(BuildAbility("gem_extractor",   cfg));
            MountSaverId     = AddAbility(BuildAbility("mount_saver",     cfg));
            DelicateTouchId   = AddAbility(BuildAbility("delicate_touch",   cfg));
            IroncladSettingId = AddAbility(BuildAbility("ironclad_setting", cfg));
            ExtractLicenseId  = AddAbility(BuildAbility("extract_license",  cfg));
            MasterJewelerId   = AddAbility(BuildAbility("master_jeweler",   cfg));
            SpecialisationID = MasterJewelerId;

            // GemExtractor and MountSaver require ExtractLicense tier 1 — you can't improve
            // gem/jewelry recovery from extraction before you can extract at all.
            var extractLicense = this[ExtractLicenseId];
            this[GemExtractorId  ].AddRequirement(new AbilityRequirement(extractLicense, 1, 1, false));
            this[MountSaverId    ].AddRequirement(new AbilityRequirement(extractLicense, 1, 1, false));
            this[DelicateTouchId ].AddRequirement(new AbilityRequirement(extractLicense, 1, 1, false));
            this[IroncladSettingId].AddRequirement(new AbilityRequirement(extractLicense, 1, 1, false));
        }

        private static Ability BuildAbility(string code, SkillConfig cfg)
        {
            if (!cfg.Abilities.TryGetValue(code, out var ab))
            {
                ab = CanJewelryXskillConfig.MakeDefaultJeweler().Abilities[code];
            }
            string langKey = code.Replace('_', '-');
            return new Ability(
                code,
                "canjewelryxskill:ability-" + langKey,
                "canjewelryxskill:abilitydesc-" + langKey,
                ab.MinLevel, ab.MaxTier, ab.Values, false);
        }
    }
}
