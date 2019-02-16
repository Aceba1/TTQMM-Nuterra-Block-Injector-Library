using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    public class BlockExamples
    {
        public static void Load()
        {
            Material mat = GameObjectJSON.MaterialFromShader();
            mat.mainTexture = GameObjectJSON.ImageFromFile(Properties.Resources.bacon_material_png);
            Material GSOMain = GameObjectJSON.GetObjectFromGameResources<Material>("GSO_Main");

            {

                new BlockPrefabBuilder(/*"GSOBlock(111)", true*/)
                    .SetBlockID(10000, "95f04b12b0e9537c")
                    .SetName("GSO Bacon strip")
                    .SetDescription("A long strip of bacon with bullet absoring grease\n" +
                    "\n" +
                    "(HeX) uuuhhhhh\n" +
                    "one day a pig was born\n" +
                    "little did people know\n" +
                    "it was the son of god\n" +
                    "Jesus - the holy pig\n" +
                    "was one day cruxified\n" +
                    "he yelled\n" +
                    "then turned into the bacon\n" +
                    "(WhitePaw) ...I think I'll add that to the description of the block\n" +
                    "(HeX) yay\n" +
                    "credit me\n" +
                    "\n" +
                    " ~HeX, 8/22/2018")
                    .SetPrice(500)
                    .SetFaction(FactionSubTypes.GSO)
                    .SetCategory(BlockCategories.Standard)
                    .SetSizeManual(
                    new IntVector3[] {
                    new IntVector3(0,0,0),
                    new IntVector3(1, 0, 0),
                    new IntVector3(2, 0, 0),
                    new IntVector3(3, 0, 0)
                    },
                    new Vector3[] {
                    new Vector3(-.5f, 0f, 0f),
                    new Vector3(0f, .5f, 0f),
                    new Vector3(1f, -.5f, 0f),
                    new Vector3(2f, .5f, 0f),
                    new Vector3(3f, -.5f, 0f),
                    new Vector3(3.5f, 0f, 0f)
                    })
                    .SetMass(4)
                    .SetHP(3000)
                    .SetModel(GameObjectJSON.MeshFromFile(Properties.Resources.bacon, "bacon"), true, mat)
                    .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.bacon_icon_png)))
                    .RegisterLater();

            }

            {

                var banagun = new BlockPrefabBuilder("GSOMGunFixed(111)", false);
                banagun.SetBlockID(10001, "daed1d86d809998a")
                    .SetName("GSO Banana gun")
                    .SetDescription("A very special banana. But not as special as you are, your banana friend tells us.")
                    .SetPrice(297)
                    .SetFaction(FactionSubTypes.GSO)
                    .SetCategory(BlockCategories.Weapons)
                    .SetHP(200);

                MeshFilter[] componentsInChildren2 = banagun.TankBlock.GetComponentsInChildren<MeshFilter>(true);

                Texture2D main = GameObjectJSON.ImageFromFile(Properties.Resources.banana_material_png);
                Texture2D gloss = GameObjectJSON.ImageFromFile(Properties.Resources.banana_gloss_material_png);

                Material changemat2 = GameObjectJSON.MaterialFromShader();

                changemat2.SetTexture("_MainTex", main);
                changemat2.SetTexture("_MetallicGlossMap", gloss);

                foreach (MeshFilter mes in componentsInChildren2)
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
                            mes.mesh = GameObjectJSON.MeshFromFile(Properties.Resources.banana_barrel, "banana_barrel");
                        }
                        else if (name == "m_GSO_MgunFixed_111_Body")
                        {
                            mes.mesh = GameObjectJSON.MeshFromFile(Properties.Resources.banana_body, "banana_body");
                        }
                        else if (name == "m_GSO_MgunFixed_111_Base")
                        {
                            mes.mesh = GameObjectJSON.MeshFromFile(Properties.Resources.banana_base, "banana_base");
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
                firedata.m_MuzzleVelocity *= 1.5f;
                firedata.m_BulletSprayVariance *= 0.5f;
                firedata.m_KickbackStrength *= 1.25f;
                banagun.SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.banana_icon_png)))
                    .RegisterLater();

            }

            {

                var cockpit_s = new BlockPrefabBuilder("GSOLightStud(111)", true)
                    .SetBlockID(9000, "66a82861496cfa13")
                    .SetName("GSO Top Cockpit")
                    .SetDescription("Pop in here and have a first-person look at the world from this block! (The side with the diamond is the viewing direction)\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)")
                    .SetPrice(300)
                    .SetHP(500)
                    .SetFaction(FactionSubTypes.GSO)
                    .SetCategory(BlockCategories.Accessories)
                    .SetModel(GameObjectJSON.MeshFromFile(Properties.Resources.cockpit_small, "cockpit_small"), true, GSOMain)
                    .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_small_icon_png)))
                    .SetSize(IntVector3.one, BlockPrefabBuilder.AttachmentPoints.Bottom);

                var view = new GameObject("FirstPersonAnchor");
                view.AddComponent<ModuleFirstPerson>();
                view.transform.parent = cockpit_s.TankBlock.transform;

                cockpit_s.RegisterLater();

            }

            {

                var cockpit_s2 = new BlockPrefabBuilder("GSOLightStud(111)", true)
                    .SetBlockID(9005, "9a6b06c93f545c61")
                    .SetName("GSO Sided Swerve Cockpit")
                    .SetDescription("Just like the other cockpit, but can be mounted on the sides of things for a better look at your surroundings!\nNOTICE: Make sure the red AP is facing up!\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)")
                    .SetPrice(300)
                    .SetHP(500)
                    .SetFaction(FactionSubTypes.GSO)
                    .SetCategory(BlockCategories.Accessories)
                    .SetModel(GameObjectJSON.MeshFromFile(Properties.Resources.cockpit_small_2, "cockpit_small_2"), true, GSOMain)
                    .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_small_2_icon_png)))
                    .SetSizeManual(new IntVector3[] { IntVector3.zero }, new Vector3[] { Vector3.down * 0.5f, Vector3.back * 0.5f });

                var view1 = new GameObject("FirstPersonAnchor");
                view1.AddComponent<ModuleFirstPerson>();
                view1.transform.parent = cockpit_s2.TankBlock.transform;
                view1.transform.rotation = Quaternion.Euler(-90, 0, 0);

                cockpit_s2.RegisterLater();

            }

            {
                new BlockPrefabBuilder(GameObjectJSON.GetBlockFromAssetTable("SPEColourBlock11_Yellow (111)"), false)
                    .SetBlockID((int)BlockTypes.SPEColourBlock11_Yellow_111)
                    .SetFaction(FactionSubTypes.SPE)
                    .SetCategory(BlockCategories.Standard)
                    .SetName("Barry Bee Benson Block")
                    .SetDescription("A colour block for making BEES!\n\n\"According to all known laws of aviation, there is no way that a bee should be able to fly. Its wings are too small to get its fat little body off the ground. The bee, of course, flies anyways. Because bees don't care what humans think is impossible.\"")
                    .RegisterLater();
            }

            {

                var cockpit_l = new BlockPrefabBuilder("GSOLightStud(111)", Vector3.one * 0.5f, true)
                    .SetBlockID(9001, "6a9262c04f45a53c")
                    .SetName("GSO Observatory")
                    .SetDescription("Mount this gigantic hamsterball to your tech to be right in the action!\nThis reorients itself to the direction of the cab\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)")
                    .SetPrice(500)
                    .SetHP(2500)
                    .SetGrade(1)
                    .SetFaction(FactionSubTypes.GSO)
                    .SetCategory(BlockCategories.Accessories)
                    .SetModel(GameObjectJSON.MeshFromFile(Properties.Resources.cockpit_large, "cockpit_large"), true, GSOMain)
                    .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_large_icon_png)))
                    .SetSize(IntVector3.one * 2, BlockPrefabBuilder.AttachmentPoints.Bottom);

                var view2 = new GameObject("FirstPersonAnchor");
                view2.AddComponent<ModuleFirstPerson>().AdaptToMainRot = true;
                view2.transform.parent = cockpit_l.TankBlock.transform;
                view2.transform.localPosition = Vector3.one * 0.5f;

                cockpit_l.RegisterLater();

            }

            {

                var cockpit_ven = new BlockPrefabBuilder("VENLightStud(111)", Vector3.forward * 0.5f, true)
                    .SetBlockID(9002, "517376c14c30592c")
                    .SetName("VEN Observatory")
                    .SetDescription("A slim, lower observatory that could fit nice on top or below a plane\nThis reorients itself to the direction of the cab\n\nRight click and drag to look and Cycle views with R (and backwards with Shift held down)")
                    .SetPrice(500)
                    .SetHP(2500)
                    .SetGrade(1)
                    .SetFaction(FactionSubTypes.VEN)
                    .SetCategory(BlockCategories.Accessories)
                    .SetModel(GameObjectJSON.MeshFromFile(Properties.Resources.cockpit_ven, "cockpit_ven"), true, GameObjectJSON.GetObjectFromGameResources<Material>("Venture_Main"))
                    .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile(Properties.Resources.cockpit_ven_icon_png)))
                    .SetSize(new IntVector3(1, 1, 2), BlockPrefabBuilder.AttachmentPoints.Bottom);

                var view3 = new GameObject("FirstPersonAnchor");
                view3.AddComponent<ModuleFirstPerson>().AdaptToMainRot = true;
                view3.transform.parent = cockpit_ven.TankBlock.transform;
                view3.transform.localPosition = Vector3.forward * 0.5f + Vector3.down * 0.25f;

                cockpit_ven.RegisterLater();

            }

            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.OleiteJelly, 4),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.Wood, 4)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(10000)
                    });
            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiteOre, 1),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.LuxiteShard, 2)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(10001)
                    });

            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiteOre, 2),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RoditeOre, 1),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RubberJelly, 2)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(9000)
                    });

            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiaIngot, 6),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RodiusCapsule, 3),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.ErudianCrystal, 2),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RubberJelly, 3)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(9001)
                    });

            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiteOre, 3),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RodiusCapsule, 1),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RubberJelly, 2)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(9005)
                    });

            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiaIngot, 2),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RodiusCapsule, 1),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.TitaniaIngot, 1),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RubberJelly, 3)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(9002)
                    }, RecipeTable.Recipe.OutputType.Items, "venfab");

            //BlockLoader.DelayAfterSingleton(new CustomChunk()
            //{
            //    BasePrefab = GameObjectJSON.GetObjectFromGameResources<Transform>("Ore_Wood"),
            //    ChunkID = 5854,
            //    Description = "but I wanna dIE",
            //    Name = "dank w00d",
            //    Mass = 2,
            //    SaleValue = 420,
            //    Restitution = 0f,
            //    FrictionDynamic = 0f,
            //    FrictionStatic = 0f
            //}.Register);

            var thng = new GameObject();
            var thnng = thng.AddComponent<FirstPersonCamera>();
            BlockLoader.DelayAfterSingleton(thnng.Manual_Awake);

            // Site used for Hash: https://www.random.org/bytes/
        }
    }
}