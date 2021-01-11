using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine;
using Harmony;

namespace Nuterra.BlockInjector
{
    internal static class DirectoryBlockLoader
    {
        static Dictionary<string, Material> HashMAGE = new Dictionary<string, Material>();
        static Dictionary<string, Mesh> ModelCache = new Dictionary<string, Mesh>();
        internal struct BlockBuilder
        {
            public string Name;
            public string Description;
            public bool KeepReferenceRenderers; // Legacy
            public bool KeepRenderers;
            public bool KeepColliders;
            public JValue PrefabReference { set => GamePrefabReference = value; }
            public JValue GamePrefabReference;
            public JValue ExplosionReference { set => DeathExplosionReference = value; }
            public JValue DeathExplosionReference;
            public int ID; //public JValue ID;
            public string IconName;
            public string MeshName;
            public string MeshColliderName { set => ColliderMeshName = value; }
            public string ColliderMeshName;

            public bool NoBoxCollider { set => SupressBoxColliderFallback = value; }
            public bool SupressBoxColliderFallback;

            public float? Friction;
            public float? StaticFriction;
            public float? Bounciness;
            public string TextureName { set => MeshTextureName = value; }
            public string MeshTextureName;

            public string MetallicTextureName { set => MeshGlossTextureName = value; }
            public string GlossTextureName { set => MeshGlossTextureName = value; }
            public string MeshGlossTextureName;

            public string EmissionTextureName { set => MeshEmissionTextureName = value; }
            public string MeshEmissionTextureName;

            public int EmissionMode;
            public string MaterialName { set => MeshMaterialName = value; }
            public string MeshMaterialName;

            public int Corp { set => Faction = value; }
            public int Corporation { set => Faction = value; }
            public int Faction;
            public int Category;
            public int Grade;
            public int Price;
            public int HP;
            public int? DamageableType;
            public int Rarity;
            public float DetachFragility{ set => Fragility = value; }
            public float? Fragility;

            public bool? DropFromCrates;
            public int? PairedBlock;

            public float Mass;
            public Vector3 CentreOfMass { set => CenterOfMass = value; }
            public Vector3? CenterOfMass;
            public IntVector3? BlockExtents;
            public bool MakeAPsAtBottom { set => APsOnlyAtBottom = value; }
            public bool APsOnlyAtBottom;
            public IntVector3[] Cells;

            public string[][] CellsMap { set => CellMap = value; }
            public string[][] CellMap;
            public Vector3[] APs;

            public string RotationGroup;

            public Vector3 PrefabOffset { set => ReferenceOffset = value; }
            public Vector3 PrefabPosition { set => ReferenceOffset = value; }
            public Vector3? ReferenceOffset;
            
            public Vector3 PrefabScale { set => ReferenceScale = value; }
            public Vector3? ReferenceScale;
            
            public Vector3 PrefabRotation { set => ReferenceRotationOffset = value; }
            public Vector3? ReferenceRotationOffset;
            
            public JToken Recipe;
            public string RecipeTable;
            public SubObj[] SubObjects;

            public JObject JSONBLOCK { set => Deserializer = value; }
            public JObject Deserializer;

            public struct SubObj
            {
                public string OverrideName { set => SubOverrideName = value; }
                public string ObjectName { set => SubOverrideName = value; }
                public string SubOverrideName;

                public string ModelName { set => MeshName = value; }
                public string MeshName;
                public int PhysicsLayer { set => Layer = value; }
                public int? Layer;
                public bool DestroyColliders { set => DestroyExistingColliders = value; }
                public bool DestroyExistingColliders;
                public bool DestroyExistingRenderers { set => DestroyExistingRenderer = value; }
                public bool DestroyRenderers { set => DestroyExistingRenderer = value; }
                public bool DestroyExistingRenderer;
                public bool ForceEmission;

                public bool GenerateBoxCollider { set => MakeBoxCollider = value; }
                public bool MakeBoxCollider;
                public bool MakeSphereCollider;
                public string MeshColliderName { set => ColliderMeshName = value; }
                public string ColliderMeshName;

                public float? Friction;
                public float? StaticFriction;
                public float? Bounciness;
                public string TextureName { set => MeshTextureName = value; }
                public string MeshTextureName;
                public string GlossTextureName { set => MeshGlossTextureName = value; }
                public string MeshGlossTextureName;
                public string EmissionTextureName { set => MeshEmissionTextureName = value; }
                public string MeshEmissionTextureName;
                public string MaterialName { set => MeshMaterialName = value; }
                public string MeshMaterialName;

                public Vector3 Position { set => SubPosition = value; }
                public Vector3? SubPosition;
                public Vector3 Scale { set => SubScale = value; }
                public Vector3? SubScale;
                public Vector3 Rotation { set => SubRotation = value; }
                public Vector3? SubRotation;

