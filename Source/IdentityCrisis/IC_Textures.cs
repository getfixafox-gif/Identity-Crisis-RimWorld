using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    [StaticConstructorOnStartup]
    public static class IC_Textures
    {
        public static readonly Texture2D Assimilate = Load("UI/Commands/ForPrisoners");
        public static readonly Texture2D Edit = Load("UI/Buttons/Rename");
        public static readonly Texture2D Cancel = Load("UI/Designators/Cancel");
        public static readonly Texture2D Eject = Load("UI/Commands/PodEject");

        private static Texture2D Load(string path)
        {
            return ContentFinder<Texture2D>.Get(path, false) ?? BaseContent.BadTex;
        }
    }
}
