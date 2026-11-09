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
                foreach (MethodInfo method in restrictionType.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    int count = method.GetParameters().Length;
                    if (method.Name == "CanWear" && count == 2)
                    {
                        canWear = method;
                    }
                    else if (method.Name == "CanEquip" && count == 2)
                    {
                        canEquip = method;
                    }
                    else if (method.Name == "CanHaveGene" && count == 3)
                    {
                        canHaveGene = method;
                    }
                }
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

        public static bool CanWear(ThingDef apparel, ThingDef race)
        {
            return Call(canWear, "CanWear", apparel, race);
        }

        public static bool CanEquip(ThingDef weapon, ThingDef race)
        {
            return Call(canEquip, "CanEquip", weapon, race);
        }

        public static bool CanHaveGene(GeneDef gene, ThingDef race)
        {
            return Call(canHaveGene, "CanHaveGene", gene, race, false);
        }

        private static bool Call(MethodInfo method, string key, params object[] args)
        {
            Init();
            if (method == null)
            {
                return true;
            }
            try
            {
                return (bool)method.Invoke(null, args);
            }
            catch (Exception e)
            {
                Warn(key, e);
                return true;
            }
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
                SetMember(comp, "ColorChannels", null);
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

        public static void TrySetHairColor(Pawn pawn, Color color)
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
                    method.Invoke(comp, new object[] { "hair", color, null });
                }
            }
            catch (Exception e)
            {
                Warn("hair color", e);
            }
        }
    }
}