                //public struct MaterialControl
                //{
                //    public string ShaderName;
                //    public string[] SetShaderKeywords;
                //    public string[] AddShaderKeywords;
                //}
            }
        }

        internal static readonly Type MeshT = typeof(Mesh), Texture2DT = typeof(Texture2D), MaterialT = typeof(Material), TextureT = typeof(Texture),
            SpriteT = typeof(Sprite),
            cT = typeof(ChunkTypes);

        static Dictionary<string, DateTime> FileChanged = new Dictionary<string, DateTime>();

        static DirectoryInfo m_CBDirectory;
        static DirectoryInfo GetCBDirectory
        {
            get
            {
                if (m_CBDirectory == null)
                {
                    string BlockPath = Path.Combine(
                    new DirectoryInfo(Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, "../../../"))
                        .FullName, "Custom Blocks");
                    try
                    {
                        if (!Directory.Exists(BlockPath))
                        {
                            Directory.CreateDirectory(BlockPath);
                            // Add Block Example.json here?
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine("Could not access \"" + BlockPath + "\"!");
                        throw E;
                    }
                    m_CBDirectory = new DirectoryInfo(BlockPath);
                }
                return m_CBDirectory;
            }
        }

        const long WatchDogTimeBreaker = 3000;

        public static IEnumerator<object> LoadBlocks(bool LoadResources, bool LoadBlocks)
        {
            var CustomBlocks = GetCBDirectory;

            if (LoadResources)
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                long TimeBreak = WatchDogTimeBreaker;
                var cbPng = CustomBlocks.GetFiles("*.png", SearchOption.AllDirectories);
                int Count = 0;
                BlockLoader.Timer.Log("Loading json images...");
                yield return null;
                foreach (FileInfo Png in cbPng)
                {
                    bool imgReparse = FileChanged.TryGetValue(Png.FullName, out DateTime lastEdit);
                    if (!imgReparse || lastEdit != Png.LastWriteTime)
                    {
                        try
                        {
                            Texture2D tex = GameObjectJSON.ImageFromFile(Png.FullName);
                            GameObjectJSON.AddObjectToUserResources<Texture2D>(Texture2DT, tex, Png.Name);
                            GameObjectJSON.AddObjectToUserResources<Texture>(TextureT, tex, Png.Name);
                            GameObjectJSON.AddObjectToUserResources<Sprite>(SpriteT, GameObjectJSON.SpriteFromImage(tex), Png.Name);
                            FileChanged[Png.FullName] = Png.LastWriteTime;
                            if (imgReparse)
                            {
                                foreach(var pair in HashMAGE)
                                {
                                    int index = pair.Key.IndexOf(Png.Name);
                                    if (index != -1)
                                    {
                                        int a = pair.Key.IndexOf(";A:"), g = pair.Key.IndexOf(";G:", a), e = pair.Key.IndexOf(";E:", g);
                                        while (index != -1)
                                        {
                                            if (index > e)
                                                pair.Value.SetTexture("_EmissionMap", tex);
                                            if (index > g && index < e)
                                                pair.Value.SetTexture("_MetallicGlossMap", tex);
                                            if (index > a && index < g)
                                                pair.Value.SetTexture("_MainTex", tex);

                                            index = pair.Key.IndexOf(Png.Name, index + 1);
                                        }
                                    }
                                }                                    
                            }
                            Count++;
                        }
                        catch (Exception E)
                        {
                            Console.WriteLine("Could not read image " + Png.Name + "\n at " + Png.FullName + "\n" + E.Message + "\n" + E.StackTrace);
                        }
                        if (TimeBreak < sw.ElapsedMilliseconds)
                        {
                            BlockLoader.Timer.ReplaceLast("Loading json images... (" + Count.ToString() + ")");
                            TimeBreak += sw.ElapsedMilliseconds + WatchDogTimeBreaker;
                            yield return null;
                        }
                    }
                }
                BlockLoader.Timer.ReplaceLast("Loaded " + Count.ToString() + " json images");
                Console.WriteLine($"Took {sw.ElapsedMilliseconds} MS to get json images");

                sw.Restart();
                TimeBreak = WatchDogTimeBreaker;
                var cbObj = CustomBlocks.GetFiles("*.obj", SearchOption.AllDirectories);
                Count = 0;
                int OverlapCount = 0;
                BlockLoader.Timer.Log("Loading json models...");
                yield return null;
                foreach (FileInfo Obj in cbObj)
                {
                    bool Exists = FileChanged.TryGetValue(Obj.FullName, out DateTime lastEdit);
                    if (!Exists || lastEdit != Obj.LastWriteTime)
                    {
                        try
                        {
                            if (!Exists)
                            {
                                Mesh model = new Mesh { name = Obj.Name };
                                GameObjectJSON.MeshFromFile(Obj.FullName, model);
                                GameObjectJSON.AddObjectToUserResources(model, Obj.Name);
                                ModelCache.Add(Obj.Name, model);
                                Count++;
                            }
                            else
                            {
                                GameObjectJSON.MeshFromFile(Obj.FullName, ModelCache[Obj.Name]);
                                OverlapCount++;
                            }
                            FileChanged[Obj.FullName] = Obj.LastWriteTime;
                        }
                        catch (Exception E)
                        {
                            Console.WriteLine("Could not read mesh " + Obj.Name + "\n at " + Obj.FullName + "\n" + E.Message + "\n" + E.StackTrace);
                        }
                        if (TimeBreak < sw.ElapsedMilliseconds)
                        {
                            BlockLoader.Timer.ReplaceLast("Loading json models... (" + Count.ToString() + ")");
                            TimeBreak += sw.ElapsedMilliseconds + WatchDogTimeBreaker;
                            yield return null;
                        }
                    }
                }
                BlockLoader.Timer.ReplaceLast("Loaded " + Count.ToString() + " json models");
                if (OverlapCount != 0)
                {
                    if (OverlapCount == 1) BlockLoader.Timer.Log("There was 1 overlapping model name!");
                    else BlockLoader.Timer.Log("There were " + OverlapCount.ToString() + " overlapping model names!");
                }
                Console.WriteLine($"Took {sw.ElapsedMilliseconds} MS to get json models");
                sw.Stop();
            }
            if (LoadBlocks)
            {
                var cbJson = CustomBlocks.GetFiles("*.json", SearchOption.AllDirectories);
                //yield return null;
                foreach (FileInfo Json in cbJson)
                {
                    if (!FileChanged.TryGetValue(Json.FullName, out DateTime lastEdit) || lastEdit != Json.LastWriteTime)
                    {
                        CreateJSONBlock(Json, Input.GetKey(KeyCode.LeftControl));
                        FileChanged[Json.FullName] = Json.LastWriteTime;
                        yield return null;
                    }
                }
            }
            yield break;
        }

