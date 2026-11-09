using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // List of every humanlike race from loaded mods, with search.
    public class Dialog_SelectRace : Window
    {
        private const float RowHeight = 46f;

        private readonly ThingDef current;
        private readonly ThingDef exclude;
        private readonly Action<ThingDef> onChosen;
        private string search = "";
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(520f, 640f);

        public Dialog_SelectRace(ThingDef current, ThingDef exclude, Action<ThingDef> onChosen)
        {
            this.current = current;
            this.exclude = exclude;
            this.onChosen = onChosen;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "IC_ChooseRace".Translate());
            Text.Font = GameFont.Small;

            Rect searchLabel = new Rect(inRect.x, inRect.y + 42f, 80f, 28f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(searchLabel, "IC_Search".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            search = Widgets.TextField(new Rect(searchLabel.xMax, searchLabel.y, inRect.width - searchLabel.width, 28f), search);

            List<ThingDef> races = RaceUtility.HumanlikeRaces.Where(r => r != exclude && Matches(r)).ToList();
            Rect outRect = new Rect(inRect.x, searchLabel.yMax + 8f, inRect.width, inRect.yMax - searchLabel.yMax - 8f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, races.Count * RowHeight);
            ThingDef chosen = null;

            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            foreach (ThingDef race in races)
            {
                Rect row = new Rect(0f, y, viewRect.width, RowHeight - 4f);
                if (race == current)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                }
                string label = race.label.NullOrEmpty() ? race.defName : race.LabelCap.ToString();
                Widgets.Label(new Rect(row.x + 6f, row.y + 2f, row.width - 12f, 22f), label);
                GUI.color = ConversionForecast.Muted;
                Text.Font = GameFont.Tiny;
                string mod = race.modContentPack?.Name ?? "?";
                Widgets.Label(new Rect(row.x + 6f, row.y + 23f, row.width - 12f, 18f), "IC_RaceFromMod".Translate(mod));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (!race.description.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(row, race.description);
                }
                if (Widgets.ButtonInvisible(row))
                {
                    chosen = race;
                }
                y += RowHeight;
            }
            Widgets.EndScrollView();

            if (chosen != null)
            {
                onChosen?.Invoke(chosen);
                Close();
            }
        }

        private bool Matches(ThingDef race)
        {
            if (search.NullOrEmpty())
            {
                return true;
            }
            string needle = search.ToLowerInvariant();
            string label = race.label ?? "";
            return label.ToLowerInvariant().Contains(needle) || race.defName.ToLowerInvariant().Contains(needle);
        }
    }
}
