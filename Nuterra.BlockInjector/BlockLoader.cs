using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    public static class BlockLoader
    {
        internal class Timer : MonoBehaviour
        {
            public static void Log(string NewLine)
            {
                blocks.Add(NewLine);
            }

            public static void AddToLast(string Append)
            {
                blocks[blocks.Count - 1] += Append;
            }

            public static void ReplaceLast(string NewLine)
            {
                blocks[blocks.Count - 1] = NewLine;
            }
            bool HasExited = false;
            internal static List<string> blocks = new List<string> { "Loaded Blocks:" };
            internal float scroll = 0f, scrollVel = 0f;
            void OnGUI()
            {
                HasExited |= Singleton.Manager<ManSplashScreen>.inst.HasExited;
                if (HasExited)
                {
                    if (JsonBlockCoroutine.RunningCoroutine)
                    {
                        GUI.Label(new Rect(4, 4, Screen.width, 25), "Bulk-loading remaining blocks! Please hold...", GUI.skin.label);
                        JsonBlockCoroutine.LockLinear += 1;
                    }
                    else
                    {
                        UnityEngine.GameObject.Destroy(this.gameObject);
                    }
                }
                else if (blocks.Count > 1)
                {
                    float height = GUI.skin.label.lineHeight;
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        GUI.Label(new Rect(0, scroll + height * (i + 1), Screen.width, 25), blocks[i], GUI.skin.label);
                    }
                    if (height * (blocks.Count + 1) + scroll > Screen.height)
                    {
                        scrollVel *= 0.9f;
                        scrollVel -= 1f;
                    }
                    else
                    {
                        scrollVel *= 0.7f;
                    }
                    scroll += scrollVel;
                }
            }

            void Start()
            {
                Singleton.DoOnceAfterStart(Wait);
            }
            void Wait()
            {
                Invoke("Doit", 3f);
            }
            void Doit()
            {
                Console.WriteLine("The block injector is ready!");
                MakeReady();
            }
        }

        internal class JsonBlockCoroutine : MonoBehaviour
        {
            internal static byte LockLinear;
            IEnumerator<object> coroutine;
            internal static bool RunningCoroutine = false;
            bool RunLoadBlocksRightAfter = false;

            void Update()
            {
                if (!RunningCoroutine)
                {
                    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.B))
                    {
                        BeginCoroutine(true, true);
                    }
                }
                else
                {
                    do
                    {
                        RunningCoroutine = coroutine.MoveNext();
                        if (!RunningCoroutine)
                        {
                            AcceptOverwrite = true;
                            if (RunLoadBlocksRightAfter)
                            {
                                BeginCoroutine(false, true);
                                RunLoadBlocksRightAfter = false;
                            }
                            else
                            {
                                LockLinear = 0;
                            }
                        }
                    }
                    while (LockLinear > 0 && RunningCoroutine);
                }
            }

            public void BeginCoroutine(bool LoadResources, bool LoadBlocks)
            {
                if (RunningCoroutine && LoadBlocks)
                {
                    RunLoadBlocksRightAfter = true;
                    return;
                }
                RunningCoroutine = true;
                coroutine = DirectoryBlockLoader.LoadBlocks(LoadResources, LoadBlocks);
                //new System.Threading.Tasks.Task(delegate
                //{
                //    DirectoryBlockLoader.LoadBlockResources();
                //    LoadedResources = true;
                //}).Start();
            }
        }

        public static bool AcceptOverwrite;

        public static readonly Dictionary<int, CustomBlock> CustomBlocks = new Dictionary<int, CustomBlock>();
        public static readonly Dictionary<int, CustomChunk> CustomChunks = new Dictionary<int, CustomChunk>();

        public static bool Register(CustomBlock block)
        {
            try
            {
                int blockID = block.BlockID;
                bool Overwriting = AcceptOverwrite && CustomBlocks.ContainsKey(blockID);
                Console.WriteLine($"Registering block: {block.GetType()} #{block.BlockID} '{block.Name}'");
                Timer.Log($" - #{blockID} - \"{block.Name}\"");
                ManSpawn spawnManager = ManSpawn.inst;
                if (!Overwriting)
                {
                    if (CustomBlocks.ContainsKey(blockID))
                    {
                        Timer.AddToLast(" - FAILED: Custom Block already exists!");
                        Console.WriteLine("Registering block failed: A block with the same ID already exists");
                        return false;
                    }
                    bool BlockExists = spawnManager.IsValidBlockToSpawn((BlockTypes)blockID);
                    if (BlockExists)
                    {
                        Timer.AddToLast(" - ID already exists");
                        Console.WriteLine("Registering block incomplete: A block with the same ID already exists");
                        return false;
                    }
                    CustomBlocks.Add(blockID, block);
                }
                else
                {
                    CustomBlocks[blockID] = block;
                }

                int hashCode = ItemTypeInfo.GetHashCode(ObjectTypes.Block, blockID);
                spawnManager.VisibleTypeInfo.SetDescriptor<FactionSubTypes>(hashCode, block.Faction);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockCategories>(hashCode, block.Category);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockRarity>(hashCode, block.Rarity);
                try
                {
                    if (Overwriting)
                    {
                        var prefabs = (BlockPrefabs.GetValue(ManSpawn.inst) as Dictionary<int, Transform>);
                        var previous = ManSpawn.inst.GetBlockPrefab((BlockTypes)blockID);

                        DepoolItems.Invoke(ComponentPool.inst, new object[] { LookupPool.Invoke(ComponentPool.inst, new object[] { previous }), int.MaxValue });
                        GameObject.Destroy(previous.gameObject);
                        prefabs[blockID] = block.Prefab.transform;

                    }
                    else
                    {
                        AddBlockToDictionary.Invoke(spawnManager, new object[] { block.Prefab });
                        (LoadedBlocks.GetValue(ManSpawn.inst) as List<BlockTypes>).Add((BlockTypes)block.BlockID);
                        (LoadedActiveBlocks.GetValue(ManSpawn.inst) as List<BlockTypes>).Add((BlockTypes)block.BlockID);
                    }

                    var m_BlockPriceLookup = BlockPriceLookup.GetValue(RecipeManager.inst) as Dictionary<int, int>;
                    if (m_BlockPriceLookup.ContainsKey(blockID)) m_BlockPriceLookup[blockID] = block.Price;
                    else m_BlockPriceLookup.Add(blockID, block.Price);

                    try
                    {
                        PrePool.Invoke(block.Prefab.GetComponent<TankBlock>(), null);
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine(E);
                        if (E.InnerException != null)
                        {
                            Console.WriteLine(E.InnerException);
                        }
                    }

                    return true;
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message + "\n" + E.StackTrace + "\n" + E.InnerException?.Message);
                    if (E.InnerException != null)
                    {
                        Timer.AddToLast(" - FAILED: " + E.InnerException?.Message);
                    }
                    else
                    {
                        Timer.AddToLast(" - FAILED: " + E.Message);
                    }
                    return false;
                }
            }
            catch (Exception E)
            {
                Console.WriteLine(E.Message + "\n" + E.StackTrace + "\n" + E.InnerException?.Message);
                if (E.InnerException != null)
                {
                    Timer.AddToLast(" - FAILED: " + E.InnerException?.Message);
                }
                else
                {
                    Timer.AddToLast(" - FAILED: " + E.Message);
                }
                return false;
            }
        }
        const BindingFlags binding = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo LoadedBlocks = typeof(ManSpawn).GetField("m_LoadedBlocks", binding);
        static readonly FieldInfo LoadedActiveBlocks = typeof(ManSpawn).GetField("m_LoadedActiveBlocks", binding);
        static readonly MethodInfo AddBlockToDictionary = typeof(ManSpawn).GetMethod("AddBlockToDictionary", binding);
        static readonly FieldInfo BlockPrefabs = typeof(ManSpawn).GetField("m_BlockPrefabs", binding);
        static readonly FieldInfo BlockPriceLookup = typeof(RecipeManager).GetField("m_BlockPriceLookup", binding);
        static readonly MethodInfo LookupPool = typeof(ComponentPool).GetMethod("LookupPool", binding);
        static readonly MethodInfo DepoolItems = typeof(ComponentPool).GetMethod("DepoolItems", binding);
        static readonly MethodInfo PrePool = typeof(TankBlock).GetMethod("PrePool", binding);

        private static bool Ready = false;
        private static event Action PostStartEvent;

        public static void DelayAfterSingleton(Action ActionToDelay)
        {
            if (Ready)
            {
                ActionToDelay();
            }
            else
            {
                PostStartEvent += ActionToDelay;
            }
        }

        internal static void MakeReady()
        {
            Ready = true;
            if (PostStartEvent != null)
            {
                PostStartEvent();
            }
        }

        public static void Register(CustomChunk chunk)
        {
            Console.WriteLine($"Registering chunk: {chunk.GetType()} #{chunk.ChunkID} '{chunk.Name}'");
            CustomChunks.Add(chunk.ChunkID, chunk);
            ResourceTable table = ResourceManager.inst.resourceTable;
            ResourceTable.Definition[] definitions = table.resources;
            Array.Resize(ref definitions, definitions.Length);
            definitions[definitions.Length - 1] = new ResourceTable.Definition() { name = chunk.Name, basePrefab = chunk.BasePrefab, frictionDynamic = chunk.FrictionDynamic, frictionStatic = chunk.FrictionStatic, mass = chunk.Mass, m_ChunkType = (ChunkTypes)chunk.ChunkID, restitution = chunk.Restitution, saleValue = chunk.SaleValue };
        }

        public static void PostModsLoaded()
        {
            var harmony = HarmonyInstance.Create("nuterra.block.injector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            new GameObject().AddComponent<Timer>();
            BlockExamples.Load();
            PostStartEvent += NetHandler.Patches.INIT;

            var jsonblockloader = new GameObject().AddComponent<JsonBlockCoroutine>();
            jsonblockloader.BeginCoroutine(true, false);
            PostStartEvent += delegate { jsonblockloader.BeginCoroutine(false, true); };
        }

        internal class Patches
        {
            //[HarmonyPatch(typeof(ManSpawn), "AddBlockToDictionary")]
            //private static class DisallowGameBlockLoading
            //{
            //    private static bool Prefix(GameObject blockPrefab)
            //    {
            //        if (blockPrefab == null) return true;
            //        var Visible = blockPrefab.GetComponent<Visible>();
            //        if (Visible == null || Visible.m_ItemType.ObjectType != ObjectTypes.Block) return true;
            //        if (BlockPrefabBuilder.OverrideValidity.TryGetValue(Visible.m_ItemType.ItemType, out string name) && name != blockPrefab.name) return false;
            //        return true;
            //    }
            //}

            static Type BTT = typeof(BlockTypes);

            [HarmonyPatch(typeof(ManSpawn), "IsBlockAvailableOnPlatform")]
            private static class TableFix
            {
                private static void Postfix(ref bool __result, BlockTypes blockType)
                {
                    if (!__result && !Enum.IsDefined(BTT, blockType)) __result = true;
                }
            }

            [HarmonyPatch(typeof(ModeCoOpCreative), "CheckBlockAllowed")]
            private static class TableFixCoOp
            {
                private static void Postfix(ref bool __result, BlockTypes blockType)
                {
                    if (!__result && !Enum.IsDefined(BTT, blockType)) __result = true;
                }
            }

            [HarmonyPatch(typeof(StringLookup), "GetString")]
            private static class OnStringLookup
            {
                private static bool Prefix(ref string __result, int itemType, LocalisationEnums.StringBanks stringBank)
                {
                    string result = "";
                    if (ResourceLookup_OnStringLookup(stringBank, itemType, ref result))
                    {
                        __result = result;
                        return false;
                    }
                    return true;
                }
            }
            [HarmonyPatch(typeof(SpriteFetcher), "GetSprite", new Type[] { typeof(ObjectTypes), typeof(int) })]
            private static class OnSpriteLookup
            {
                private static bool Prefix(ref UnityEngine.Sprite __result, ObjectTypes objectType, int itemType)
                {
                    UnityEngine.Sprite result = null;
                    if (ResourceLookup_OnSpriteLookup(objectType, itemType, ref result))
                    {
                        __result = result;
                        return false;
                    }
                    return true;
                }
            }
        }

        static Type T_BlockUnlockTable = typeof(BlockUnlockTable),
            CorpBlockData = T_BlockUnlockTable.GetNestedType("CorpBlockData", BindingFlags.Instance | BindingFlags.NonPublic),
            GradeData = T_BlockUnlockTable.GetNestedType("GradeData", BindingFlags.Instance | BindingFlags.NonPublic);
        static FieldInfo m_CorpBlockList = T_BlockUnlockTable.GetField("m_CorpBlockList", BindingFlags.Instance | BindingFlags.NonPublic),
            m_CorpBlockLevelLookup = T_BlockUnlockTable.GetField("m_CorpBlockLevelLookup", BindingFlags.Instance | BindingFlags.NonPublic),
            m_BlockList = GradeData.GetField("m_BlockList"),
            m_GradeList = CorpBlockData.GetField("m_GradeList");

        internal static void FixBlockUnlockTable(CustomBlock block)
        {
            try
            {
                ManLicenses.inst.DiscoverBlock((BlockTypes)block.BlockID);
                Array blockList = m_CorpBlockList.GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array;


                object corpData = blockList.GetValue((int)block.Faction);
                BlockUnlockTable.UnlockData[] unlocked = m_BlockList.GetValue((m_GradeList.GetValue(corpData) as Array).GetValue(block.Grade)) as BlockUnlockTable.UnlockData[];
                Array.Resize(ref unlocked, unlocked.Length + 1);
                unlocked[unlocked.Length - 1] = new BlockUnlockTable.UnlockData
                {
                    m_BlockType = (BlockTypes)block.BlockID,
                    m_BasicBlock = true,
                    m_DontRewardOnLevelUp = true
                };
                m_BlockList
                    .SetValue(
                    (m_GradeList.GetValue(corpData) as Array).GetValue(block.Grade),
                    unlocked);

                ((m_CorpBlockLevelLookup
                    .GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array)
                    .GetValue((int)block.Faction) as Dictionary<BlockTypes, int>)
                    .Add((BlockTypes)block.BlockID, block.Grade);
                ManLicenses.inst.DiscoverBlock((BlockTypes)block.BlockID);
            }
            catch (Exception E)
            {
                Timer.AddToLast(" - FAILED: Could not add to block table. Could it be the grade level?");
                Console.WriteLine("Registering block failed: Could not add to block table. " + E.Message);
            }
        }

        //static int lastFrameRendered;
        static bool PermitSpriteGeneration = true;
        private static bool ResourceLookup_OnSpriteLookup(ObjectTypes objectType, int itemType, ref UnityEngine.Sprite result)
        {
            if (objectType == ObjectTypes.Block)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    if (result == null && PermitSpriteGeneration)// && lastFrameRendered != Time.frameCount)
                    {
                        try
                        {
                            //lastFrameRendered = Time.frameCount;
                            var b = new TankPreset.BlockSpec() { block = block.Name, m_BlockType = (BlockTypes)block.BlockID, m_SkinID = 0, m_VisibleID = -1, orthoRotation = 0, position = IntVector3.zero, saveState = new Dictionary<int, Module.SerialData>(), textSerialData = new List<string>() };
                            var image = ManScreenshot.inst.RenderSnapshotFromTechData(new TechData() { m_BlockSpecs = new List<TankPreset.BlockSpec> { b } }, new IntVector2(256, 256));

                            //float x = image.height / (float)image.width;
                            float x = 1f;
                            result = GameObjectJSON.SpriteFromImage(image);//GameObjectJSON.CropImage(image, new Rect((1f - x) * 0.5f, 0f, x, 1f)));
                        }
                        catch { PermitSpriteGeneration = false; }
                        block.DisplaySprite = result;
                    }
                    return result != null;
                }
            }
            else if (objectType == ObjectTypes.Chunk)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    return result != null;
                }
            }
            return false;
        }

        private static bool ResourceLookup_OnStringLookup(LocalisationEnums.StringBanks StringBank, int EnumValue, ref string Result)
        {
            CustomBlock block;
            CustomChunk chunk;
            switch (StringBank)
            {
                case LocalisationEnums.StringBanks.BlockNames:
                    if (CustomBlocks.TryGetValue(EnumValue, out block))
                    {
                        Result = block.Name;
                        return Result != null && Result != "";
                    }
                    break;

                case LocalisationEnums.StringBanks.BlockDescription:
                    if (CustomBlocks.TryGetValue(EnumValue, out block))
                    {
                        Result = block.Description;
                        return Result != null && Result != "";
                    }
                    break;
                case LocalisationEnums.StringBanks.ChunkName:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Name;
                        return Result != null && Result != "";
                    }
                    break;

                case LocalisationEnums.StringBanks.ChunkDescription:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Description;
                        return Result != null && Result != "";
                    }
                    break;
            }
            return false;
        }
    }
}