        static void L(string Log, bool On)
        {
            if (On) Console.WriteLine(Time.realtimeSinceStartup.ToString("000.000") + "  " + Log);
        }

        private static void CreateJSONBlock(FileInfo Json, bool l = false)
        {
            try
            {
                L("Get locals for " + Json.Name, l);
                JObject jObject = JObject.Parse(StripComments(File.ReadAllText(Json.FullName)));
                BlockBuilder jBlock = jObject.ToObject<BlockBuilder>(new JsonSerializer() { MissingMemberHandling = MissingMemberHandling.Ignore });
                BlockPrefabBuilder blockbuilder;
                //string ID = jBlock.ID.ToObject<string>();

                L("Read JSON", l);
                /*Local*/
                bool BlockAlreadyExists = BlockLoader.CustomBlocks.TryGetValue(jBlock.ID, out var ExistingJSONBlock); //BlockLoader.NameIDToRuntimeIDTable.TryGetValue(ID, out int overlap);
                if (BlockAlreadyExists && !BlockLoader.AcceptOverwrite)
                {
                    string name = ExistingJSONBlock.Name;//BlockLoader.CustomBlocks[overlap].Name;
                    Console.WriteLine("Could not read block " + Json.Name + "\n at " + Json.FullName + "\n\nBlock ID collides with " + name);
                    BlockLoader.Timer.Log($" ! Could not read #{Json.Name} - Block ID collides with { name }!");
                    return;
                }

                string gpr = jBlock.GamePrefabReference?.ToString();
                if (string.IsNullOrEmpty(gpr))
                {
                    L("New instance", l);
                    blockbuilder = new BlockPrefabBuilder();
                }
                else
                {
                    L("Prefab reference", l);
                    if (int.TryParse(gpr, out int gprID))
                        blockbuilder = new BlockPrefabBuilder(gprID, false);
                    else
                        blockbuilder = new BlockPrefabBuilder(gpr, false);

                    if (jBlock.KeepRenderers) // Keep renderers
                    {
                        if (!jBlock.KeepColliders) // Don't keep colliders
                        {
                            blockbuilder.RemoveChildrenWithComponent(true, null, typeof(Collider));
                        }
                    }
                    else if (!jBlock.KeepReferenceRenderers) // Don't keep renderers
                    {
                        if (!jBlock.KeepColliders) // Don't keep colliders
                        {
                            blockbuilder.RemoveChildrenWithComponent(true, null, typeof(MeshRenderer), typeof(TankTrack), typeof(SkinnedMeshRenderer), typeof(MeshFilter), typeof(Collider));
                        }
                        else // Keep colliders
                        {
                            blockbuilder.RemoveChildrenWithComponent(true, null, typeof(MeshRenderer), typeof(TankTrack), typeof(SkinnedMeshRenderer), typeof(MeshFilter));
                        }
                    }

                    if (jBlock.ReferenceRotationOffset.HasValue && jBlock.ReferenceRotationOffset != Vector3.zero)
                    {
                        L("Rotate Prefab", l);
                        blockbuilder.Prefab.transform.RotateChildren(jBlock.ReferenceRotationOffset.Value);
                    }

                    if (jBlock.ReferenceScale.HasValue && jBlock.ReferenceScale != Vector3.zero)
                    {
                        for (int ti = 0; ti < blockbuilder.Prefab.transform.childCount; ti++)
                        {
                            var chi = blockbuilder.Prefab.transform.GetChild(ti);
                            L("Scale Prefab", l);
                            chi.localPosition = Vector3.Scale(chi.localPosition, jBlock.ReferenceScale.Value);
                            chi.localScale = Vector3.Scale(chi.localScale, jBlock.ReferenceScale.Value);
                        }
                    }

                    if (jBlock.ReferenceOffset.HasValue)
                    {
                        L("Offset Prefab", l);
                        for (int i = 0; i < blockbuilder.Prefab.transform.childCount; i++)
                        {
                            blockbuilder.Prefab.transform.GetChild(i).localPosition += jBlock.ReferenceOffset.Value;
                        }
                    }
                }

                if (jBlock.DeathExplosionReference != null)
                {
                    L("Reference Death Explosion", l);
                    string der = jBlock.DeathExplosionReference.ToString();
                    if (!string.IsNullOrEmpty(der))
                    {
                        if (int.TryParse(der, out int derID))
                            blockbuilder.SetDeathExplosionReference(derID);
                        else
                            blockbuilder.SetDeathExplosionReference(der);
                    }
                }

                if (jBlock.EmissionMode != 0)
                {
                    L("Set EmissionMode", l);
                    blockbuilder.SetCustomEmissionMode((BlockPrefabBuilder.EmissionMode)jBlock.EmissionMode);
                }

                // Give the component this file's path
                blockbuilder._mcb.FilePath = Json.FullName;

                //If gameobjectJSON exists, use it
                if (jBlock.Deserializer != null)
                {
                    L("Use Deserializer", l);
                    GameObjectJSON.CreateGameObject(jBlock.Deserializer, blockbuilder.Prefab);
                }

                L("Set ID", l);
                blockbuilder.SetBlockID(jBlock.ID); //blockbuilder.SetBlockID(ID);

                //Set Category
                L("Set Category", l);
                if (jBlock.Category != 0)
                {
                    blockbuilder.SetCategory((BlockCategories)jBlock.Category);
                }
                else
                {
                    blockbuilder.SetCategory(BlockCategories.Standard);
                }

                L("Set Faction (Corp)", l);
                if (jBlock.Faction != 0)
                {
                    blockbuilder.SetFaction((FactionSubTypes)jBlock.Faction);
                }
                else if (jBlock.Faction < 0)
                {
                    blockbuilder.SetFaction(FactionSubTypes.NULL);
                }
                else
                {
                    blockbuilder.SetFaction(FactionSubTypes.GSO);
                }

                L("Set Block Grade", l);
                blockbuilder.SetGrade(jBlock.Grade);

                L("Set HP", l);
                if (jBlock.HP != 0)
                {
                    blockbuilder.SetHP(jBlock.HP);
                }
                else
                {
                    blockbuilder.SetHP(250);
                }

                if (jBlock.DamageableType.HasValue)
                {
                    L("Set DamageableType", l);
                    blockbuilder.SetDamageableType((ManDamage.DamageableType)jBlock.DamageableType.Value);
                }

                if (jBlock.Fragility.HasValue)
                {
                    L("Set DetachFragility", l);
                    blockbuilder.SetDetachFragility(jBlock.Fragility.Value);
                }

                L("Set Rarity", l);
                blockbuilder.SetRarity((BlockRarity)jBlock.Rarity);

                if (!string.IsNullOrEmpty(jBlock.IconName))
                {
                    L("Set Icon", l);
                    var Spr = GameObjectJSON.GetObjectFromUserResources<Sprite>(SpriteT, jBlock.IconName);
                    if (Spr == null)
                    {
                        blockbuilder.SetIcon((Sprite)null);
                    }
                    else
                    {
                        blockbuilder.SetIcon(Spr);
                    }
                }

                if(jBlock.DropFromCrates.HasValue)
                {
                    L("Set DropFromCrates", l);
                    blockbuilder.SetDropFromCrates(jBlock.DropFromCrates.Value);
                }

                if (jBlock.PairedBlock.HasValue)
                {
                    L("Set PairedBlock", l);
                    blockbuilder.SetPairedBlock(jBlock.PairedBlock.Value);
                }


                /*Local*/
                Material localmat = null;

                bool missingflag1 = string.IsNullOrWhiteSpace(jBlock.MeshTextureName),
                    missingflag2 = string.IsNullOrWhiteSpace(jBlock.MeshGlossTextureName),
                    missingflag3 = string.IsNullOrWhiteSpace(jBlock.MeshEmissionTextureName),
                    missingflags = missingflag1 && missingflag2 && missingflag3;

                string DupeCheck = "M:" + jBlock.MeshMaterialName + ";A:" + jBlock.MeshTextureName + ";G:" + jBlock.MeshGlossTextureName + ";E:" + jBlock.MeshEmissionTextureName;
                if (!missingflags && HashMAGE.TryGetValue(DupeCheck, out Material localcustomMat))
                {
                    L("Get Cached Material (" + DupeCheck + ")", l);
                    localmat = localcustomMat;
                }
                else
                {
                    if (!string.IsNullOrEmpty(jBlock.MeshMaterialName))
                    {
                        L("Get Material", l);
                        string matName = jBlock.MeshMaterialName.Replace("Venture_", "VEN_")
                                                                .Replace("GeoCorp_", "GC_");
                        try
                        {
                            localmat = GameObjectJSON.GetObjectFromGameResources<Material>(MaterialT, matName);
                            if (localmat == null) Console.WriteLine(matName + " is not a valid Game Material!", l);
                        }
                        catch { Console.WriteLine(jBlock.MeshMaterialName + " is not a valid Game Material!"); }
                    }
                    if (localmat == null)
                        localmat = GameObjectJSON.MaterialFromShader(Color.white);

                    if (!missingflags)
                    {
                        L("Texture Material", l);
                        localmat = GameObjectJSON.SetTexturesToMaterial(true, localmat,
                            missingflag1 ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, jBlock.MeshTextureName),
                            missingflag2 ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, jBlock.MeshGlossTextureName),
                            missingflag3 ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, jBlock.MeshEmissionTextureName));
                        HashMAGE.Add(DupeCheck, localmat);
                    }
                }

                L("Get Collision Material", l);
                /*Local*/
                PhysicMaterial localphysmat = new PhysicMaterial();
                if (jBlock.Friction.HasValue)
                {
                    localphysmat.dynamicFriction = jBlock.Friction.Value;
                }
                if (jBlock.StaticFriction.HasValue)
                {
                    localphysmat.staticFriction = jBlock.StaticFriction.Value;
                }
                if (jBlock.Bounciness.HasValue)
                {
                    localphysmat.bounciness = jBlock.Bounciness.Value;
                }

                //Get Mesh
                Mesh mesh = null;
                if (!string.IsNullOrEmpty(jBlock.MeshName))
                {
                    L("Get Mesh", l);
                    mesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, jBlock.MeshName);
                }

                //Get Collider
                Mesh colliderMesh = null;
                if (!string.IsNullOrEmpty(jBlock.ColliderMeshName))
                {
                    L("Get Collider", l);
                    colliderMesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, jBlock.ColliderMeshName);
                }

                //Set Mesh
                if (colliderMesh == null)
                {
                    if (mesh != null)
                    {
                        L("Set Mesh" + (jBlock.SupressBoxColliderFallback ? "" : " and Auto Collider"), l);
                        blockbuilder.SetModel(mesh, !jBlock.SupressBoxColliderFallback, localmat, localphysmat);
                    }
                }
                else
                {
                    L("Set Mesh and Collider", l);
                    blockbuilder.SetModel(mesh, colliderMesh, true, localmat, localphysmat);
                }

                if (jBlock.SubObjects != null && jBlock.SubObjects.Length != 0)
                {
                    L("Set Sub Bodies", l);
                    var tr = blockbuilder.Prefab.transform;

                    foreach (var sub in jBlock.SubObjects)
                    {
                        string LocalPath;

                        L("-Get GameObject", l);
                        Transform childT = string.IsNullOrEmpty(sub.SubOverrideName) ? null : (tr.RecursiveFindWithProperties(sub.SubOverrideName) as Component)?.transform;
                        bool New = childT == null;
                        GameObject childG = null;
                        if (New)
                        {
                            string name = (string.IsNullOrEmpty(sub.SubOverrideName) ? "SubObject_" + (tr.childCount + 1).ToString() : sub.SubOverrideName);
                            L("-New GameObject " + name, l);
                            LocalPath = "/" + name;
                            childG = new GameObject(name);
                            childT = childG.transform;
                            childT.parent = tr;
                            childT.localPosition = Vector3.zero;
                            childT.localRotation = Quaternion.identity;
                            if (sub.Layer.HasValue)
                            {
                                childG.layer = sub.Layer.Value;
                            }
                            else
                            {
                                childG.layer = 8;//Globals.inst.layerTank;
                            }
                            New = true;
                        }
                        else
                        {
                            L("-Existing GameObject " + sub.SubOverrideName, l);
                            childG = childT.gameObject;
                            if (sub.Layer.HasValue)
                            {
                                childG.layer = sub.Layer.Value;
                            }
                        }

                        if (sub.SubPosition.HasValue)
                        {
                            L("-Offset Position", l);
                            childT.localPosition = sub.SubPosition.Value;
                        }
                        if (sub.SubRotation.HasValue)
                        {
                            L("-Offset Rotation", l);
                            childT.localRotation = Quaternion.Euler(sub.SubRotation.Value);
                        }

                        //-DestroyCollidersOnObj
                        if (sub.DestroyExistingColliders)
                        {
                            L("-Destroy Colliders", l);
                            foreach (var collider in childG.GetComponents<Collider>())
                            {
                                Component.DestroyImmediate(collider);
                            }
                        }

                        //-DestroyRendersOnObj
                        if (sub.DestroyExistingRenderer)
                        {
                            L("-Destroy Renderers", l);
                            foreach (var comp1 in childG.GetComponents<Renderer>())
                            {
                                Component.DestroyImmediate(comp1);
                            }
                            foreach (var comp2 in childG.GetComponents<MeshFilter>())
                            {
                                Component.DestroyImmediate(comp2);
                            }
                        }

                        //-Get Mesh
                        Mesh submesh = null;
                        if (!string.IsNullOrEmpty(sub.MeshName))
                        {
                            L("-Get Mesh", l);
                            submesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, sub.MeshName);
                        }

                        //-Get Collider
                        Mesh subcolliderMesh = null;
                        if (!string.IsNullOrEmpty(sub.ColliderMeshName))
                        {
                            L("-Get Collider", l);
                            subcolliderMesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, sub.ColliderMeshName);
                        }

                        //-Get Material
                        Material mat = localmat;

                        if (!New && !sub.DestroyExistingRenderer)
                        {
                            var ren = childG.GetComponent<Renderer>();
                            if (ren)
                                mat = ren.sharedMaterial;
                        }

                        bool smissingflag1 = string.IsNullOrWhiteSpace(sub.MeshTextureName),
                            smissingflag2 = string.IsNullOrWhiteSpace(sub.MeshGlossTextureName),
                            smissingflag3 = string.IsNullOrWhiteSpace(sub.MeshEmissionTextureName),
                            smissingflags = smissingflag1 && smissingflag2 && smissingflag3;

                        string SubDupeCheck = 
                            "M:" + (sub.MeshMaterialName??jBlock.MeshMaterialName) + 
                            ";A:" + sub.MeshTextureName + 
                            ";G:" + sub.MeshGlossTextureName + 
                            ";E:" + sub.MeshEmissionTextureName;

                        if (!smissingflags && HashMAGE.TryGetValue(SubDupeCheck, out Material customMat))
                        {
                            L("-Get Cached Material (" + SubDupeCheck + ")", l);
                            mat = customMat;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(sub.MeshMaterialName))
                            {
                                L("-Get Material", l);
                                string matName = sub.MeshMaterialName.Replace("Venture_", "VEN_")
                                                                     .Replace("GeoCorp_", "GC_");
                                try
                                {
                                    var mat2 = GameObjectJSON.GetObjectFromGameResources<Material>(MaterialT, matName);
                                    if (mat2 == null) Console.WriteLine(matName + " is not a valid Game Material!", l);
                                    else mat = mat2;
                                }
                                catch { Console.WriteLine(sub.MeshMaterialName + " is not a valid Game Material!"); }
                            }

                            if (!smissingflags)
                            {
                                L("-Texture Material", l);
                                mat = GameObjectJSON.SetTexturesToMaterial(true, mat,
                                    smissingflag1 ? null : GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, sub.MeshTextureName),
                                    smissingflag2 ? null : GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, sub.MeshGlossTextureName),
                                    smissingflag3 ? null : GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, sub.MeshEmissionTextureName));
                                HashMAGE.Add(SubDupeCheck, mat);
                            }
                        }

                        PhysicMaterial physmat = localphysmat;
                        if (sub.MakeBoxCollider || sub.MakeSphereCollider || !string.IsNullOrWhiteSpace(sub.ColliderMeshName))
                        {
                            L("-Get Collision Material", l);
                            bool newphysmat = false;
                            if (sub.Friction.HasValue && sub.Friction.Value != localphysmat.dynamicFriction)
                            {
                                if (!newphysmat) { physmat = CopyPhysicMaterial(localphysmat); newphysmat = true; }
                                physmat.dynamicFriction = sub.Friction.Value;
                            }
                            if (sub.StaticFriction.HasValue && sub.StaticFriction.Value != localphysmat.staticFriction)
                            {
                                if (!newphysmat) { physmat = CopyPhysicMaterial(localphysmat); newphysmat = true; }
                                physmat.staticFriction = sub.StaticFriction.Value;
                            }
                            if (sub.Bounciness.HasValue && sub.Bounciness.Value != localphysmat.bounciness)
                            {
                                if (!newphysmat) { physmat = CopyPhysicMaterial(localphysmat); newphysmat = true; }
                                physmat.bounciness = sub.Bounciness.Value;
                            }
                        }

                        //-Apply
                        if (submesh != null)
                        {
                            L("-Set Mesh", l);
                            if (New) childG.AddComponent<MeshFilter>().sharedMesh = submesh;
                            else childG.EnsureComponent<MeshFilter>().sharedMesh = submesh;
                            childG.EnsureComponent<MeshRenderer>().sharedMaterial = mat;
                        }
                        else
                        {
                            var renderers = childG.GetComponents<Renderer>();
                            if (renderers.Length != 0)
                            {
                                L("-Set Material", l);
                                foreach (var renderer in renderers)
                                {
                                    renderer.sharedMaterial = mat;
                                    if (renderer is ParticleSystemRenderer psrenderer)
                                        psrenderer.trailMaterial = mat;
                                }
                                if (sub.ForceEmission)
                                {
                                    L("-Set Emission packet", l);
                                    foreach (var renderer in renderers)
                                    {
                                        MaterialSwapper.SetMaterialPropertiesOnRenderer(renderer, ManTechMaterialSwap.MaterialColour.Normal, 1f, 0);
                                    }
                                }
                            }
                        }

                        if (subcolliderMesh != null)
                        {
                            L("-Set Collider Mesh", l);
                            MeshCollider mc;
                            if (New) mc = childG.AddComponent<MeshCollider>();
                            else mc = childG.EnsureComponent<MeshCollider>();
                            mc.convex = true;
                            mc.sharedMesh = subcolliderMesh;
                            mc.sharedMaterial = physmat;
                        }
                        if (sub.MakeBoxCollider)
                        {
                            if (submesh != null)
                            {
                                L("-Set Collider Box from Mesh", l);
                                submesh.RecalculateBounds();
                                var bc = childG.EnsureComponent<BoxCollider>();
                                bc.size = submesh.bounds.size - Vector3.one * 0.2f;
                                bc.center = submesh.bounds.center;
                                bc.sharedMaterial = physmat;
                            }
                            else
                            {
                                L("-Set Collider Box", l);
                                var bc = childG.EnsureComponent<BoxCollider>();
                                bc.size = Vector3.one;
                                bc.center = Vector3.zero;
                                bc.sharedMaterial = physmat;
                            }
                        }
                        if (sub.MakeSphereCollider)
                        {
                            L("-Set Collider Sphere", l);
                            var bc = childG.EnsureComponent<SphereCollider>();
                            bc.radius = 0.5f;
                            bc.center = Vector3.zero;
                            bc.sharedMaterial = physmat;
                        }
                        //-Set Size
                        if (sub.SubScale.HasValue && sub.SubScale != Vector3.zero)
                        {
                            L("-Set Size", l);
                            childT.localScale = sub.SubScale.Value;
                        }
                    }
                }

                L("Set Name", l);
                blockbuilder.SetName(jBlock.Name);

                L("Set Description", l);
                blockbuilder.SetDescription(jBlock.Description);

                //Set Cells
                if (jBlock.CellMap != null && jBlock.CellMap.Length != 0)
                {
                    L("Set Cell Map", l);
                    blockbuilder.SetSizeFromStringMap(jBlock.CellMap);
                }
                else if (jBlock.Cells != null && jBlock.Cells.Length != 0)
                {
                    L("Set Cells Manual", l);
                    blockbuilder.SetSizeManual(jBlock.Cells, true);
                }
                else if (jBlock.BlockExtents.HasValue)
                {
                    L("Set Cells Extents", l);
                    blockbuilder.SetSize(jBlock.BlockExtents.Value, (jBlock.APsOnlyAtBottom ? BlockPrefabBuilder.AttachmentPoints.Bottom : BlockPrefabBuilder.AttachmentPoints.All));
                }

                //Set APs
                if (jBlock.APs != null)
                {
                    L("Set APs", l);
                    blockbuilder.SetAPsManual(jBlock.APs);
                }

                if(jBlock.RotationGroup != "")
                {
                    blockbuilder.SetRotationGroupName(jBlock.RotationGroup);
                }

                //Set Mass
                L("Set Mass", l);
                if (jBlock.Mass != 0f)
                {
                    blockbuilder.SetMass(jBlock.Mass);
                }
                else
                {
                    blockbuilder.SetMass(1f);
                }

                //Set Center of Mass
                if (jBlock.CenterOfMass.HasValue)
                {
                    L("Set Center of Mass", l);
                    blockbuilder.SetCenterOfMass(jBlock.CenterOfMass.Value);
                }

                //Recipe
                /*Local*/int RecipePrice = 0;
                if (!BlockAlreadyExists && jBlock.Recipe != null)
                {
                    L("Apply Recipe", l);
                    Dictionary<int, int> RecipeBuilder = new Dictionary<int, int>();
                    if (jBlock.Recipe is JValue rString)
                    {
                        string[] recipe = rString.ToObject<string>().Replace(" ", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string item in recipe)
                        {
                            RecipePrice += AppendToRecipe(RecipeBuilder, item, 1);
                        }
                    }
                    else if (jBlock.Recipe is JObject rObject)
                    {
                        foreach (var item in rObject)
                        {
                            RecipePrice += AppendToRecipe(RecipeBuilder, item.Key, item.Value.ToObject<int>());
                        }
                    }
                    else if (jBlock.Recipe is JArray rArray)
                    {
                        foreach (var item in rArray)
                        {
                            RecipePrice += AppendToRecipe(RecipeBuilder, item.ToString(), 1);
                        }
                    }

                    var Input = new CustomRecipe.RecipeInput[RecipeBuilder.Count];
                    int ite = 0;
                    foreach (var pair in RecipeBuilder)
                    {
                        Input[ite] = new CustomRecipe.RecipeInput(pair.Key, pair.Value);
                        ite++;
                    }

                    string fab = "gsofab";
                    if (!string.IsNullOrEmpty(jBlock.RecipeTable))
                        fab = jBlock.RecipeTable;
                    else
                        fab = CustomRecipe.FabricatorFromFactionType((FactionSubTypes)jBlock.Faction);
                    blockbuilder.SetCustomRecipeTable(fab);
                    blockbuilder.SetRecipe(Input);
                    //CustomRecipe.RegisterRecipe(Input, new CustomRecipe.RecipeOutput[1] {
                    //            new CustomRecipe.RecipeOutput(blockbuilder.RuntimeID)
                    //        }, RecipeTable.Recipe.OutputType.Items, fab);
                }

                L("Set Price", l);
                if (jBlock.Price != 0)
                {
                    blockbuilder.SetPrice(jBlock.Price);
                }
                else if (RecipePrice > 0)
                {
                    blockbuilder.SetPrice(RecipePrice);
                }
                else
                {
                    blockbuilder.SetPrice(500);
                }

                // REGISTER
                if (BlockAlreadyExists)
                {
                    L("Override Register", l);
                    blockbuilder.OverlapExistingRegister();
                }
                else
                {
                    L("Register", l);
                    blockbuilder.RegisterLater(0f);
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("Could not read block " + Json.Name + "\n at " + Json.FullName + "\n\n" + E);
                BlockLoader.Timer.Log($" ! Could not read #{Json.Name} - \"{E.Message}\"");
            }
        }

        static int AppendToRecipe(Dictionary<int, int> RecipeBuilder, string Type, int Count)
        {
            if (!Enum.TryParse(Type, true, out ChunkTypes chunk))
            {
                if (int.TryParse(Type, out int result))
                {
                    chunk = (ChunkTypes)result;
                }
            }
            if (chunk != ChunkTypes.Null)
            {
                if (!RecipeBuilder.ContainsKey((int)chunk))
                {
                    RecipeBuilder.Add((int)chunk, Count);
                }
                else
                {
                    RecipeBuilder[(int)chunk] += Count;
                }
                return RecipeManager.inst.GetChunkPrice(chunk);
            }
            else
            {
                Console.WriteLine("No ChunkTypes found matching given name, nor could parse as ID (int): " + Type);
            }
            return 0;
        }

        private static PhysicMaterial CopyPhysicMaterial(PhysicMaterial original)
        {
            return new PhysicMaterial() { dynamicFriction = original.dynamicFriction, bounciness = original.bounciness, staticFriction = original.staticFriction };
        }
        private static string GetPath(this Transform transform, Transform targetParent = null)
        {
            if (transform == targetParent) return "";
            string result = transform.name;
            Transform parent = transform.parent;
            while(!(parent == targetParent || parent == null))
            {
                result = parent.name + "/" + result;
                parent = parent.parent;
            }
            return result;
        }

        private static void RotateChildren(this Transform transform, Vector3 Rotation)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform Child = transform.GetChild(i);
                Child.Rotate(Rotation, Space.Self);
                Child.localPosition = Quaternion.Euler(Rotation) * Child.localPosition;
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