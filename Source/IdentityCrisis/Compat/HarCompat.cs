using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    public enum HarRestrictionResult { Unavailable, Allowed, Denied }

    public class HarAddonOption
    {
        public int index;
        public string label;
        public int count;
        public bool customizable;
        public bool linked;
    }

    // All access to Humanoid Alien Races goes through reflection in this class.
    // Every call is wrapped in try/catch and logs a single warning per problem.
    public static class HarCompat
    {
        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool initialized;
        private static Type restrictionType;
        private static MethodInfo canWear;
        private static MethodInfo canEquip;
        private static MethodInfo canHaveGene;
        private static readonly HashSet<string> warned = new HashSet<string>();

        public static bool Active
        {
            get
            {
                Init();
                return restrictionType != null;
            }
        }

        private static void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            try
            {
                restrictionType = GenTypes.GetTypeInAnyAssembly("AlienRace.RaceRestrictionSettings");
                if (restrictionType == null)
                {
                    Warn("RaceRestrictionSettings not found");
                    return;
                }
                canWear = RestrictionMethod("CanWear", typeof(ThingDef), typeof(ThingDef));
                canEquip = RestrictionMethod("CanEquip", typeof(ThingDef), typeof(ThingDef));
                canHaveGene = RestrictionMethod("CanHaveGene", typeof(GeneDef), typeof(ThingDef), typeof(bool));
            }
            catch (Exception e)
            {
                Warn("init", e);
            }
        }

        public static void Warn(string key, Exception e = null)
        {
            if (warned.Add(key))
            {
                Log.Warning("[Identity Crisis] HAR compatibility problem (" + key + ")" + (e != null ? ": " + e : "."));
            }
        }

        public static object GetMember(object obj, string name)
        {
            if (obj == null)
            {
                return null;
            }
            Type type = obj.GetType();
            FieldInfo field = type.GetField(name, AnyMember);
            if (field != null)
            {
                return field.GetValue(obj);
            }
            PropertyInfo property = type.GetProperty(name, AnyMember);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(obj, null);
            }
            return null;
        }

        public static bool SetMember(object obj, string name, object value)
        {
            if (obj == null)
            {
                return false;
            }
            Type type = obj.GetType();
            PropertyInfo property = type.GetProperty(name, AnyMember);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(obj, value, null);
                return true;
            }
            FieldInfo field = type.GetField(name, AnyMember);
            if (field != null)
            {
                field.SetValue(obj, value);
                return true;
            }
            return false;
        }

        private static object GeneralSettings(ThingDef race)
        {
            return GetMember(GetMember(race, "alienRace"), "generalSettings");
        }

        private static object PartGenerator(ThingDef race)
        {
            return GetMember(GeneralSettings(race), "alienPartGenerator");
        }

        public static List<HeadTypeDef> HeadTypes(ThingDef race)
        {
            try
            {
                if (GetMember(PartGenerator(race), "HeadTypes") is IEnumerable list)
                {
                    return list.OfType<HeadTypeDef>().ToList();
                }
            }
            catch (Exception e)
            {
                Warn("head types", e);
            }
            return null;
        }

        public static List<BodyTypeDef> BodyTypes(ThingDef race)
        {
            try
            {
                if (GetMember(PartGenerator(race), "bodyTypes") is IEnumerable list)
                {
                    return list.OfType<BodyTypeDef>().ToList();
                }
            }
            catch (Exception e)
            {
                Warn("body types", e);
            }
            return null;
        }

        public static float? MaleGenderProbability(ThingDef race)
        {
            try
            {
                if (GetMember(GeneralSettings(race), "maleGenderProbability") is float value)
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                Warn("gender probability", e);
            }
            return null;
        }

        public static bool HasStyle(ThingDef race, Type styleType)
        {
            try
            {
                if (GetMember(GetMember(race, "alienRace"), "styleSettings") is IDictionary dict && dict.Contains(styleType))
                {
                    if (GetMember(dict[styleType], "hasStyle") is bool hasStyle)
                    {
                        return hasStyle;
                    }
                }
            }
            catch (Exception e)
            {
                Warn("style settings", e);
            }
            return true;
        }

        private static MethodInfo RestrictionMethod(string name, params Type[] arguments)
        {
            MethodInfo method = restrictionType.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, arguments, null);
            return method?.ReturnType == typeof(bool) ? method : null;
        }

        public static void RequireRestrictionChecks()
        {
            Init();
            if (restrictionType == null || canWear == null || canEquip == null || (ModsConfig.BiotechActive && canHaveGene == null))
                throw new InvalidOperationException("HAR restriction API unavailable or incompatible.");
        }

        public static HarRestrictionResult CheckCanWear(ThingDef apparel, ThingDef race)
        {
            Init();
            return Check(canWear, "CanWear", apparel, race);
        }

        public static HarRestrictionResult CheckCanEquip(ThingDef weapon, ThingDef race)
        {
            Init();
            return Check(canEquip, "CanEquip", weapon, race);
        }

        public static HarRestrictionResult CheckCanHaveGene(GeneDef gene, ThingDef race, bool xenogene = false)
        {
            Init();
            return Check(canHaveGene, "CanHaveGene", gene, race, xenogene);
        }

        // Legacy callers remain source-compatible, but an unavailable check no longer grants permission.
        public static bool CanWear(ThingDef apparel, ThingDef race) => CheckCanWear(apparel, race) == HarRestrictionResult.Allowed;
        public static bool CanEquip(ThingDef weapon, ThingDef race) => CheckCanEquip(weapon, race) == HarRestrictionResult.Allowed;
        public static bool CanHaveGene(GeneDef gene, ThingDef race) => CheckCanHaveGene(gene, race) == HarRestrictionResult.Allowed;

        private static HarRestrictionResult Check(MethodInfo method, string key, params object[] args)
        {
            if (method == null) return HarRestrictionResult.Unavailable;
            try
            {
                object value = method.Invoke(null, args);
                if (!(value is bool allowed)) return HarRestrictionResult.Unavailable;
                return allowed ? HarRestrictionResult.Allowed : HarRestrictionResult.Denied;
            }
            catch (Exception e)
            {
                Warn(key, e);
                return HarRestrictionResult.Unavailable;
            }
        }

        // Strict planning path: unlike UI fallback helpers, missing HAR metadata is an error.
        // No explicit writes to the source pawn, health or genes occur while resolving.
        // Ordinary color getters may populate engine/HAR caches.
        public static void ResolveAppearance(Pawn pawn, AssimilationConfig config)
        {
            try
            {
                RequireRestrictionChecks();
                object alien = GetMember(config.race, "alienRace");
                if (alien != null && config.race.race.hasGenders)
                {
                    object probability = GetMember(GeneralSettings(config.race), "maleGenderProbability");
                    if (!(probability is float male) || float.IsNaN(male))
                        throw new InvalidOperationException("HAR gender metadata unavailable.");
                    if (male <= 0f) config.gender = Gender.Female;
                    else if (male >= 1f) config.gender = Gender.Male;
                }
                List<BodyTypeDef> bodies = PlanningBodies(config.race, config.gender);
                List<HeadTypeDef> heads = PlanningHeads(config.race, config.gender);
                config.bodyType = Choose(bodies, config.bodyType, pawn.story.bodyType);
                config.headType = Choose(heads, config.headType, pawn.story.headType);
                config.hair = ResolveStyle(pawn, config, config.hair, pawn.story.hairDef, HairDefOf.Bald);
                config.beard = ResolveStyle(pawn, config, config.beard, pawn.style?.beardDef, BeardDefOf.NoBeard);
                // Automatic colors are explicitly pinned to the existing visible colors. No
                // race-default color generator is invoked later during application.
                if (!config.overrideHairColor) config.hairColor = pawn.story.HairColor;
                if (!config.overrideSkinColor) config.skinColor = pawn.story.SkinColor;
                config.overrideHairColor = config.overrideSkinColor = true;
                ValidateColor(config.hairColor);
                ValidateColor(config.skinColor);
                List<HarAddonOption> addons = ReadAddons(config.race);
                List<int> requested = config.addonVariants ?? new List<int>();
                if (requested.Skip(addons.Count).Any(x => x >= 0)) throw new InvalidOperationException("Unknown addon index.");
                config.addonVariants = new List<int>();
                int previous = 0;
                foreach (HarAddonOption addon in addons)
                {
                    int selected = addon.index < requested.Count ? requested[addon.index] : -1;
                    int count = Math.Max(1, addon.count);
                    int value = selected < 0 ? (addon.linked ? previous % count : Rand.Range(0, count)) : selected;
                    if (value >= count || (addon.linked && value != previous % count))
                        throw new InvalidOperationException("Invalid or conflicting linked addon variant.");
                    config.addonVariants.Add(value);
                    previous = value;
                }
                ValidateResolvedAppearance(pawn, config);
            }
            catch (Exception e)
            {
                Warn("appearance preflight", e);
                throw;
            }
        }

        private static List<BodyTypeDef> PlanningBodies(ThingDef race, Gender gender)
        {
            object alien = GetMember(race, "alienRace");
            List<BodyTypeDef> list;
            if (alien == null) list = RaceUtility.AllowedBodyTypes(race, gender);
            else
            {
                if (!(GetMember(PartGenerator(race), "bodyTypes") is IEnumerable values))
                    throw new InvalidOperationException("HAR body metadata unavailable.");
                list = values.OfType<BodyTypeDef>().ToList();
            }
            return list.Where(b => b != null && b != BodyTypeDefOf.Child && b != BodyTypeDefOf.Baby
                && !(gender == Gender.Male && b == BodyTypeDefOf.Female)
                && !(gender == Gender.Female && b == BodyTypeDefOf.Male)).Distinct().ToList();
        }

        private static List<HeadTypeDef> PlanningHeads(ThingDef race, Gender gender)
        {
            object alien = GetMember(race, "alienRace");
            List<HeadTypeDef> list;
            if (alien == null) list = DefDatabase<HeadTypeDef>.AllDefsListForReading.Where(h => h.randomChosen).ToList();
            else
            {
                if (!(GetMember(PartGenerator(race), "HeadTypes") is IEnumerable values))
                    throw new InvalidOperationException("HAR head metadata unavailable.");
                list = values.OfType<HeadTypeDef>().ToList();
            }
            return list.Where(h => h != null && (h.gender == Gender.None || h.gender == gender)
                && h.requiredGenes.NullOrEmpty()).Distinct().ToList();
        }

        private static T Choose<T>(List<T> allowed, T requested, T existing) where T : class
        {
            if (allowed.Count == 0) throw new InvalidOperationException("No supported appearance choices.");
            if (requested != null && !allowed.Contains(requested))
                throw new InvalidOperationException("Requested appearance is not allowed by target race/gender.");
            return requested ?? (allowed.Contains(existing) ? existing : allowed.RandomElement());
        }

        private static T ResolveStyle<T>(Pawn pawn, AssimilationConfig config, T requested, T existing, T empty) where T : StyleItemDef
        {
            object settings = StyleSettingsFor(config.race, typeof(T));
            bool hasStyle = settings == null || ReadBool(settings, "hasStyle");
            if (typeof(T) == typeof(BeardDef) && config.gender == Gender.Female) hasStyle = false;
            if (!hasStyle)
            {
                if (requested != null && requested != empty)
                    throw new InvalidOperationException("Target race/gender does not support the requested style.");
                return empty;
            }
            List<T> allowed = DefDatabase<T>.AllDefsListForReading.Where(d => StyleAllowed(pawn, config.gender, d, settings)).ToList();
            return Choose(allowed, requested, existing);
        }

        private static object StyleSettingsFor(ThingDef race, Type type)
        {
            if (GetMember(race, "alienRace") == null) return null;
            if (!(GetMember(GetMember(race, "alienRace"), "styleSettings") is IDictionary settings) || !settings.Contains(type))
                throw new InvalidOperationException("HAR style metadata unavailable.");
            return settings[type] ?? throw new InvalidOperationException("HAR style settings missing.");
        }

        private static bool ReadBool(object value, string name)
        {
            if (!(GetMember(value, name) is bool result)) throw new InvalidOperationException("HAR member unavailable: " + name);
            return result;
        }

        private static List<string> Tags(object settings, string name)
        {
            object tags = GetMember(settings, name);
            if (tags == null) return new List<string>();
            if (!(tags is IEnumerable enumerable)) throw new InvalidOperationException("Invalid HAR style tags.");
            return enumerable.Cast<object>().Select(t => t.ToString()).ToList();
        }

        private static bool StyleAllowed(Pawn pawn, Gender gender, StyleItemDef style, object settings)
        {
            if (style == null) return false;
            if (settings != null)
            {
                List<string> allowed = Tags(settings, "styleTags");
                List<string> banned = Tags(settings, "bannedTags");
                if (allowed.Count > 0 && !allowed.Any(t => style.styleTags?.Contains(t) == true)) return false;
                if (banned.Any(t => style.styleTags?.Contains(t) == true)) return false;
                if (!ReadBool(settings, "genderRespected")) return true;
            }
            StyleGender styleGender = pawn.Ideo?.style.GetGender(style) ?? style.styleGender;
            return gender == Gender.None || (styleGender != StyleGender.Male || gender == Gender.Male)
                && (styleGender != StyleGender.Female || gender == Gender.Female);
        }

        private static void ValidateColor(Color color)
        {
            if (float.IsNaN(color.r) || float.IsNaN(color.g) || float.IsNaN(color.b) || float.IsNaN(color.a)
                || color.r < 0 || color.r > 1 || color.g < 0 || color.g > 1 || color.b < 0 || color.b > 1 || color.a < 0 || color.a > 1)
                throw new InvalidOperationException("Invalid appearance color.");
        }

        public static void ValidateResolvedAppearance(Pawn pawn, AssimilationConfig config)
        {
            try
            {
                RequireRestrictionChecks();
                if (!PlanningBodies(config.race, config.gender).Contains(config.bodyType)
                    || !PlanningHeads(config.race, config.gender).Contains(config.headType))
                    throw new InvalidOperationException("Resolved body/head is no longer allowed.");
                if (ResolveStyle(pawn, config, config.hair, config.hair, HairDefOf.Bald) != config.hair
                    || ResolveStyle(pawn, config, config.beard, config.beard, BeardDefOf.NoBeard) != config.beard)
                    throw new InvalidOperationException("Resolved style is no longer allowed.");
                List<HarAddonOption> addons = ReadAddons(config.race);
                if (config.addonVariants == null || config.addonVariants.Count != addons.Count)
                    throw new InvalidOperationException("Addon metadata changed.");
                int previous = 0;
                foreach (HarAddonOption addon in addons)
                {
                    int value = config.addonVariants[addon.index];
                    int count = Math.Max(1, addon.count);
                    if (value < 0 || value >= count || (addon.linked && value != previous % count))
                        throw new InvalidOperationException("Resolved addon is no longer allowed.");
                    previous = value;
                }
                ThingComp comp = AlienComp(pawn);
                if ((GetMember(config.race, "alienRace") != null || addons.Count > 0) && comp == null)
                    throw new InvalidOperationException("Target race requires unsupported AlienComp creation.");
                if (comp != null)
                {
                    foreach (string field in new[] { "colorChannels", "addonVariants", "addonColors", "nodeProps", "headVariant", "bodyVariant" })
                        if (comp.GetType().GetField(field, AnyMember) == null)
                            throw new InvalidOperationException("HAR appearance member unavailable: " + field);
                    if (!(GetMember(comp, "addonColors") is IList))
                        throw new InvalidOperationException("HAR addon colors unavailable.");
                    if (comp.GetType().GetMethod("OverwriteColorChannel", AnyMember, null,
                        new[] { typeof(string), typeof(Color?), typeof(Color?) }, null) == null)
                        throw new InvalidOperationException("HAR color API unavailable.");
                }
            }
            catch (Exception e) { Warn("resolved appearance preflight", e); throw; }
        }

        public static void ApplyResolvedAppearance(Pawn pawn, AssimilationConfig config)
        {
            ThingComp comp = AlienComp(pawn);
            if (comp == null) return;
            try
            {
                SetRequired(comp, "colorChannels", null);
                SetRequired(comp, "addonVariants", new List<int>(config.addonVariants));
                if (!(GetMember(comp, "addonColors") is IList colors)) throw new InvalidOperationException("HAR addon colors unavailable.");
                colors.Clear();
                SetRequired(comp, "nodeProps", null);
                SetRequired(comp, "headVariant", -1);
                SetRequired(comp, "bodyVariant", -1);
                MethodInfo setter = comp.GetType().GetMethod("OverwriteColorChannel", AnyMember, null,
                    new[] { typeof(string), typeof(Color?), typeof(Color?) }, null);
                if (setter == null) throw new InvalidOperationException("HAR color API unavailable.");
                setter.Invoke(comp, new object[] { "hair", (Color?)config.hairColor, (Color?)config.hairColor });
                setter.Invoke(comp, new object[] { "skin", (Color?)config.skinColor, (Color?)config.skinColor });
            }
            catch (Exception e) { Warn("resolved appearance apply", e); throw; }
        }

        private static void SetRequired(object target, string name, object value)
        {
            if (!SetMember(target, name, value)) throw new InvalidOperationException("HAR write unavailable: " + name);
        }

        public static ThingComp AlienComp(Pawn pawn)
        {
            return pawn?.AllComps?.FirstOrDefault(c => c.GetType().Name == "AlienComp");
        }

        // Clears cached colors, addon variants and graphics nodes so HAR regenerates them for the new race.
        public static void ResetAlienComp(Pawn pawn)
        {
            ThingComp comp = AlienComp(pawn);
            if (comp == null)
            {
                return;
            }
            try
            {
                SetMember(comp, "colorChannels", null);
                SetMember(comp, "addonVariants", null);
                if (GetMember(comp, "addonColors") is IList addonColors)
                {
                    addonColors.Clear();
                }
                SetMember(comp, "nodeProps", null);
                SetMember(comp, "headVariant", -1);
                SetMember(comp, "bodyVariant", -1);
                SetMember(comp, "headMaskVariant", -1);
                SetMember(comp, "bodyMaskVariant", -1);
            }
            catch (Exception e)
            {
                Warn("AlienComp reset", e);
            }
        }

        // HAR's addon order includes race-specific and universal addons. Keep all entries
        // so saved indices stay aligned, but only expose user-customizable ones.
        public static List<HarAddonOption> Addons(ThingDef race)
        {
            try { return ReadAddons(race); }
            catch (Exception e) { Warn("addon options", e); return new List<HarAddonOption>(); }
        }

        private static List<HarAddonOption> ReadAddons(ThingDef race)
        {
            var result = new List<HarAddonOption>();
            object generator = PartGenerator(race);
            var items = new List<object>();
            if (GetMember(race, "alienRace") != null && generator == null)
                throw new InvalidOperationException("HAR part generator unavailable.");
            if (generator != null)
            {
                if (!(GetMember(generator, "bodyAddons") is IEnumerable specific))
                    throw new InvalidOperationException("HAR addon metadata unavailable.");
                foreach (object item in specific) items.Add(item);
            }
            Type utilities = GenTypes.GetTypeInAnyAssembly("AlienRace.Utilities");
            PropertyInfo universal = utilities?.GetProperty("UniversalBodyAddons", AnyMember);
            if (universal == null || !(universal.GetValue(null, null) is IEnumerable extras))
                throw new InvalidOperationException("HAR universal addon metadata unavailable.");
            foreach (object item in extras) items.Add(item);
            foreach (object item in items)
            {
                if (!(GetMember(item, "VariantCountMax") is int count) || count < 0)
                    throw new InvalidOperationException("HAR addon variant count unavailable.");
                result.Add(new HarAddonOption { index = result.Count,
                    label = GetMember(item, "Name")?.ToString() ?? "Addon " + (result.Count + 1),
                    count = count, customizable = ReadBool(item, "userCustomizable"),
                    linked = ReadBool(item, "linkVariantIndexWithPrevious") });
            }
            return result;
        }

        public static void TryApplyAddonVariants(Pawn pawn, List<int> choices)
        {
            if (choices == null || !choices.Any(x => x >= 0)) return;
            ThingComp comp = AlienComp(pawn);
            if (comp == null) return;
            try
            {
                List<HarAddonOption> addons = Addons(pawn.def);
                var variants = new List<int>();
                int previous = 0;
                foreach (HarAddonOption addon in addons)
                {
                    int selected = addon.index < choices.Count ? choices[addon.index] : -1;
                    int max = Math.Max(1, addon.count);
                    int value = selected >= 0 ? selected % max : (addon.linked ? previous % max : Rand.Range(0, max));
                    variants.Add(value);
                    previous = value;
                }
                if (variants.Count == 0) return;
                SetMember(comp, "addonVariants", variants);
                SetMember(comp, "nodeProps", null);
            }
            catch (Exception e) { Warn("addon variants", e); }
        }

        public static void TrySetSkinColor(Pawn pawn, Color color)
        {
            TrySetColorChannel(pawn, "skin", color);
        }

        public static void TrySetHairColor(Pawn pawn, Color color)
        {
            TrySetColorChannel(pawn, "hair", color);
        }

        private static void TrySetColorChannel(Pawn pawn, string channel, Color color)
        {
            ThingComp comp = AlienComp(pawn);
            if (comp == null)
            {
                return;
            }
            try
            {
                MethodInfo method = comp.GetType().GetMethod("OverwriteColorChannel", AnyMember);
                if (method != null && method.GetParameters().Length == 3)
                {
                    method.Invoke(comp, new object[] { channel, color, null });
                }
            }
            catch (Exception e)
            {
                Warn(channel + " color", e);
            }
        }
    }
}
