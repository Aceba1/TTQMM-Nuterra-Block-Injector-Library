using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

namespace Nuterra.BlockInjector
{
    public static class BlockExamples
    {
        public static void Load()
        {
            #region Blocks
            {
                Material mat = GameObjectJSON.MaterialFromShader()
                    .SetTexturesToMaterial(
                        Alpha: GameObjectJSON.ImageFromFile(Properties.Resources.bacon_material_png
                    )
                );
                Material GSOMain = GameObjectJSON.GetObjectFromGameResources<Material>("GSO_Main");
                {
                    var bacon = new BlockPrefabBuilder(/*"GSOBlock(111)", true*/)
                        .SetBlockID(10000) // Eventually, IDs could be either simple numbers (with or without quotes), or strings preferably with an author/pack naming scheme. Such as "author:block"
                        .SetName("GSO Bacon strip")
                        .SetDescription("A long strip of bacon with bullet absoring grease.\n\nOriginating from back when the Nuterra API was still being worked on, and was the first block to be recovered after their vanish...\n" +
                        "\n" +
                        "<b>HeX</b>: uuuhhhhh\n" +
                        "one day a pig was born\n" +
                        "little did people know\n" +
                        "it was the son of god\n" +
                        "Jesus - the holy pig\n" +
                        "was one day cruxified\n" +
                        "he yelled\n" +
                        "then turned into the bacon\n" +
                        "<b>Aceba1</b>: ...I think I'll add that to the description of the block\n" +
                        "<b>HeX</b>: yay\n" +
                        "credit me\n" +
                        "\n" +
                        "  - <b>HeX</b>, 8/22/2018")
                        .SetPrice(500)
                        .SetFaction(FactionSubTypes.GSO)
                        .SetCategory(BlockCategories.Standard)
                        .SetSizeManual(
                        new IntVector3[] {
                            new IntVector3(0,0,0), new IntVector3(1, 0, 0), new IntVector3(2, 0, 0), new IntVector3(3, 0, 0) },
                        new Vector3[] {
                            new Vector3(-.5f, 0f, 0f), new Vector3(0f, .5f, 0f), new Vector3(1f, -.5f, 0f),
                            new Vector3(2f, .5f, 0f), new Vector3(3f, -.5f, 0f), new Vector3(3.5f, 0f, 0f) })
                        .SetMass(4)
                        .SetHP(3000)
                        .SetModel(GameObjectJSON.MeshFromData(Properties.Resources.bacon), true, mat)
                        .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.bacon_icon_png)))
                        .SetDeathExplosionReference((int)BlockTypes.GSOBigBertha_845)
                        .SetRecipe(new Dictionary<ChunkTypes, int> {
                            { ChunkTypes.OleiteJelly, 4 },
                            { ChunkTypes.Wood, 4 }
                        }) // This is the cleaner way to set a recipe
                        .RegisterLater();


                    // Here is what needs to be done for the older way of making recipes below. They should both achieve the same thing

