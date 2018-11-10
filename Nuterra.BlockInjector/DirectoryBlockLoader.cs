using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    internal static class DirectoryBlockLoader
    {
        internal struct BlockBuilder
        {
            public string Name;
            public string Description;
            public bool DestroyReferenceRenderers;
            public string GamePrefabReference;
            public int ID;
            public string IDNetHex;
            public string IconName;
            public string MeshName;
            public string ColliderMeshName;
            public string MeshTextureName;
            public string MeshMaterialName;
            public int Faction;
            public int Category;
            public int Grade;
            public int Price;
            public int HP;
            public float Mass;
            public IntVector3 BlockExtents;
            public bool APsOnlyAtBottom;
            public IntVector3[] Cells;
            public Vector3[] APs;
            public Vector3 ReferenceOffset;
            public string Recipe;
            public Dictionary<string, SubObj> SubObjects;

            public struct SubObj
            {
                public string MeshName;
                public string ColliderMeshName;
                public string MeshTextureName;
                public string MeshMaterialName;
            }
        }

        public static void LoadBlocks()
        {
            var dir = new DirectoryInfo(Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, "../../../"));
            string BlockPath = Path.Combine(dir.FullName, "Custom Blocks");
            if (!Directory.Exists(BlockPath))
            {
                Directory.CreateDirectory(BlockPath);
                File.WriteAllText(BlockPath + "/Example.json", Properties.Resources.ExampleJson);
            }

            Sprite NoSpriteBlock;
            try
            {
                NoSpriteBlock = GameObjectJSON.GetObjectFromGameResources<Sprite>("Icon_Plus");
            }
            catch
            {
                NoSpriteBlock = new Sprite();
            }
            var CustomBlocks = new DirectoryInfo(BlockPath);
            var cbJson = CustomBlocks.GetFiles("*.json", SearchOption.AllDirectories);
            var cbObj = CustomBlocks.GetFiles("*.obj", SearchOption.AllDirectories);
            var cbPng = CustomBlocks.GetFiles("*.png", SearchOption.AllDirectories);

            foreach (FileInfo Png in cbPng)
            {
                try
                {
                    GameObjectJSON.AddObjectToUserResources(GameObjectJSON.ImageFromFile(Png.FullName), Png.Name);
                    Console.WriteLine("Added " + Png.Name + "\n from " + Png.FullName);
                }
                catch
                {
                    Console.WriteLine("Could not read image " + Png.Name + "\n at " + Png.FullName);
                }
            }
            foreach (FileInfo Obj in cbObj)
            {
                try
                {
                    GameObjectJSON.AddObjectToUserResources(GameObjectJSON.MeshFromFile(Obj.FullName), Obj.Name);
                    Console.WriteLine("Added " + Obj.Name + "\n from " + Obj.FullName);
                }
                catch
                {
                    Console.WriteLine("Could not read mesh " + Obj.Name + "\n at " + Obj.FullName);
                }
            }
            foreach (FileInfo Json in cbJson)
            {
                try
                {
                    //Read JSON
                    JObject jObject = JObject.Parse(StripComments(File.ReadAllText(Json.FullName)));
                    bool BlockJSON = jObject.Count == 2;
                    var buildablock = (BlockJSON ? jObject.First : jObject).ToObject<BlockBuilder>(new JsonSerializer() { MissingMemberHandling = MissingMemberHandling.Ignore });
                    BlockPrefabBuilder blockbuilder;

                    bool HasSubObjs = buildablock.SubObjects != null && buildablock.SubObjects.Count != 0;

                    //Prefab reference
                    if (buildablock.GamePrefabReference == null || buildablock.GamePrefabReference == "")
                    {
                        blockbuilder = new BlockPrefabBuilder();
                    }
                    else
                    {
                        if (buildablock.ReferenceOffset != null && buildablock.ReferenceOffset != Vector3.zero)
                        {
                            //Offset Prefab
                            blockbuilder = new BlockPrefabBuilder(buildablock.GamePrefabReference, buildablock.ReferenceOffset, true);
                        }
                        else
                        {
                            blockbuilder = new BlockPrefabBuilder(buildablock.GamePrefabReference, true);
                        }
                    }

                    //If gameobjectJSON exists, use it
                    if (BlockJSON)
                    {
                        GameObjectJSON.CreateGameObject(jObject.Last.ToObject<JObject>(), blockbuilder.Prefab);
                    }

                    //Set IP
                    blockbuilder.SetBlockID(buildablock.ID, buildablock.IDNetHex);

                    //Set Category
                    if (buildablock.Category != 0)
                    {
                        blockbuilder.SetCategory((BlockCategories)buildablock.Category);
                    }
                    else
                    {
                        blockbuilder.SetCategory(BlockCategories.Standard);
                    }

                    //Set Faction (Corp)
                    if (buildablock.Faction != 0)
                    {
                        blockbuilder.SetFaction((FactionSubTypes)buildablock.Faction);
                    }
                    else
                    {
                        blockbuilder.SetFaction(FactionSubTypes.GSO);
                    }

                    //Set Block Grade
                    blockbuilder.SetGrade(buildablock.Grade);

                    //Set HP
                    if (buildablock.HP != 0)
                    {
                        blockbuilder.SetHP(buildablock.HP);
                    }
                    else
                    {
                        blockbuilder.SetHP(100);
                    }

                    //Set Icon
                    if (buildablock.IconName != null && buildablock.IconName != "")
                    {
                        var Tex = GameObjectJSON.GetObjectFromUserResources<Texture2D>(buildablock.IconName);
                        if (Tex == null)
                        {
                            Tex = GameObjectJSON.GetObjectFromGameResources<Texture2D>(buildablock.IconName);
                            if (Tex == null)
                            {
                                var Spr = GameObjectJSON.GetObjectFromGameResources<Sprite>(buildablock.IconName);
                                if (Spr == null)
                                {
                                    blockbuilder.SetIcon(NoSpriteBlock);
                                }
                                else
                                {
                                    blockbuilder.SetIcon(Spr);
                                }
                            }
                            else
                            {
                                blockbuilder.SetIcon(Tex);
                            }
                        }
                        else
                        {
                            blockbuilder.SetIcon(Tex);
                        }
                    }

                    //Set Model
                    if (!HasSubObjs)
                    {
                        //-Get Material
                        Material mat = null;
                        if (buildablock.MeshMaterialName != null && buildablock.MeshMaterialName != "")
                        {
                            mat = new Material(GameObjectJSON.GetObjectFromGameResources<Material>(buildablock.MeshMaterialName));
                        }
                        if (mat == null)
                        {
                            mat = GameObjectJSON.MaterialFromShader();
                        }
                        if (buildablock.MeshTextureName != null && buildablock.MeshTextureName != "")
                        {
                            Texture2D tex = GameObjectJSON.GetObjectFromUserResources<Texture2D>(buildablock.MeshTextureName);
                            if (tex != null)
                            {
                                mat.mainTexture = tex;
                            }
                        }
                        //-Get Mesh
                        Mesh mesh = null;
                        if (buildablock.MeshName != null && buildablock.MeshName != "")
                        {
                            mesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(buildablock.MeshName);
                        }
                        if (mesh == null)
                        {
                            mesh = GameObjectJSON.GetObjectFromGameResources<Mesh>("Cube");
                        }
                        //-Get Collider
                        Mesh colliderMesh = null;
                        if (buildablock.ColliderMeshName != null && buildablock.ColliderMeshName != "")
                        {
                            colliderMesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(buildablock.ColliderMeshName);
                        }
                        //-Apply
                        if (colliderMesh == null)
                        {
                            blockbuilder.SetModel(mesh, true, mat);
                        }
                        else
                        {
                            blockbuilder.SetModel(mesh, colliderMesh, true, mat);
                        }
                    }

                    //Set Name
                    blockbuilder.SetName(buildablock.Name);

                    //Set Desc
                    blockbuilder.SetDescription(buildablock.Description);

                    //Set Price
                    if (buildablock.Price != 0)
                    {
                        blockbuilder.SetPrice(buildablock.Price);
                    }
                    else
                    {
                        blockbuilder.SetPrice(500);
                    }

                    //Set Size
                    if (buildablock.Cells != null)
                    {
                        blockbuilder.SetSizeManual(buildablock.Cells, buildablock.APs);
                    }
                    else
                    {
                        IntVector3 extents = buildablock.BlockExtents;
                        if (extents != null)
                        {
                            extents = IntVector3.one;
                        }

                        blockbuilder.SetSize(extents, (buildablock.APsOnlyAtBottom ? BlockPrefabBuilder.AttachmentPoints.Bottom : BlockPrefabBuilder.AttachmentPoints.All));
                    }

                    //Set Mass
                    if (buildablock.Mass != 0f)
                    {
                        blockbuilder.SetMass(buildablock.Mass);
                    }
                    else
                    {
                        blockbuilder.SetMass(1f);
                    }

                    blockbuilder.RegisterLater(6);

                    if (buildablock.Recipe != null && buildablock.Recipe != "")
                    {
                        string[] recipes = buildablock.Recipe.Replace(" ", "").Split(',');
                        foreach (string recipe in recipes)
                        {
                            try
                            {
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine("Could not read block " + Json.Name + "\n at " + Json.FullName + "\n\n" + E.Message + "\n" + E.StackTrace);
                }
            }
        }

        public static string StripComments(string input)
        {
            // JavaScriptSerializer doesn't accept commented-out JSON,
            // so we'll strip them out ourselves;
            // NOTE: for safety and simplicity, we only support comments on their own lines,
            // not sharing lines with real JSON

            input = Regex.Replace(input, @"^\s*//.*$", "", RegexOptions.Multiline);  // removes comments like this
            input = Regex.Replace(input, @"^\s*/\*(\s|\S)*?\*/\s*$", "", RegexOptions.Multiline); /* comments like this */

            return input;
        }
    }
}