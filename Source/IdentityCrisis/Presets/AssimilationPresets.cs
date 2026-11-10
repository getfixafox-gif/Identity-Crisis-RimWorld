using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Save-local templates contain configuration only: never a Pawn or a pod reference.
    public sealed class AssimilationPreset : IExposable
    {
        public string id;
        public string name;
        private AssimilationConfig template;
        // Keep names independently so removing a mod cannot silently turn an explicit choice into Auto.
        private List<string> requiredDefs;

        public AssimilationPreset() { }
        public AssimilationPreset(string name, AssimilationConfig source)
        {
            id = Guid.NewGuid().ToString("N");
            this.name = name;
            template = source.Copy();
            template.policyCaptured = false;
            requiredDefs = DefNames(template);
        }

        private static List<string> DefNames(AssimilationConfig c)
        {
            return new List<string> { c.race?.defName ?? "", c.bodyType?.defName ?? "",
                c.headType?.defName ?? "", c.hair?.defName ?? "", c.beard?.defName ?? "" };
        }

        public bool TryGetConfig(out AssimilationConfig result, out string error)
        {
            result = null;
            error = "IC_V03_InvalidPreset".Translate().Resolve();
            if (template?.race?.race == null || !template.race.race.Humanlike ||
                requiredDefs == null || requiredDefs.Count != 5 ||
                !requiredDefs.SequenceEqual(DefNames(template))) return false;
            if (!ValidConfig(template)) return false;
            // Defensive copy: neither the editor nor BuildPlan can mutate the saved template.
            result = template.Copy();
            result.policyCaptured = false;
            error = null;
            return true;
        }

        internal static bool ValidConfig(AssimilationConfig c)
        {
            if (c?.race?.race == null || !c.race.race.Humanlike ||
                !Finite(c.ageYears) || c.ageYears < RaceUtility.AdultMinAge(c.race) ||
                c.ageYears > RaceUtility.MaxAge(c.race) || !ValidColor(c.hairColor) || !ValidColor(c.skinColor)) return false;
            if (c.gender != Gender.None && c.gender != Gender.Male && c.gender != Gender.Female) return false;
            if (RaceUtility.GenderLocked(c.race, out Gender locked) && c.gender != locked) return false;
            if (c.race.race.hasGenders && c.gender == Gender.None) return false;
            if (c.bodyType != null && !RaceUtility.AllowedBodyTypes(c.race, c.gender).Contains(c.bodyType)) return false;
            if (c.headType != null && !RaceUtility.AllowedHeadTypes(c.race, c.gender).Contains(c.headType)) return false;
            if (c.hair != null && c.hair != HairDefOf.Bald && !RaceUtility.HasHair(c.race)) return false;
            if (c.beard != null && c.beard != BeardDefOf.NoBeard && !RaceUtility.HasBeard(c.race, c.gender)) return false;
            List<HarAddonOption> addons = HarCompat.Addons(c.race);
            if (c.addonVariants != null)
            {
                if (c.addonVariants.Count > addons.Count) return false;
                for (int i = 0; i < c.addonVariants.Count; i++)
                {
                    int v = c.addonVariants[i];
                    if (v < -1 || (v >= 0 && (v >= System.Math.Max(1, addons[i].count)))) return false;
                    if (addons[i].linked && i > 0 && v != c.addonVariants[i - 1] % System.Math.Max(1, addons[i].count)) return false;
                }
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidColor(Color c) => Finite(c.r) && Finite(c.g) && Finite(c.b) && Finite(c.a) &&
            c.r >= 0f && c.r <= 1f && c.g >= 0f && c.g <= 1f && c.b >= 0f && c.b <= 1f && c.a >= 0f && c.a <= 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Deep.Look(ref template, "template");
            Scribe_Collections.Look(ref requiredDefs, "requiredDefs", LookMode.Value);
        }
    }

    public sealed class WorldComponent_AssimilationPresets : WorldComponent
    {
        private const int Limit = 100;
        private List<AssimilationPreset> presets = new List<AssimilationPreset>();
        public WorldComponent_AssimilationPresets(World world) : base(world) { }
        public IEnumerable<AssimilationPreset> Presets => presets.AsReadOnly();
        public static WorldComponent_AssimilationPresets Current => Find.World?.GetComponent<WorldComponent_AssimilationPresets>();

        public bool Save(string name, AssimilationConfig source, out string error)
        {
            if (!ValidateName(name, null, out name, out error)) return false;
            if (presets.Count >= Limit) { error = "IC_V03_PresetLimit".Translate(Limit).Resolve(); return false; }
            if (!AssimilationPreset.ValidConfig(source)) { error = "IC_V03_InvalidPreset".Translate().Resolve(); return false; }
            presets.Add(new AssimilationPreset(name, source));
            return true;
        }

        public bool Rename(string id, string name, out string error)
        {
            AssimilationPreset preset = presets.Find(p => p.id == id);
            error = "IC_V03_InvalidPreset".Translate().Resolve();
            if (preset == null || !ValidateName(name, id, out name, out error)) return false;
            preset.name = name;
            return true;
        }

        public void Delete(string id) => presets.RemoveAll(p => p.id == id);

        private bool ValidateName(string input, string exceptId, out string name, out string error)
        {
            name = input?.Trim();
            error = "IC_V03_InvalidName".Translate().Resolve();
            if (string.IsNullOrEmpty(name) || name.Length > 64 || name.Any(char.IsControl) || name.Contains("<") || name.Contains(">")) return false;
            string candidate = name;
            if (presets.Any(p => p.id != exceptId && string.Equals(p.name, candidate, StringComparison.OrdinalIgnoreCase)))
            { error = "IC_V03_DuplicateName".Translate().Resolve(); return false; }
            error = null;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref presets, "IC_AssimilationPresets", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (presets == null) presets = new List<AssimilationPreset>();
                presets.RemoveAll(p => p == null || string.IsNullOrEmpty(p.id) || string.IsNullOrEmpty(p.name));
                // Invalid/missing race definitions remain visible, but application is safely refused.
                presets = presets.GroupBy(p => p.id).Select(g => g.First()).Take(Limit).ToList();
            }
        }
    }
}