                    //CustomRecipe.RegisterRecipe( // This is the old way to make recipes
                    //    new CustomRecipe.RecipeInput[]
                    //    {
                    //        new CustomRecipe.RecipeInput((int)ChunkTypes.OleiteJelly, 4),
                    //        new CustomRecipe.RecipeInput((int)ChunkTypes.Wood, 4)
                    //    },
                    //    new CustomRecipe.RecipeOutput[]
                    //    {
                    //        new CustomRecipe.RecipeOutput(bacon.RuntimeID)
                    //    });
                } // Bacon
                {
                    var banagun = new BlockPrefabBuilder("GSOMGunFixed(111)", false);
                    banagun.SetBlockID(10001)
                        .SetName("GSO Banana gun")
                        .SetDescription("A very special banana. But not as special as you are, your banana friend tells us.\n\nBefore the official release of TerraTech 1.0, there was a mod called the <b>UltiMod</b>. A collection of mods that added things like the water mod, block replacements, 'improved' EXP joints, and various tools & keybind options. This banana gun was part of the block replacement mod, which back then only reskinned existing blocks.\nBits of code from the <b>UltiMod</b> still persist in mods after 1.0, no longer stuck under one title.\n\nWe've come a long way...")
                        .SetPrice(297)
                        .SetFaction(FactionSubTypes.GSO)
                        .SetCategory(BlockCategories.Weapons)
                        .SetHP(200)
                        .SetRecipe(ChunkTypes.PlumbiteOre, ChunkTypes.LuxiteShard, ChunkTypes.LuxiteShard);
                    // Another way to set recipes for a block. Resembles the string/array system for JSON-Blocks

                    MeshFilter[] componentsInChildren2 = banagun.TankBlock.GetComponentsInChildren<MeshFilter>(true);

                    Texture2D main = GameObjectJSON.ImageFromFile(Properties.Resources.banana_material_png);
                    Texture2D gloss = GameObjectJSON.ImageFromFile(Properties.Resources.banana_gloss_material_png);

                    Material changemat2 = GameObjectJSON.MaterialFromShader().SetTexturesToMaterial(main, gloss);

                    foreach (MeshFilter mes in componentsInChildren2) // This somewhat mimics the JSON-Block parser...
                    {
                        string name = mes.name;
                        if (name == "m_MuzzleFlash_01")
                        {
                            mes.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", GameObjectJSON.ImageFromFile(Properties.Resources.banana_blast_material_png));
                        }
                        else
                        {
                            if (name == "m_GSO_MgunFixed_111_Barrel")
                            {
                                mes.mesh = GameObjectJSON.MeshFromData(Properties.Resources.banana_barrel);
                            }
                            else if (name == "m_GSO_MgunFixed_111_Body")
                            {
                                mes.mesh = GameObjectJSON.MeshFromData(Properties.Resources.banana_body);
                            }
                            else if (name == "m_GSO_MgunFixed_111_Base")
                            {
                                mes.mesh = GameObjectJSON.MeshFromData(Properties.Resources.banana_base);
                            }
                            else
                            {
                                Component.DestroyImmediate(mes);
                                continue;
                            }
                            mes.GetComponent<MeshRenderer>().material = changemat2;
                        }
                    }
                    var firedata = banagun.Prefab.GetComponent<FireData>();
                    firedata.m_MuzzleVelocity *= 1.6f;
                    firedata.m_BulletSprayVariance *= 0.3f;
                    firedata.m_KickbackStrength *= 1.6f;
                    var newbullet = GameObject.Instantiate(firedata.m_BulletPrefab);
                    newbullet.gameObject.SetActive(false);
                    var lr = newbullet.gameObject.GetComponent<LineRenderer>();
                    var colorKeys = lr.colorGradient.colorKeys;
                    for (int i = 0; i < colorKeys.Length; i++)
                    {
                        var color = colorKeys[i];
                        colorKeys[i] = new GradientColorKey(new Color(color.color.r, color.color.g, 0), color.time);
                    }
                    typeof(WeaponRound).GetField("m_Damage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(newbullet, 100);
                    firedata.m_BulletPrefab = newbullet;
                    banagun.SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.banana_icon_png)))
                        .RegisterLater();
                } // Banana Gun
                {
                    var cockpit_s = new BlockPrefabBuilder("GSOLightStud(111)", true)
                        .SetBlockID(9000)
                        .SetName("GSO Top Cockpit")
                        .SetDescription("Pop in here and have a first-person look at the world from this block! (The side with the diamond is the viewing direction)\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)")
                        .SetPrice(300)
                        .SetHP(500)
                        .SetFaction(FactionSubTypes.GSO)
                        .SetCategory(BlockCategories.Accessories)
                        .SetModel(GameObjectJSON.MeshFromData(Properties.Resources.cockpit_small), true, GSOMain)
                        .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_small_icon_png)))
                        .SetSize(IntVector3.one, BlockPrefabBuilder.AttachmentPoints.Bottom)
                        .SetRecipe(new Dictionary<ChunkTypes, int> {
                            { ChunkTypes.PlumbiteOre, 2},
                            { ChunkTypes.RoditeOre, 1},
                            { ChunkTypes.RubberJelly, 2}
                        });

                    var view = new GameObject("FirstPersonAnchor");
                    view.AddComponent<ModuleFirstPerson>();
                    view.transform.parent = cockpit_s.TankBlock.transform;

                    cockpit_s.RegisterLater();
                } // GSO Top FPV
                {
                    var cockpit_s2 = new BlockPrefabBuilder(BlockTypes.GSOLightStud_111, true) // "GSOLightStud(111)"
                        .SetBlockID(9005)
                        .SetName("GSO Sided Swerve Cockpit")
                        .SetDescription("Just like the other cockpit, but can be mounted on the sides of things for a better look at your surroundings! Note: Orientation requires adjusting\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)")
                        .SetPrice(300)
                        .SetHP(500)
                        .SetFaction(FactionSubTypes.GSO)
                        .SetCategory(BlockCategories.Accessories)
                        .SetModel(GameObjectJSON.MeshFromData(Properties.Resources.cockpit_small_2), true, GSOMain)
                        .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_small_2_icon_png)))
                        .SetSizeManual(new IntVector3[] { IntVector3.zero }, new Vector3[] { Vector3.down * 0.5f, Vector3.back * 0.5f })
                        .SetRecipe(new Dictionary<ChunkTypes, int> {
                            { ChunkTypes.PlumbiteOre, 3},
                            { ChunkTypes.RodiusCapsule, 1},
                            { ChunkTypes.RubberJelly, 2}
                        });

                    var view1 = new GameObject("FirstPersonAnchor");
                    view1.AddComponent<ModuleFirstPerson>();
                    view1.transform.parent = cockpit_s2.TankBlock.transform;
                    view1.transform.rotation = Quaternion.Euler(-90, 0, 0);

                    cockpit_s2.RegisterLater();

                } // GSO Front FPV
                {
                    var cockpit_l = new BlockPrefabBuilder(11, Vector3.one * 0.5f, true) /*"GSOLightStud(111)"*/
                        .SetBlockID(9001)
                        .SetName("GSO Observatory")
                        .SetDescription("Mount this gigantic hamsterball to your tech to be right in the action!\nThis reorients itself to the direction of the cab\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)\n\nAnother recovery from the original Nuterra API, along with the FPV Top Cockpit. This model remains intact.")
                        .SetPrice(500)
                        .SetHP(2500)
                        .SetGrade(1)
                        .SetFaction(FactionSubTypes.GSO)
                        .SetCategory(BlockCategories.Accessories)
                        .SetModel(GameObjectJSON.MeshFromData(Properties.Resources.cockpit_large), true, GSOMain)
                        .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_large_icon_png)))
                        .SetSize(IntVector3.one * 2, BlockPrefabBuilder.AttachmentPoints.Bottom)
                        .SetRecipe(new Dictionary<ChunkTypes, int> {
                            { ChunkTypes.PlumbiaIngot, 6 },
                            { ChunkTypes.RodiusCapsule, 3 },
                            { ChunkTypes.ErudianCrystal, 2 },
                            { ChunkTypes.RubberJelly, 3 }
                        });

                    var view2 = new GameObject("FirstPersonAnchor");
                    view2.AddComponent<ModuleFirstPerson>().AdaptToMainRot = true;
                    view2.transform.parent = cockpit_l.TankBlock.transform;
                    view2.transform.localPosition = Vector3.one * 0.5f;

                    cockpit_l.RegisterLater();
                } // GSO Dome FPV
                {
                    var cockpit_ven = new BlockPrefabBuilder(BlockTypes.VENLightStud_111, Vector3.forward * 0.5f, true)
                        .SetBlockID(9002)//, "517376c14c30592c")
                        .SetName("VEN Observatory")
                        .SetDescription("A slim, lower observatory that could fit nice on top or below a plane\nThis reorients itself to the direction of the cab\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)\n\nRedesign provided by <b>Mr. Starch</b>")
                        .SetPrice(500)
                        .SetHP(2500)
                        .SetGrade(1)
                        .SetFaction(FactionSubTypes.VEN)
                        .SetCategory(BlockCategories.Accessories)
                        .SetModel(GameObjectJSON.MeshFromData(Properties.Resources.cockpit_ven), true, GameObjectJSON.GetObjectFromGameResources<Material>("VEN_Main"))
                        .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_ven_icon_png)))
                        .SetSize(new IntVector3(1, 1, 2), BlockPrefabBuilder.AttachmentPoints.Bottom)
                        .SetRecipe(new Dictionary<ChunkTypes, int> {
                            { ChunkTypes.PlumbiaIngot, 2},
                            { ChunkTypes.RodiusCapsule, 1},
                            { ChunkTypes.TitaniaIngot, 1},
                            { ChunkTypes.RubberJelly, 3}
                        });

                    var view3 = new GameObject("FirstPersonAnchor");
                    view3.AddComponent<ModuleFirstPerson>().AdaptToMainRot = true;
                    view3.transform.parent = cockpit_ven.TankBlock.transform;
                    view3.transform.localPosition = Vector3.forward * 0.5f + Vector3.down * 0.1f;

                    cockpit_ven.RegisterLater();
                } // VEN Slim FPV
            }
            #endregion

            var thng = new GameObject();
            var thnng = thng.AddComponent<FirstPersonCamera>();
            BlockLoader.DelayAfterSingleton(thnng.Manual_Awake);
        }
    }
}