using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Custom icons live in Textures/UI/IdentityCrisis. If a custom icon is missing,
    // the vanilla texture is used instead, so the UI never shows BadTex.
    [StaticConstructorOnStartup]
    public static class IC_Textures
    {
        public static readonly Texture2D Assimilate = Load("UI/IdentityCrisis/Assimilate", "UI/Commands/ForPrisoners");
        public static readonly Texture2D Edit = Load("UI/IdentityCrisis/EditSetup", "UI/Buttons/Rename");
        public static readonly Texture2D Cancel = Load(null, "UI/Designators/Cancel");
        public static readonly Texture2D Eject = Load(null, "UI/Commands/PodEject");

        private static Texture2D Load(string customPath, string vanillaPath)
        {
            Texture2D tex = null;
            if (customPath != null)
            {
                tex = ContentFinder<Texture2D>.Get(customPath, false);
            }
            if (tex == null)
            {
                tex = ContentFinder<Texture2D>.Get(vanillaPath, false);
            }
            return tex ?? BaseContent.BadTex;
        }
    }
}
