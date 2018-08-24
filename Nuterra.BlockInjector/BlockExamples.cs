using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    public class BaconBlock
    {
        public static void Load()
        {
            Material mat = GameObjectJSON.MaterialFromShader();
            mat.mainTexture = GameObjectJSON.ImageFromFile("bacon_material.png");

            new BlockPrefabBuilder()
                .SetBlockID(10000)
                .SetName("GSO Bacon strip")
                .SetDescription("A long strip of bacon with bullet absoring grease\n" +
                "\n\n" +
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
                .SetModel(GameObjectJSON.MeshFromFile("bacon.obj"), true, mat)
                .SetIcon(GameObjectJSON.SpriteFromImage(GameObjectJSON.ImageFromFile("bacon_icon.png")))
                .RegisterLater();

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
        }
    }
}
