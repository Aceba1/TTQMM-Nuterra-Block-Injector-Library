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
                Debug.Log("Made Bacon Block");
            
                var banagun = new BlockPrefabBuilder("GSOMGunFixed(111)", false);
                banagun.SetBlockID(9999, "daed1d86d809998a")
                    .SetName("GSO Banana gun")
                    .SetDescription("A very special banana. But not as special as you are, your banana friend tells us.")
                    .SetPrice(297)
                    .SetHP(200)
                    .SetFaction(FactionSubTypes.GSO);

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
                Debug.Log("Made BananaGun Block");


            }
            catch (Exception E)
            {
                UnityEngine.Debug.LogException(E);
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
                    new CustomRecipe.RecipeInput((int)ChunkTypes.PlumbiteOre, 3),
                    new CustomRecipe.RecipeInput((int)ChunkTypes.LuxiteShard, 3)
                    },
                    new CustomRecipe.RecipeOutput[]
                    {
                    new CustomRecipe.RecipeOutput(9999)
                    });
            // Site used for Hash: https://www.random.org/bytes/
        }
    }
}
