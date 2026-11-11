using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Setup window: current pawn on the left, new body settings in the middle,
    // psychological forecast on the right. Works on a copy of the config until Accept.
    public class Dialog_AssimilationSetup : Window
    {
        private const float RowHeight = 30f;
        private const float LabelWidth = 130f;

        private readonly Building_AssimilationPod pod;
        private readonly Pawn pawn;
        private readonly AssimilationConfig config;
        private List<ForecastLine> forecast;
        private Vector2 forecastScroll;
        private Vector2 settingsScroll;
        private readonly AssimilationPreview preview = new AssimilationPreview();

        public override Vector2 InitialSize => new Vector2(1000f, 640f);

        public Dialog_AssimilationSetup(Building_AssimilationPod pod, Pawn pawn, AssimilationConfig existing)
        {
            this.pod = pod;
            this.pawn = pawn;
            config = existing != null ? existing.Copy() : RaceUtility.DefaultConfig(pawn, RaceUtility.DefaultTargetRace(pawn));
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            RefreshForecast();
        }

        private static string T(string key)
        {
            return key.Translate().Resolve();
        }

        private static string DefLabel(Def def)
        {
            if (def == null)
            {
                return T("IC_Auto");
            }
            return def.label.NullOrEmpty() ? def.defName : def.LabelCap.ToString();
        }

        private void RefreshForecast()
        {
            forecast = ConversionForecast.Build(pawn, config);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "IC_SetupTitle".Translate(pawn.LabelShortCap));
            Text.Font = GameFont.Small;

            float top = inRect.y + 44f;
            float height = inRect.yMax - 50f - top;
            Rect left = new Rect(inRect.x, top, 230f, height);
            Rect center = new Rect(left.xMax + 12f, top, 370f, height);
            Rect right = new Rect(center.xMax + 12f, top, inRect.xMax - center.xMax - 12f, height);

            DrawCurrent(left);
            DrawSettings(center);
            DrawForecast(right);
            DrawButtons(new Rect(inRect.x, inRect.yMax - 40f, inRect.width, 40f));
        }

        private void DrawCurrent(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), T("IC_SetupCurrent"));

            Vector2 portraitSize = new Vector2(110f, 130f);
            Rect portraitRect = new Rect(inner.x + (inner.width - portraitSize.x) / 2f, inner.y + 26f, portraitSize.x, portraitSize.y);
            RenderTexture portrait = PortraitsCache.Get(pawn, portraitSize, Rot4.South);
            if (portrait != null) GUI.DrawTexture(portraitRect, portrait);

            float previewY = portraitRect.yMax + 2f;
            Widgets.Label(new Rect(inner.x, previewY, inner.width, 24f), T("IC_PreviewTitle"));
            Rect newPortraitRect = new Rect(portraitRect.x, previewY + 24f, portraitSize.x, portraitSize.y);
            RenderTexture newPortrait = preview.Get(config, pawn, portraitSize);
            if (newPortrait != null) GUI.DrawTexture(newPortraitRect, newPortrait);
            else Widgets.Label(newPortraitRect, T("IC_PreviewUnavailable"));

            float y = newPortraitRect.yMax + 8f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "IC_CurrentRace".Translate(pawn.def.LabelCap));
            y += 24f;
            string gender = pawn.gender == Gender.None ? T("IC_None") : pawn.gender.GetLabel().CapitalizeFirst();
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "IC_CurrentGender".Translate(gender));
            y += 24f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "IC_CurrentAge".Translate(pawn.ageTracker.AgeBiologicalYears));
            y += 30f;

            GUI.color = ConversionForecast.Muted;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, y, inner.width, Mathf.Max(0f, inner.yMax - y)), T("IC_PreviewNote"));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private static bool ButtonRow(Rect inner, ref float y, string label, string value, bool enabled)
        {
            Rect labelRect = new Rect(inner.x, y, LabelWidth, RowHeight);
            Rect buttonRect = new Rect(inner.x + LabelWidth, y, inner.width - LabelWidth, RowHeight);
            y += RowHeight + 4f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            return Widgets.ButtonText(buttonRect, value, true, true, enabled);
        }

        private void DrawSettings(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect outer = rect.ContractedBy(10f);
            List<HarAddonOption> addons = HarCompat.Addons(config.race);
            int visibleAddons = 0;
            foreach (HarAddonOption addon in addons) if (addon.customizable && addon.count > 1) visibleAddons++;
            Rect view = new Rect(0f, 0f, outer.width - 16f, 660f + visibleAddons * RowHeight);
            Widgets.BeginScrollView(outer, ref settingsScroll, view);
            Rect inner = view;
            float y = inner.y;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), T("IC_SetupSettings"));
            y += 30f;
            bool hasRace = config.race != null;

            // Race
            string raceLabel = hasRace ? config.race.LabelCap.ToString() : T("IC_None");
            if (ButtonRow(inner, ref y, T("IC_FieldRace"), raceLabel, true))
            {
                Find.WindowStack.Add(new Dialog_SelectRace(config.race, pawn.def, OnRaceChosen));
            }

            // Gender
            bool locked = RaceUtility.GenderLocked(config.race, out Gender fixedGender);
            string genderLabel = config.gender == Gender.None ? T("IC_None") : config.gender.GetLabel().CapitalizeFirst();
            if (locked)
            {
                genderLabel = genderLabel + " (" + T("IC_GenderLocked") + ")";
            }
            if (ButtonRow(inner, ref y, T("IC_FieldGender"), genderLabel, hasRace && !locked))
            {
                config.gender = config.gender == Gender.Male ? Gender.Female : Gender.Male;
                RaceUtility.Validate(config);
                RefreshForecast();
            }

            // Age
            if (hasRace)
            {
                float minAge = RaceUtility.AdultMinAge(config.race);
                float maxAge = RaceUtility.MaxAge(config.race);
                Listing_Standard listing = new Listing_Standard();
                listing.Begin(new Rect(inner.x, y, inner.width, 34f));
                float raw = listing.SliderLabeled("IC_FieldAge".Translate(Mathf.FloorToInt(config.ageYears)).Resolve(), config.ageYears, minAge, maxAge, 0.4f);
                listing.End();
                y += 36f;
                if (Mathf.Abs(raw - config.ageYears) > 0.001f)
                {
                    config.ageYears = Mathf.Clamp(Mathf.Round(raw), minAge, maxAge);
                    RefreshForecast();
                }
                if (Widgets.ButtonText(new Rect(inner.x + LabelWidth, y, inner.width - LabelWidth, 26f), T("IC_ResetAge")))
                {
                    config.ageYears = RaceUtility.ProportionalAge(pawn, config.race);
                    RefreshForecast();
                }
                y += 32f;
            }

            // Body type
            if (ButtonRow(inner, ref y, T("IC_FieldBody"), DefLabel(config.bodyType), hasRace))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(T("IC_Auto"), () => config.bodyType = null)
                };
                foreach (BodyTypeDef body in RaceUtility.AllowedBodyTypes(config.race, config.gender))
                {
                    BodyTypeDef local = body;
                    options.Add(new FloatMenuOption(DefLabel(local), () => config.bodyType = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Head type
            if (ButtonRow(inner, ref y, T("IC_FieldHead"), DefLabel(config.headType), hasRace))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(T("IC_Auto"), () => config.headType = null)
                };
                foreach (HeadTypeDef head in RaceUtility.AllowedHeadTypes(config.race, config.gender))
                {
                    HeadTypeDef local = head;
                    options.Add(new FloatMenuOption(DefLabel(local), () => config.headType = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Hair
            bool hasHair = hasRace && RaceUtility.HasHair(config.race);
            if (ButtonRow(inner, ref y, T("IC_FieldHair"), hasHair ? DefLabel(config.hair) : T("IC_None"), hasHair))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(T("IC_Auto"), () => config.hair = null)
                };
                foreach (HairDef hair in RaceUtility.AllHair())
                {
                    HairDef local = hair;
                    options.Add(new FloatMenuOption(DefLabel(local), () => config.hair = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Beard
            bool hasBeard = hasRace && RaceUtility.HasBeard(config.race, config.gender);
            if (ButtonRow(inner, ref y, T("IC_FieldBeard"), hasBeard ? DefLabel(config.beard) : T("IC_None"), hasBeard))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(T("IC_Auto"), () => config.beard = null)
                };
                foreach (BeardDef beard in RaceUtility.AllBeards())
                {
                    BeardDef local = beard;
                    options.Add(new FloatMenuOption(DefLabel(local), () => config.beard = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Hair color
            if (hasHair)
            {
                DrawHairColor(inner, ref y);
            }
            if (hasRace)
            {
                DrawSkinColor(inner, ref y);
            }
            foreach (HarAddonOption addon in addons)
            {
                if (!addon.customizable || addon.count <= 1) continue;
                HarAddonOption selectedAddon = addon;
                int value = config.addonVariants != null && addon.index < config.addonVariants.Count ? config.addonVariants[addon.index] : -1;
                string choice = value < 0 ? T("IC_Auto") : "IC_AddonVariant".Translate(value + 1).Resolve();
                if (ButtonRow(inner, ref y, addon.label, choice, true))
                {
                    var options = new List<FloatMenuOption> {
                        new FloatMenuOption(T("IC_Auto"), () => SetAddonChoice(addons, selectedAddon.index, -1))
                    };
                    for (int i = 0; i < addon.count; i++)
                    {
                        int local = i;
                        options.Add(new FloatMenuOption("IC_AddonVariant".Translate(i + 1).Resolve(), () => SetAddonChoice(addons, selectedAddon.index, local)));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            Widgets.EndScrollView();
        }

        private void SetAddonChoice(List<HarAddonOption> addons, int index, int value)
        {
            if (config.addonVariants == null) config.addonVariants = new List<int>();
            while (config.addonVariants.Count < addons.Count) config.addonVariants.Add(-1);
            int first = index;
            while (first > 0 && addons[first].linked) first--;
            int last = index;
            while (last + 1 < addons.Count && addons[last + 1].linked) last++;
            for (int i = first; i <= last; i++) config.addonVariants[i] = value;
        }

        private void DrawSkinColor(Rect inner, ref float y)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, y, LabelWidth, 26f), T("IC_FieldSkinColor"));
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(inner.x + LabelWidth, y, inner.width - LabelWidth, 26f), T("IC_Auto"), true, true, config.overrideSkinColor))
            {
                config.overrideSkinColor = false;
            }
            y += 30f;
            float startX = inner.x + LabelWidth;
            float x = startX;
            foreach (Color color in RaceUtility.SkinPalette)
            {
                if (x + 20f > inner.xMax)
                {
                    x = startX;
                    y += 24f;
                }
                Rect swatch = new Rect(x, y, 20f, 20f);
                Widgets.DrawBoxSolid(swatch, color);
                if (config.overrideSkinColor && config.skinColor.IndistinguishableFrom(color))
                {
                    Widgets.DrawBox(swatch, 2);
                }
                if (Widgets.ButtonInvisible(swatch))
                {
                    config.overrideSkinColor = true;
                    config.skinColor = color;
                }
                x += 24f;
            }
            y += 28f;
        }

        private void DrawHairColor(Rect inner, ref float y)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, y, LabelWidth, 26f), T("IC_FieldHairColor"));
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(inner.x + LabelWidth, y, inner.width - LabelWidth, 26f), T("IC_Auto"), true, true, config.overrideHairColor))
            {
                config.overrideHairColor = false;
            }
            y += 30f;
            float startX = inner.x + LabelWidth;
            float x = startX;
            foreach (Color color in RaceUtility.HairPalette)
            {
                if (x + 20f > inner.xMax)
                {
                    x = startX;
                    y += 24f;
                }
                Rect swatch = new Rect(x, y, 20f, 20f);
                Widgets.DrawBoxSolid(swatch, color);
                if (config.overrideHairColor && config.hairColor.IndistinguishableFrom(color))
                {
                    Widgets.DrawBox(swatch, 2);
                }
                if (Widgets.ButtonInvisible(swatch))
                {
                    config.overrideHairColor = true;
                    config.hairColor = color;
                }
                x += 24f;
            }
            y += 28f;
        }

        private void DrawForecast(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), T("IC_SetupForecast"));

            Rect outRect = new Rect(inner.x, inner.y + 30f, inner.width, inner.height - 30f);
            float viewWidth = outRect.width - 16f;
            string note = T("IC_SetupForecastNote");

            float total = 0f;
            foreach (ForecastLine line in forecast)
            {
                total += Text.CalcHeight(line.text, viewWidth - (line.indent ? 16f : 0f)) + 4f;
            }
            Text.Font = GameFont.Tiny;
            float noteHeight = Text.CalcHeight(note, viewWidth);
            Text.Font = GameFont.Small;
            total += noteHeight + 12f;

            Rect viewRect = new Rect(0f, 0f, viewWidth, total);
            Widgets.BeginScrollView(outRect, ref forecastScroll, viewRect);
            float y = 0f;
            foreach (ForecastLine line in forecast)
            {
                float indent = line.indent ? 16f : 0f;
                float lineHeight = Text.CalcHeight(line.text, viewWidth - indent);
                GUI.color = line.color;
                Widgets.Label(new Rect(indent, y, viewWidth - indent, lineHeight), line.text);
                y += lineHeight + 4f;
            }
            GUI.color = ConversionForecast.Muted;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0f, y + 8f, viewWidth, noteHeight), note);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        private void DrawButtons(Rect rect)
        {
            const float width = 160f;
            Rect cancel = new Rect(rect.xMax - width * 2f - 10f, rect.y, width, rect.height);
            Rect accept = new Rect(rect.xMax - width, rect.y, width, rect.height);
            if (Widgets.ButtonText(cancel, T("IC_Cancel")))
            {
                Close();
            }
            bool valid = config.race != null && config.race != pawn.def;
            if (Widgets.ButtonText(accept, T("IC_Accept"), true, true, valid))
            {
                pod.SetPending(pawn, config);
                Close();
            }
        }

        private void OnRaceChosen(ThingDef race)
        {
            if (config.race != race) config.addonVariants?.Clear();
            config.race = race;
            RaceUtility.ApplyRaceDefaults(config, pawn);
            RefreshForecast();
        }
    }
}
