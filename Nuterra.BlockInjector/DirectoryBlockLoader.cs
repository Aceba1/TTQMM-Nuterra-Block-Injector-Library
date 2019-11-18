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
    internal static class DirectoryBlockLoader
    {
        internal struct BlockBuilder
        {
            public string Name;
            public string Description;
            public bool KeepReferenceRenderers;
            public bool KeepReferenceColliders;
            public string GamePrefabReference;
            public int ID;
            public string IconName;
            public string MeshName;
            public string ColliderMeshName;
            public bool SupressBoxColliderFallback;
            public float? Friction;
            public float? StaticFriction;
            public float? Bounciness;
            public string MeshTextureName;
            public string MeshGlossTextureName;
            public string MeshEmissionTextureName;
            public string MeshMaterialName;
            public int Faction;
            public int Category;
            public int Grade;
            public int Price;
            public int HP;
            public int? DamageableType;
            public int Rarity;
            public float? Fragility;
            public float Mass;
            public Vector3? CenterOfMass;
            public IntVector3? BlockExtents;
            public bool APsOnlyAtBottom;
            public IntVector3[] Cells;
            public Vector3[] APs;
            public Vector3? ReferenceOffset;
            public Vector3? ReferenceScale;
            public Vector3? ReferenceRotationOffset;
            public string Recipe;
            public SubObj[] SubObjects;

            public JObject JSONBLOCK;

            public struct SubObj
            {
                public string SubOverrideName;
                public string MeshName;
                public int? Layer;
                public bool DestroyExistingColliders;
                public bool MakeBoxCollider;
                public bool MakeSphereCollider;
                public string ColliderMeshName;
                public float? Friction;
                public float? StaticFriction;
                public float? Bounciness;
                public string MeshTextureName;
                public string MeshGlossTextureName;
                public string MeshEmissionTextureName;
                public string MeshMaterialName;
                public Vector3? SubPosition;
                public Vector3? SubScale;
                public Vector3? SubRotation;
                public bool DestroyExistingRenderer;
                //PUT ANIMATION CURVES HERE
                public AnimInfo[] Animations;
                public struct AnimInfo
                {
                    public string ClipName;
                    public Curve[] Curves;

                    public AnimationCurve[] GetAnimationCurves()
                    {
                        var result = new AnimationCurve[Curves.Length];
                        for (int i = 0; i < Curves.Length; i++)
                        {
                            result[i] = Curves[i].ToAnimationCurve();
                        }
                        return result;
                    }

                    public struct Curve
                    {
                        public string ComponentName;
                        public string PropertyName;
                        public Key[] Keys;
                        public AnimationCurve ToAnimationCurve()
                        {
                            var Keyframes = new Keyframe[Keys.Length];
                            for (int i = 0; i < Keys.Length; i++)
                            {
                                Keyframes[i] = Keys[i].ToKeyframe();
                            }
                            return new AnimationCurve(Keyframes);
                        }

                        public struct Key
                        {
                            public float Time;
                            public float Value;
                            public float inTangent;
                            public float outTangent;
                            public Keyframe ToKeyframe()
                            {
                                return new Keyframe(Time, Value, inTangent, outTangent);
                            }
                        }
                    }
                }
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
                    if (!FileChanged.TryGetValue(Png.FullName, out DateTime lastEdit) || lastEdit != Png.LastWriteTime)
                    {
                        try
                        {
                            Texture2D tex = GameObjectJSON.ImageFromFile(Png.FullName);
                            GameObjectJSON.AddObjectToUserResources<Texture2D>(Texture2DT, tex, Png.Name);
                            GameObjectJSON.AddObjectToUserResources<Texture>(TextureT, tex, Png.Name);
                            GameObjectJSON.AddObjectToUserResources<Sprite>(SpriteT, GameObjectJSON.SpriteFromImage(tex), Png.Name);
                            FileChanged[Png.FullName] = Png.LastWriteTime;
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
                BlockLoader.Timer.Log("Loading json models...");
                yield return null;
                foreach (FileInfo Obj in cbObj)
                {
                    if (!FileChanged.TryGetValue(Obj.FullName, out DateTime lastEdit) || lastEdit != Obj.LastWriteTime)
                    {
                        try
                        {
                            GameObjectJSON.AddObjectToUserResources(GameObjectJSON.MeshFromFile(Obj.FullName), Obj.Name);
                            FileChanged[Obj.FullName] = Obj.LastWriteTime;
                            Count++;
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

                L("Read JSON", l);
                /*Local*/bool BlockAlreadyExists = BlockLoader.CustomBlocks.TryGetValue(jBlock.ID, out var ExistingJSONBlock);
                if (BlockAlreadyExists && !BlockLoader.AcceptOverwrite)
                {
                    Console.WriteLine("Could not read block " + Json.Name + "\n at " + Json.FullName + "\n\nBlock ID collides with " + ExistingJSONBlock.Name);
                    BlockLoader.Timer.Log($" ! Could not read #{Json.Name} - Block ID collides with {ExistingJSONBlock.Name}!");
                    return;
                }

                if (string.IsNullOrEmpty(jBlock.GamePrefabReference))
                {
                    L("New instance", l);
                    blockbuilder = new BlockPrefabBuilder();
                }
                else
                {
                    L("Prefab reference", l);
                    if (jBlock.ReferenceOffset.HasValue && jBlock.ReferenceOffset != Vector3.zero)
                    {
                        //Offset Prefab
                        blockbuilder = new BlockPrefabBuilder(jBlock.GamePrefabReference, jBlock.ReferenceOffset.Value, false);
                    }
                    else
                    {
                        blockbuilder = new BlockPrefabBuilder(jBlock.GamePrefabReference, false);
                    }
                    if (!jBlock.KeepReferenceRenderers)
                    {
                        if (!jBlock.KeepReferenceColliders)
                        {
                            blockbuilder.RemoveChildrenWithComponent(true, null, typeof(MeshRenderer), typeof(MeshFilter), typeof(Collider));
                        }
                        else
                        {
                            blockbuilder.RemoveChildrenWithComponent(true, null, typeof(MeshRenderer), typeof(MeshFilter));
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
                }

                //If gameobjectJSON exists, use it
                if (jBlock.JSONBLOCK != null)
                {
                    L("Use JSONBLOCK", l);
                    GameObjectJSON.CreateGameObject(jObject.Property("JSONBLOCK").Value.ToObject<JObject>(), blockbuilder.Prefab);
                }

                L("Set IP", l);
                blockbuilder.SetBlockID(jBlock.ID);

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

                if (jBlock.IconName != null && jBlock.IconName != "")
                {
                    L("Set Icon", l);
                    var Spr = GameObjectJSON.GetObjectFromUserResources<Sprite>(SpriteT, jBlock.IconName);
                    if (Spr == null)
                    {
                        //var Tex = GameObjectJSON.GetObjectFromGameResources<Texture2D>(Texture2DT, jBlock.IconName);
                        //if (Tex == null)
                        //{
                        //    Spr = GameObjectJSON.GetObjectFromGameResources<Sprite>(jBlock.IconName);
                        //    if (Spr == null)
                        //    {
                                blockbuilder.SetIcon((Sprite)null);
                        //    }
                        //    else
                        //    {
                        //        blockbuilder.SetIcon(Spr);
                        //    }
                        //}
                        //else
                        //{
                        //    blockbuilder.SetIcon(Tex);
                        //}
                    }
                    else
                    {
                        blockbuilder.SetIcon(Spr);
                    }
                }

                L("Get Material", l);
                /*Local*/
                Material localmat = null;
                if (jBlock.MeshMaterialName != null && jBlock.MeshMaterialName != "")
                {
                    jBlock.MeshMaterialName.Replace("Venture_", "VEN_");
                    jBlock.MeshMaterialName.Replace("GeoCorp_", "GC_");
                    try
                    {
                        localmat = GameObjectJSON.GetObjectFromGameResources<Material>(MaterialT, jBlock.MeshMaterialName);
                    }
                    catch { Console.WriteLine(jBlock.MeshMaterialName + " is not a valid Game Material!"); }
                }
                if (localmat == null)
                {
                    localmat = GameObjectJSON.MaterialFromShader();
                }

                //-Texture Material
                {
                    bool missingflag1 = string.IsNullOrWhiteSpace(jBlock.MeshTextureName),
                        missingflag2 = string.IsNullOrWhiteSpace(jBlock.MeshGlossTextureName),
                        missingflag3 = string.IsNullOrWhiteSpace(jBlock.MeshEmissionTextureName);
                    if (!missingflag1 || !missingflag2 || !missingflag3)
                    {
                        L("-Texture Material", l);
                        localmat = GameObjectJSON.SetTexturesToMaterial(true, localmat,
                            missingflag1 ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, jBlock.MeshTextureName),
                            missingflag2 ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, jBlock.MeshGlossTextureName),
                            missingflag3 ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, jBlock.MeshEmissionTextureName));
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
                if (jBlock.MeshName != null && jBlock.MeshName != "")
                {
                    L("Get Mesh", l);
                    mesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, jBlock.MeshName);
                }

                //Get Collider
                Mesh colliderMesh = null;
                if (jBlock.ColliderMeshName != null && jBlock.ColliderMeshName != "")
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
                            foreach (var comp1 in childG.GetComponents<MeshRenderer>())
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
                        if (sub.MeshName != null && sub.MeshName != "")
                        {
                            L("-Get Mesh", l);
                            submesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, sub.MeshName);
                        }

                        //-Get Collider
                        Mesh subcolliderMesh = null;
                        if (sub.ColliderMeshName != null && sub.ColliderMeshName != "")
                        {
                            L("-Get Collider", l);
                            subcolliderMesh = GameObjectJSON.GetObjectFromUserResources<Mesh>(MeshT, sub.ColliderMeshName);
                        }

                        //-Get Material
                        Material mat = localmat;
                        if (!New && !sub.DestroyExistingRenderer)
                        {
                            var ren = childG.GetComponent<MeshRenderer>();
                            if (ren != null)
                            {
                                mat = ren.material;
                            }
                        }
                        if (sub.MeshMaterialName != null && sub.MeshMaterialName != "")
                        {
                            L("-Get Material", l);
                            sub.MeshMaterialName.Replace("Venture_", "VEN_");
                            sub.MeshMaterialName.Replace("GeoCorp_", "GC_");
                            try
                            {
                                var mat2 = GameObjectJSON.GetObjectFromGameResources<Material>(MaterialT, sub.MeshMaterialName);
                                if (mat2 == null) Console.WriteLine(sub.MeshMaterialName + " is not a valid Game Material!", l);
                                else mat = mat2;
                            }
                            catch { Console.WriteLine(sub.MeshMaterialName + " is not a valid Game Material!"); }
                        }
                        L("-Texture Material", l);
                        mat = GameObjectJSON.SetTexturesToMaterial(true, mat,
                            string.IsNullOrWhiteSpace(sub.MeshTextureName) ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, sub.MeshTextureName),
                            string.IsNullOrWhiteSpace(sub.MeshGlossTextureName) ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, sub.MeshGlossTextureName),
                            string.IsNullOrWhiteSpace(sub.MeshEmissionTextureName) ? null :
                            GameObjectJSON.GetObjectFromUserResources<Texture2D>(Texture2DT, sub.MeshEmissionTextureName));

                        L("-Get Collision Material", l);
                        PhysicMaterial physmat = localphysmat;
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

                        //-Apply
                        if (submesh != null)
                        {
                            L("-Set Mesh", l);
                            if (New) childG.AddComponent<MeshFilter>().sharedMesh = submesh;
                            else childG.EnsureComponent<MeshFilter>().sharedMesh = submesh;
                            childG.EnsureComponent<MeshRenderer>().material = mat;
                        }
                        else
                        {
                            var renderer = childG.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                L("-Set Material", l);
                                renderer.material = mat;
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
                        //-Animation
                        if (sub.Animations != null)
                        {
                            Console.WriteLine("Animation block detected");
                            var mA = tr.GetComponentsInChildren<ModuleAnimator>(true);
                            if (mA.Length != 0)
                            {
                                var Animator = mA[0];
                                GameObjectJSON.DumpAnimation(Animator);
                                foreach (var anim in sub.Animations)
                                {
                                    GameObjectJSON.ModifyAnimation(Animator.Animator, anim.ClipName, childT.GetPath(Animator.transform), GameObjectJSON.AnimationCurveStruct.ConvertToStructArray(anim.Curves));
                                }
                            }
                        }
                    }
                }

                L("Set Name", l);
                blockbuilder.SetName(jBlock.Name);

                L("Set Description", l);
                blockbuilder.SetDescription(jBlock.Description);

                //Set Cells
                if (jBlock.Cells != null && jBlock.Cells.Length != 0)
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
                if (!BlockAlreadyExists && jBlock.Recipe != null && jBlock.Recipe != "")
                {
                    L("Apply Recipe", l);
                    Dictionary<int, int> RecipeBuilder = new Dictionary<int, int>();
                    string[] recipes = jBlock.Recipe.Replace(" ", "").Split(',');
                    foreach (string recipe in recipes)
                    {
                        ChunkTypes chunk = ChunkTypes.Null;
                        if (!Enum.TryParse(recipe, true, out chunk))
                        {
                            if (int.TryParse(recipe, out int result))
                            {
                                chunk = (ChunkTypes)result;
                            }
                        }
                        if (chunk != ChunkTypes.Null)
                        {
                            if (!RecipeBuilder.ContainsKey((int)chunk))
                            {
                                RecipeBuilder.Add((int)chunk, 1);
                            }
                            else
                            {
                                RecipeBuilder[(int)chunk]++;
                            }
                            RecipePrice += RecipeManager.inst.GetChunkPrice(chunk);
                        }
                        else
                        {
                            Console.WriteLine("No ChunkTypes found matching given name, nor could parse as ID (int): " + recipe);
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
                    switch ((FactionSubTypes)jBlock.Faction)
                    {
                        case FactionSubTypes.GC: fab = "gcfab"; break;
                        case FactionSubTypes.VEN: fab = "venfab"; break;
                        case FactionSubTypes.HE: fab = "hefab"; break;
                        case FactionSubTypes.BF: fab = "bffab"; break;
                    }

                    CustomRecipe.RegisterRecipe(Input, new CustomRecipe.RecipeOutput[1] {
                                new CustomRecipe.RecipeOutput(jBlock.ID)
                            }, RecipeTable.Recipe.OutputType.Items, fab);
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
                    BlockLoader.Register(blockbuilder.Build());
                    blockbuilder.Prefab.SetActive(false);
                }
                else
                {
                    L("Register", l);
                    blockbuilder.RegisterLater(2);
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("Could not read block " + Json.Name + "\n at " + Json.FullName + "\n\n" + E);
                BlockLoader.Timer.Log($" ! Could not read #{Json.Name} - \"{E.Message}\"");
            }
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