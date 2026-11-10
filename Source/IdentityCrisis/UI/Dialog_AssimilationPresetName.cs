using System;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    internal sealed class Dialog_AssimilationPresetName : Window
    {
        private string name;
        private string error;
        private readonly Func<string, string> submit;
        public override Vector2 InitialSize => new Vector2(440f, 230f);
        public Dialog_AssimilationPresetName(string initial, Func<string, string> submit)
        {
            name = initial ?? "";
            this.submit = submit;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0f, 0f, rect.width, 30f), "IC_V03_PresetName".Translate());
            name = Widgets.TextField(new Rect(0f, 36f, rect.width, 32f), name);
            if (error != null) Widgets.Label(new Rect(0f, 76f, rect.width, 64f), error);
            if (Widgets.ButtonText(new Rect(0f, rect.height - 36f, 140f, 36f), "IC_Cancel".Translate())) Close();
            if (Widgets.ButtonText(new Rect(rect.width - 140f, rect.height - 36f, 140f, 36f), "IC_Accept".Translate()))
            {
                error = submit(name);
                if (error == null) Close();
            }
        }
    }

    internal sealed class Dialog_AssimilationConfirm : Window
    {
        private readonly string message;
        private readonly Action accept;
        public override Vector2 InitialSize => new Vector2(540f, 360f);
        public Dialog_AssimilationConfirm(string message, Action accept)
        {
            this.message = message;
            this.accept = accept;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0f, 0f, rect.width, rect.height - 50f), message);
            if (Widgets.ButtonText(new Rect(0f, rect.height - 36f, 150f, 36f), "IC_Cancel".Translate())) Close();
            if (Widgets.ButtonText(new Rect(rect.width - 150f, rect.height - 36f, 150f, 36f), "IC_Accept".Translate()))
            {
                accept();
                Close();
            }
        }
    }
}
