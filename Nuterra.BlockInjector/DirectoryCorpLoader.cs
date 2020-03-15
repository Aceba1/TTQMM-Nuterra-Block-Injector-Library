using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    class DirectoryCorpLoader
    {
        internal struct CorpBuilder
        {
            public int ID;
            public string Name;
            public int GradesAmount;
            public int[] XPLevels;
            public bool HasLicense;

            public string CorpIconName;
            public string SelectedCorpIconName;
            public string ModernCorpIconName;

            public CorpMaterial Material;

            public struct CorpMaterial
            {
                public string TextureName;
                public string GlossTextureName { set => MetallicTextureName = value; }
                public string MetallicTextureName;
                public string EmissionTextureName;
            }
        }

        static DirectoryInfo m_CCDirectory;
        static DirectoryInfo GetCCDirectory
        {
            get
            {
                if (m_CCDirectory == null)
                {
                    string CorpPath = Path.Combine(
                    new DirectoryInfo(Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, "../../../"))
                        .FullName, "Custom Corps");
                    try
                    {
                        if (!Directory.Exists(CorpPath))
                        {
                            Directory.CreateDirectory(CorpPath);
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine("Could not access \"" + CorpPath + "\"!");
                        throw E;
                    }
                    m_CCDirectory = new DirectoryInfo(CorpPath);
                }
                return m_CCDirectory;
            }
        }

        public static IEnumerator<object> LoadCorps(bool LoadResources, bool LoadCorps)
        {
            var CustomCorps = GetCCDirectory;

            if (LoadResources)
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                var cbPng = CustomCorps.GetFiles("*.png", SearchOption.AllDirectories);
                int Count = 0;
                BlockLoader.Timer.Log("Loading json images...");
                yield return null;
                foreach (FileInfo Png in cbPng)
                {
                    try
                    {
                        Texture2D tex = GameObjectJSON.ImageFromFile(Png.FullName);
                        GameObjectJSON.AddObjectToUserResources<Texture2D>(tex, Png.Name);
                        GameObjectJSON.AddObjectToUserResources<Texture>(tex, Png.Name);
                        GameObjectJSON.AddObjectToUserResources<Sprite>(GameObjectJSON.SpriteFromImage(tex), Png.Name);
                        Count++;
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine("Could not read image " + Png.Name + "\n at " + Png.FullName + "\n" + E.Message + "\n" + E.StackTrace);
                    }
                    yield return null;
                }
                BlockLoader.Timer.ReplaceLast("Loaded " + Count.ToString() + " corp images");
                Console.WriteLine($"Took {sw.ElapsedMilliseconds} MS to get corp images");
            }
            if (LoadCorps)
            {
                var ccJson = CustomCorps.GetFiles("*.json", SearchOption.AllDirectories);
                //yield return null;
                foreach (FileInfo Json in ccJson)
                {
                    CreateJSONCorp(Json);
                    yield return null;
                }
            }
            yield break;
        }

        static void L(string Log)
        {
            Console.WriteLine(Time.realtimeSinceStartup.ToString("000.000") + "  " + Log);
        }

        private static void CreateJSONCorp(FileInfo Json)
        {
            try
            {
                L("Get locals for " + Json.Name);
                JObject jObject = JObject.Parse(DirectoryBlockLoader.StripComments(File.ReadAllText(Json.FullName)));
                CorpBuilder jCorp = jObject.ToObject<CorpBuilder>(new JsonSerializer() { MissingMemberHandling = MissingMemberHandling.Ignore });

                L("Read JSON");
                bool CorpAlreadyExists = BlockLoader.CustomCorps.TryGetValue(jCorp.ID, out var ExistingJSONCorp);
                if (CorpAlreadyExists)
                {
                    string name = ExistingJSONCorp.Name;
                    Console.WriteLine("Could not read corp " + Json.Name + "\n at " + Json.FullName + "\n\nCorp ID collides with " + name);
                    return;
                }

                CustomCorporation corp = new CustomCorporation(jCorp.ID, jCorp.Name);

                if(jCorp.GradesAmount != 0)
                {
                    L("Set GradesAmount");
                    corp.GradesAmount = jCorp.GradesAmount;
                }

                if (jCorp.XPLevels != null)
                {
                    L("Set XPLevels");
                    corp.XPLevels = jCorp.XPLevels;
                }

                corp.HasLicense = false;// jCorp.HasLicense;
                
                if (!jCorp.CorpIconName.NullOrEmpty())
                {
                    L("Set CorpIcon");
                    var Spr = GameObjectJSON.GetObjectFromUserResources<Sprite>(jCorp.CorpIconName);
                    if (Spr != null)
                    {
                        corp.CorpIcon = Spr;
                    }
                }

                if (!jCorp.SelectedCorpIconName.NullOrEmpty())
                {
                    L("Set SelectedCorpIcon");
                    var Spr = GameObjectJSON.GetObjectFromUserResources<Sprite>(jCorp.SelectedCorpIconName);
                    if (Spr != null)
                    {
                        corp.SelectedCorpIcon = Spr;
                    }
                }

                if (!jCorp.ModernCorpIconName.NullOrEmpty())
                {
                    L("Set ModernCorpIcon");
                    var Spr = GameObjectJSON.GetObjectFromUserResources<Sprite>(jCorp.ModernCorpIconName);
                    if (Spr != null)
                    {
                        corp.ModernCorpIcon = Spr;
                    }
                }

                if(!jCorp.Material.Equals(default(CorpBuilder.CorpMaterial)))
                {
                    L("Set Material");
                    Console.WriteLine(jCorp.Material.TextureName + " " + jCorp.Material.MetallicTextureName + " " + jCorp.Material.EmissionTextureName);
                    Material corpMat = GameObjectJSON.GetObjectFromGameResources<Material>("GSO_Main");
                    corpMat = GameObjectJSON.SetTexturesToMaterial(true, corpMat,
                            jCorp.Material.TextureName.NullOrEmpty() ? null : GameObjectJSON.GetObjectFromUserResources<Texture2D>(jCorp.Material.TextureName),
                            jCorp.Material.MetallicTextureName.NullOrEmpty() ? null : GameObjectJSON.GetObjectFromUserResources<Texture2D>(jCorp.Material.MetallicTextureName),
                            jCorp.Material.EmissionTextureName.NullOrEmpty() ? null : GameObjectJSON.GetObjectFromUserResources<Texture2D>(jCorp.Material.EmissionTextureName));
                    corpMat.name = corp.Name + "_Main";
                    GameObjectJSON.AddToResourceCache(corpMat, corpMat.name);
                    corp.Material = corpMat;
                }


                corp.Register();
            }
            catch (Exception E)
            {
                Console.WriteLine("Could not read corp " + Json.Name + "\n at " + Json.FullName + "\n\n" + E);
                BlockLoader.Timer.Log($" ! Could not read #{Json.Name} - \"{E.Message}\"");
            }
        }
    }
}
