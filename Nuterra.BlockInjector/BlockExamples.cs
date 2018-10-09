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
            try
            {
                Material mat = GameObjectJSON.MaterialFromShader();
                mat.mainTexture = GameObjectJSON.ImageFromFile("bacon_material.png");
                Material GSOMain = GameObjectJSON.GetObjectFromGameResources<Material>("GSO_Main");

                new BlockPrefabBuilder("GSOBlock(111)", true)
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
                    .SetPrice(5000)
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
                    .SetModel(GameObjectJSON.MeshFromFile("bacon.obj"), true, mat)
                    .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile("bacon_icon.png")))
                    .RegisterLater();



                var banagun = new BlockPrefabBuilder("GSOMGunFixed(111)", false);
                banagun.SetBlockID(10001, "daed1d86d809998a")
                    .SetName("GSO Banana gun")
                    .SetDescription("A very special banana. But not as special as you are, your banana friend tells us.")
                    .SetPrice(297)
                    .SetFaction(FactionSubTypes.GSO)
                    .SetCategory(BlockCategories.Weapons)
                    .SetHP(200);

                MeshFilter[] componentsInChildren2 = banagun.TankBlock.GetComponentsInChildren<MeshFilter>(true);

                Texture2D main = GameObjectJSON.ImageFromFile("banana_material.png");
                Texture2D gloss = GameObjectJSON.ImageFromFile("banana_gloss_material.png");

                foreach (MeshFilter mes in componentsInChildren2)
                {
                    string name = mes.name;
                    if (name == "m_MuzzleFlash_01")
                    {
                        mes.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", GameObjectJSON.ImageFromFile("banana_blast_material.png"));
                    }
                    else
                    {
                        if (name == "m_GSO_MgunFixed_111_Barrel")
                        {
                            mes.mesh = GameObjectJSON.MeshFromFile("banana_barrel.obj");
                        }
                        else if (name == "m_GSO_MgunFixed_111_Body")
                        {
                            mes.mesh = GameObjectJSON.MeshFromFile("banana_body.obj");
                        }
                        else if (name == "m_GSO_MgunFixed_111_Base")
                        {
                            mes.mesh = GameObjectJSON.MeshFromFile("banana_base.obj");
                        }
                        else
                        {
                            Component.DestroyImmediate(mes);
                            continue;
                        }
                        Material changemat2 = mes.GetComponent<MeshRenderer>().material;
                        changemat2.SetTexture("_MainTex", main);
                        changemat2.SetTexture("_MetallicGlossMap", gloss);
                    }
                }
                banagun.SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile("banana_icon.png")))
                    .RegisterLater();




                var cockpit_s = new BlockPrefabBuilder("GSOBlock(111)", true)
                    .SetBlockID(9000, "66a82861496cfa13")
                .SetName("GSO Top Cockpit")
                .SetDescription("Pop in here and have a first-person look at the world from this block! (The side with the diamond is the viewing direction)\n\nRight click and drag to look and Cycle views with R")
                .SetPrice(300)
                .SetFaction(FactionSubTypes.GSO)
                .SetCategory(BlockCategories.Accessories)
                .SetModel(GameObjectJSON.MeshFromFile("cockpit_small.obj"), false, GSOMain)
                .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile("cockpit_small_icon.png")))
                .SetSize(IntVector3.one, BlockPrefabBuilder.AttachmentPoints.Bottom);

                var view = new GameObject("FirstPersonAnchor");
                view.AddComponent<FirstPersonCamera.ModuleFirstPerson>();
                view.transform.parent = cockpit_s.TankBlock.transform;

                cockpit_s.RegisterLater();


                var cockpit_s2 = new BlockPrefabBuilder("GSOBlock(111)", true)
                    .SetBlockID(9005, "9a6b06c93f545c61")
                .SetName("GSO Sided \"Swerve\" Cockpit")
                .SetDescription("Just like the other cockpit, but can be mounted on the sides of things for a better look at your surroundings! (Make sure that none of the 3 arrows point down)\n\nRight click and drag to look and Cycle views with R")
                .SetPrice(300)
                .SetFaction(FactionSubTypes.GSO)
                .SetCategory(BlockCategories.Accessories)
                .SetModel(GameObjectJSON.MeshFromFile("cockpit_small_2.obj"), false, GSOMain)
                .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile("cockpit_small_2_icon.png")))
                .SetSizeManual(new IntVector3[] { IntVector3.zero }, new Vector3[] { Vector3.down * 0.5f, Vector3.back * 0.5f });

                var view1 = new GameObject("FirstPersonAnchor");
                view1.AddComponent<FirstPersonCamera.ModuleFirstPerson>();
                view1.transform.parent = cockpit_s2.TankBlock.transform;
                view1.transform.rotation = Quaternion.Euler(-90, 0, 0);

                cockpit_s2.RegisterLater();

                var cockpit_l = new BlockPrefabBuilder("GSOBlock(111)", true)
                    .SetBlockID(9001, "6a9262c04f45a53c")
                .SetName("GSO Observatory")
                .SetDescription("Mount this gigantic hamsterball to your tech to be right in the action!\n\nRight click and drag to look and Cycle views with R")
                .SetPrice(500)
                .SetFaction(FactionSubTypes.GSO)
                .SetCategory(BlockCategories.Accessories)
                .SetModel(GameObjectJSON.MeshFromFile("cockpit_large.obj"), true, GSOMain)
                .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile("cockpit_large_icon.png")))
                .SetSize(IntVector3.one * 2, BlockPrefabBuilder.AttachmentPoints.Bottom);

                var view2 = new GameObject("FirstPersonAnchor");
                view2.AddComponent<FirstPersonCamera.ModuleFirstPerson>().AdaptToMainRot = true;
                view2.transform.parent = cockpit_l.TankBlock.transform;
                view2.transform.localPosition = Vector3.one * 0.5f;

                cockpit_l.RegisterLater();
            }
            catch (Exception E)
            {
                UnityEngine.Debug.LogException(E);
            }
            CustomRecipe.RegisterRecipe(
                    new CustomRecipe.RecipeInput[]
                    {
                        new CustomRecipe.RecipeInput((int)ChunkTypes.RubberBrick)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(5854,1,ObjectTypes.Chunk)
                    },
                    RecipeTable.Recipe.OutputType.Items,
                    "gsorefinery");
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
                        new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiteOre, 3),
                        new CustomRecipe.RecipeInput((int)ChunkTypes.LuxiteShard, 3)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                        new CustomRecipe.RecipeOutput(9999)
                    });

            BlockLoader.DelayAfterSingleton(new CustomChunk()
            {
                BasePrefab = GameObjectJSON.GetObjectFromGameResources<Transform>("Ore_Wood"),
                ChunkID = 5854,
                Description = "but I wanna dIE",
                Name = "dank w00d",
                Mass = 2,
                SaleValue = 420,
                Restitution = 0f,
                FrictionDynamic = 0f,
                FrictionStatic = 0f
            }.Register);

            var thng = new GameObject();
            var thnng = thng.AddComponent<FirstPersonCamera>();
            BlockLoader.DelayAfterSingleton(thnng.Manual_Awake);

            // Site used for Hash: https://www.random.org/bytes/
        }

    }
}