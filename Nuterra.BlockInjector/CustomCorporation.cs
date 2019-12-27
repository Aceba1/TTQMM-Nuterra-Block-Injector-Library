using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Nuterra
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
    }
}
