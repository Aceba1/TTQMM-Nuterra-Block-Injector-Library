using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Nuterra.BlockInjector
{
    public class CustomCorporation
    {
        public int CorpID { get; internal set; }
        public string Name { get; internal set; }
        public int GradesAmount { get; internal set; }
        public int[] XPLevels { get; internal set; }
        public bool HasLicense { get; internal set; }
        public Sprite CorpIcon { get; internal set; }
        public Sprite SelectedCorpIcon { get; internal set; }
        public Sprite ModernCorpIcon { get; internal set; }

        public CustomCorporation(int corpID, string name, int gradesAmount = 1, int[] xpLevels = null, bool hasLicense = false, Sprite corpIcon = null, Sprite selectedCorpIcon = null, Sprite modernCorpIcon = null)
        {
            CorpID = corpID;
            Name = name;
            GradesAmount = Math.Max(1, gradesAmount);
            XPLevels = xpLevels;
            HasLicense = false;
            CorpIcon = corpIcon;
            SelectedCorpIcon = selectedCorpIcon;
            ModernCorpIcon = modernCorpIcon;
        }

        public void Register()
        {
            BlockLoader.Register(this);
        }
    }
}
