﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nuterra.BlockInjector
{
    public static class BlockLoader
    {
        internal class Timer : MonoBehaviour
        {
            public static void Log(string NewLine)
            {
                if (!HasExited)
                    blocks.Add(NewLine);
            }

            public static void AddToLast(string Append)
            {
                if (!HasExited)
                    blocks[blocks.Count - 1] += Append;
            }

            public static void ReplaceLast(string NewLine)
            {
                if (!HasExited)
                    blocks[blocks.Count - 1] = NewLine;
            }
            static bool HasExited = false;
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
                        blocks.Clear();
                        UnityEngine.GameObject.Destroy(this.gameObject);
                        ((BlockRotationTable)m_BlockRotationTable.GetValue(ManTechBuilder.inst)).InitRuntime();
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

        internal class JsonCorpCoroutine : MonoBehaviour
        {
            IEnumerator<object> coroutine;
            internal static bool RunningCoroutine = false;

            void Update()
            {
                if (RunningCoroutine)
                {
                    do
                    {
                        RunningCoroutine = coroutine.MoveNext();
                    }
                    while (RunningCoroutine);
                }
            }

            public void BeginCoroutine(bool LoadResources, bool LoadCorps)
            {
                RunningCoroutine = true;
                coroutine = DirectoryCorpLoader.LoadCorps(LoadResources, LoadCorps);
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
                    if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.AltGr)) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.B))
                    {
                        BeginCoroutine(true, true);
                    }
                }
                else
                {
                    var temp = RunningCoroutine;
                    do
                    {
                        RunningCoroutine = coroutine.MoveNext();
                        if (!RunningCoroutine)
                        {
                            if (RunLoadBlocksRightAfter)
                            {
                                BeginCoroutine(false, true);
                                RunLoadBlocksRightAfter = false;
                            }
                            else
                            {
                                LockLinear = 0;
                                AcceptOverwrite = true;
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

        public static bool AcceptOverwrite { get; private set; }

        public static readonly Dictionary<int, CustomCorporation> CustomCorps = new Dictionary<int, CustomCorporation>();
        public static readonly Dictionary<int, CustomBlock> CustomBlocks = new Dictionary<int, CustomBlock>();
        public static readonly Dictionary<int, CustomChunk> CustomChunks = new Dictionary<int, CustomChunk>();
        //public static readonly Dictionary<string, int> NameIDToRuntimeIDTable = new Dictionary<string, int>();
        //public static string GetNameIDFromRuntimeID(int runtimeID) => CustomBlocks[runtimeID].BlockID;

        public static bool Register(CustomBlock block)
        {
            try
            {
                int blockID = block.BlockID, runtimeID = block.RuntimeID;
                bool AlreadyExists = CustomBlocks.ContainsKey(blockID); //NameIDToRuntimeIDTable.ContainsKey(blockID);
                bool Overwriting = AcceptOverwrite && AlreadyExists;
                Console.WriteLine($"Registering block: #{blockID} '{block.Name}'"); 
                //Console.WriteLine($"Registering block: {block.GetType()} #({block.RuntimeID}){block.BlockID} '{block.Name}'");
                Timer.Log($" - #{runtimeID} - \"{block.Name}\""); 
                ManSpawn spawnManager = ManSpawn.inst;
                if (!Overwriting)
                {
                    if (AlreadyExists)
                    {
                        Timer.AddToLast(" - FAILED: Custom Block already exists!");
                        Console.WriteLine("Registering block failed: A block with the same ID already exists \n" + blockID);
                        return false;
                    }

                    bool BlockExists = spawnManager.IsValidBlockToSpawn((BlockTypes)runtimeID);
                    if (BlockExists)
                    {
                        Timer.AddToLast(" - ID already exists");
                        Console.WriteLine("Registering block incomplete: A block with the same ID already exists");
                        return false;
                    }
                    CustomBlocks.Add(runtimeID, block);
                    //NameIDToRuntimeIDTable.Add(block.BlockID, block.RuntimeID);
                }
                else
                {
                    CustomBlocks[runtimeID] = block;
                    //UnpermitSpriteGeneration.Remove(runtimeID);
                }

                int hashCode = ItemTypeInfo.GetHashCode(ObjectTypes.Block, runtimeID);
                spawnManager.VisibleTypeInfo.SetDescriptor<FactionSubTypes>(hashCode, block.Faction);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockCategories>(hashCode, block.Category);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockRarity>(hashCode, block.Rarity);
                spawnManager.VisibleTypeInfo.SetDescriptor<ModulePlatformRestrictions.PlatformAvailability>(hashCode, (ModulePlatformRestrictions.PlatformAvailability)(-1));
                try
                {
                    var rotationTable = (BlockRotationTable)m_BlockRotationTable.GetValue(ManTechBuilder.inst);

                    if (Overwriting)
                    {
                        var prefabs = (BlockPrefabs.GetValue(ManSpawn.inst) as Dictionary<int, Transform>);
                        var previous = ManSpawn.inst.GetBlockPrefab((BlockTypes)runtimeID);

                        DepoolItems.Invoke(ComponentPool.inst, new object[] { LookupPool.Invoke(ComponentPool.inst, new object[] { previous }), int.MaxValue });
                        GameObject.Destroy(previous.gameObject);
                        prefabs[runtimeID] = block.Prefab.transform;

                        if (!string.IsNullOrEmpty(block.RotationGroupName))
                        {
                            var pair = rotationTable.m_BlockRotationGroupIndex.Find(e => e.blockType == runtimeID);
                            pair.groupName = block.RotationGroupName;
                        }
                    }
                    else
                    {
                        ManSpawn.inst.AddBlockToDictionary(block.Prefab);

                        (LoadedActiveBlocks.GetValue(ManSpawn.inst) as List<BlockTypes>).Add((BlockTypes)runtimeID);

                        if (!string.IsNullOrEmpty(block.RotationGroupName))
                        {
                            rotationTable.m_BlockRotationGroupIndex.Add(new BlockRotationTable.GroupIndexLookup
                            {
                                blockType = runtimeID,
                                groupName = block.RotationGroupName
                            });
                        }
                    }

                    var m_BlockPriceLookup = BlockPriceLookup.GetValue(RecipeManager.inst) as Dictionary<int, int>;
                    if (m_BlockPriceLookup.ContainsKey(runtimeID)) m_BlockPriceLookup[runtimeID] = block.Price;
                    else m_BlockPriceLookup.Add(runtimeID, block.Price);

                    try
                    {
                        PrePool.Invoke(block.Prefab.GetComponent<TankBlock>(), null);
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine(E);
                    }

                    return true;
                }
                catch (Exception E)
                {
                    Console.WriteLine(E);
                    if (E.InnerException != null)
                    {
                        Timer.AddToLast(" - FAILED: " + E.InnerException.Message);
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
                Console.WriteLine(E);
                if (E.InnerException != null)
                {
                    Timer.AddToLast(" - FAILED: " + E.InnerException.Message);
                }
                else
                {
                    Timer.AddToLast(" - FAILED: " + E.Message);
                }
                return false;
            }
        }

        public static bool Register(CustomCorporation corp)
        {
            try
            {
                int corpID = corp.CorpID;

                Console.WriteLine($"Registering corp: {corp.GetType()} #{corp.CorpID} '{corp.Name}'");
                Timer.Log($" - #{corpID} - \"{corp.Name}\"");
                ManSpawn spawnManager = ManSpawn.inst;

                if (CustomCorps.ContainsKey(corpID))
                {
                    Timer.AddToLast(" - FAILED: Custom Corp already exists!");
                    Console.WriteLine("Registering corp failed: A corp with the same ID already exists");
                    return false;
                }

                CustomCorps.Add(corpID, corp);
                return true;
            }
            catch (Exception E)
            {
                Console.WriteLine(E);
                if (E.InnerException != null)
                {
                    Timer.AddToLast(" - FAILED: " + E.InnerException.Message);
                }
                else
                {
                    Timer.AddToLast(" - FAILED: " + E.Message);
                }
                return false;
            }
        }

        const BindingFlags binding = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
        //static readonly FieldInfo LoadedBlocks = typeof(ManSpawn).GetField("m_LoadedBlocks", binding);
        static readonly FieldInfo LoadedActiveBlocks = typeof(ManSpawn).GetField("m_LoadedActiveBlocks", binding);
        //static readonly MethodInfo AddBlockToDictionary = typeof(ManSpawn).GetMethod("AddBlockToDictionary", binding);
        static readonly FieldInfo BlockPrefabs = typeof(ManSpawn).GetField("m_BlockPrefabs", binding);
        static readonly FieldInfo BlockPriceLookup = typeof(RecipeManager).GetField("m_BlockPriceLookup", binding);
        static readonly FieldInfo TankPrefab = typeof(ManSpawn).GetField("m_TankPrefab", binding);
        static readonly FieldInfo RuntimeTankPrefabs = typeof(ManSpawn).GetField("m_TankRuntimePrefabs", binding);
        static readonly FieldInfo RuntimePrefabTank = typeof(ManSpawn).GetNestedType("PrefabPair", binding).GetField("stdPrefab", binding);
        static readonly MethodInfo LookupPool = typeof(ComponentPool).GetMethod("LookupPool", binding);
        static readonly MethodInfo DepoolItems = typeof(ComponentPool).GetMethod("DepoolItems", binding);
        static readonly MethodInfo PrePool = typeof(TankBlock).GetMethod("PrePool", binding);

        //private static GameObject _techPrefab;
        //public static GameObject TechPrefab
        //{
        //    get
        //    {
        //        if (_techPrefab == null)
        //            techPrefab = (TankPrefab.GetValue(ManSpawn.inst) as Transform).gameObject;
        //            _techPrefab = (RuntimePrefabTank.GetValue(RuntimeTankPrefabs.GetValue(ManSpawn.inst)) as Transform).gameObject;
        //        return _techPrefab;
        //    }
        //}

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
            PostStartEvent?.Invoke();
        }

        private static List<Type> techComponentsToAdd = new List<Type>();
        public static void AddTechComponentToPrefab<TC>() where TC : TechComponent
        {
            if (Ready) throw new Exception("AddTechComponentToPrefab: Method is being called too late!");
            techComponentsToAdd.Add(typeof(TC));
        }
        public static void AddTechComponentToPrefab(Type techComponent)
        {
            if (Ready) throw new Exception("AddTechComponentToPrefab: Method is being called too late!");
            techComponentsToAdd.Add(techComponent);
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
            if (Input.GetKey(KeyCode.T)) CapInjectedID++; // Debug test, offset everything by 1
            var harmony = new Harmony("nuterra.block.injector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            #region Tech Prefab Patches
            AddTechComponentToPrefab<TechPhysicsReset>();
            #endregion

            #region 1.4.0.1+ Patches
            Patches.OfficialBlocks.Patch(harmony);
            #endregion

            #region Miscellaneous Patches
            try
            {
                harmony.Patch(typeof(Projectile).GetMethod("PrePool", BindingFlags.NonPublic | BindingFlags.Instance), null, null, transpiler: new HarmonyMethod(typeof(Patches.Projectile_UnlockColliderQuantity).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                //harmony.Patch(typeof(ModuleItemConsume).GetMethod("OnPool", BindingFlags.NonPublic | BindingFlags.Instance), null, null, transpiler: new HarmonyMethod(typeof(Patches.ModuleItemConsume_CrashWrapper).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                
                //harmony.Patch(typeof(ModuleItemConsume).GetMethod("InitRecipeOutput", BindingFlags.NonPublic | BindingFlags.Instance), null, null, transpiler: new HarmonyMethod(typeof(Patches.ModuleItemConsume_UnlockDeliveryBlockerRange).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                //Console.WriteLine("Patched range of Delivery Blocker");
            }
            catch(Exception E)
            {
                Console.WriteLine(E);
            }
            #endregion

            T_ManCustomSkins.GetMethod("Awake", binding).Invoke(ManCustomSkins.inst, Array.Empty<object>());

            new GameObject().AddComponent<Timer>();
            BlockExamples.Load();
            PostStartEvent += NetHandler.Patches.INIT;

            var loader = new GameObject();
            /*var jsoncorploader = loader.AddComponent<JsonCorpCoroutine>();
            jsoncorploader.BeginCoroutine(true, true);*/

            var jsonblockloader = loader.AddComponent<JsonBlockCoroutine>();
            jsonblockloader.BeginCoroutine(true, false);
            PostStartEvent += delegate { jsonblockloader.BeginCoroutine(false, true); };
            PostStartEvent += SubscribeGameModeStart;
        }

        private static void SubscribeGameModeStart() => Singleton.Manager<ManGameMode>.inst.ModeStartEvent.Subscribe(OnGameModeStart);

        private static void OnGameModeStart(Mode mode)
        {
            foreach (var pair in CustomBlocks)
                ManLicenses.inst.DiscoverBlock((BlockTypes)pair.Key);
        }


        internal static Type T_BlockUnlockTable = typeof(BlockUnlockTable),
            CorpBlockData = T_BlockUnlockTable.GetNestedType("CorpBlockData", binding),
            GradeData = T_BlockUnlockTable.GetNestedType("GradeData", binding),
            
            T_BlockRewardPoolTable = typeof(BlockRewardPoolTable),
            CorpRewardTiers = T_BlockRewardPoolTable.GetNestedType("CorpRewardTiers", binding),
            TierRewardPool = T_BlockRewardPoolTable.GetNestedType("TierRewardPool", binding),
            
            T_SpriteFetcher = typeof(SpriteFetcher),
            T_UICorpLicense = typeof(UICorpLicense),
            T_ManCustomSkins = typeof(ManCustomSkins),
            T_UICorpToggle = typeof(UICorpToggle),
            T_ManPurchases = typeof(ManPurchases);
        internal static FieldInfo m_CorpBlockList = T_BlockUnlockTable.GetField("m_CorpBlockList", binding),
            m_CorpBlockLevelLookup = T_BlockUnlockTable.GetField("m_CorpBlockLevelLookup", binding),
            m_BlockList = GradeData.GetField("m_BlockList", binding),
            m_AdditionalUnlocks = GradeData.GetField("m_AdditionalUnlocks", binding),
            m_GradeList = CorpBlockData.GetField("m_GradeList", binding),

            m_CorpRewardPools = T_BlockRewardPoolTable.GetField("m_CorpRewardPools", binding),
            CorpRewardTiers_m_Corp = CorpRewardTiers.GetField("m_Corp", binding),
            m_Tiers = CorpRewardTiers.GetField("m_Tiers", binding),
            m_RewardPool = TierRewardPool.GetField("m_RewardPool", binding),

            m_CorpIcons = T_SpriteFetcher.GetField("m_CorpIcons", binding),
            m_SelectedCorpIcons = T_SpriteFetcher.GetField("m_SelectedCorpIcons", binding),
            m_ModernCorpIcons = T_SpriteFetcher.GetField("m_ModernCorpIcons", binding),
            m_LevelTitleStringID = T_UICorpLicense.GetField("m_LevelTitleStringID", binding),
            m_SkinInfos = T_ManCustomSkins.GetField("m_SkinInfos", binding),
            m_CorpSkinSelections = T_ManCustomSkins.GetField("m_CorpSkinSelections", binding),
            m_SkinTextures = T_ManCustomSkins.GetField("m_SkinTextures", binding),
            m_SkinUIInfos = T_ManCustomSkins.GetField("m_SkinUIInfos", binding),
            m_SkinMeshes = T_ManCustomSkins.GetField("m_SkinMeshes", binding),
            m_SkinIndexToIDMapping = T_ManCustomSkins.GetField("m_SkinIndexToIDMapping", binding),
            m_SkinIDToIndexMapping = T_ManCustomSkins.GetField("m_SkinIDToIndexMapping", binding),
            m_Corp = T_UICorpToggle.GetField("m_Corp", binding),
            m_Icon = T_UICorpToggle.GetField("m_Icon", binding),
            m_SelectedIcon = T_UICorpToggle.GetField("m_SelectedIcon", binding),
            m_TooltipComponent = T_UICorpToggle.GetField("m_TooltipComponent", binding),
            inst = T_ManPurchases.GetField("inst", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy),

            m_BlockRotationTable = typeof(ManTechBuilder).GetField("m_BlockRotationTable", binding);

        //static FactionSubTypes last = FactionSubTypes.BF;
        internal static readonly int CapVanillaID = EnumNamesIterator<BlockTypes>.Names.Length;
        internal static int CapInjectedID = CapVanillaID;

        internal static int GetNextAvailableID() => ++CapInjectedID;

        internal const string NameIDProtocolStart = "_C_BLOCK:";

        internal class Patches
        {
            public static readonly int BASE_ID = 1000000;
            public static readonly int NEW_BASE_ID = BASE_ID; //100000000;


            [HarmonyPatch(typeof(RecipeTable.Recipe.ItemSpec), "GetHashCode")]
            private static class CraftingPatch_FixHashOptimization
            {
                public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    var codes = new List<CodeInstruction>(instructions);
                    FixHashOptimization(ref codes);
                    Console.WriteLine("Injected RecipeTable.Recipe.ItemSpec.GetHashCode()");
                    return codes;
                }

                private static void FixHashOptimization(ref List<CodeInstruction> codes)
                {
                    for (int i = 0; i < codes.Count; i++)
                        if (codes[i].opcode == OpCodes.Callvirt)
                        {
                            codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, 65535));
                            codes.Insert(i + 2, new CodeInstruction(OpCodes.And));
                            return;
                        }
                }
            }

#if false
            // Rename all block GameObjects to _C_BLOCK:<ID>
            // BlockSpec.block is grabbed from the GameObject name
            static void NameToRuntimeIDProtocol(ref TankPreset.BlockSpec b)
            {
                //Console.Write(b.block + "," + b.m_BlockType + ";");
                if (b.block.StartsWith(NameIDProtocolStart))
                {
                    string nameID = b.block.Substring(NameIDProtocolStart.Length);
                    if (BlockLoader.NameIDToRuntimeIDTable.TryGetValue(nameID, out int runtimeID))
                    {
                        Console.WriteLine("Found custom block " + nameID + "(" + runtimeID + ") <= (" + b.m_BlockType + ")!");
                        b.m_BlockType = (BlockTypes)runtimeID;
                    }
                    else
                    {
                        Console.WriteLine("Could not find custom block " + nameID + "(" + b.m_BlockType + ")!");
                    }
                }
                else if ((int)b.m_BlockType >= CapVanillaID || (int)b.m_BlockType < 0)
                {
                    //Console.WriteLine("IRREGULARITY DETECTED");
                    Console.WriteLine("HERESY DETECTED: " + b.m_BlockType);
                    Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
                }
                //Console.WriteLine(b.block + "," + b.m_BlockType + ";");
            }

            // Patches:
            //TankPreset.BlockSpec.NetDeserialize
            [HarmonyPatch(typeof(TankPreset.BlockSpec), "NetDeserialize")]
            static class BlockSpec_NetDeserialize
            {
                static void Postfix(ref TankPreset.BlockSpec __instance)
                {
                    NameToRuntimeIDProtocol(ref __instance);
                }
            }

            //TankPreset.BlockSpec.OnDeserialize
            [HarmonyPatch(typeof(TankPreset.BlockSpec), "OnDeserialize")]
            static class BlockSpec_OnDeserialize
            {
                static void Postfix(ref TankPreset.BlockSpec __instance)
                {
                    if ((int)__instance.m_BlockType >= CapVanillaID || (int)__instance.m_BlockType < 0)
                    {
                        //Console.WriteLine("IRREGULARITY DETECTED");
                        Console.WriteLine(__instance.m_BlockType);
                        try
                        {
                            Console.WriteLine(__instance.block);
                        }
                        catch
                        {
                            Console.WriteLine("Oop, block name is literally uncomprehendable");
                        }
                    }
                    NameToRuntimeIDProtocol(ref __instance);
                }
            }

            [HarmonyPatch(typeof(TechData), "UpdateFromDeprecatedBounds")]
            static class TechData_OnDeserialize
            {
                static void Postfix(ref TechData __instance)
                {
                    var specs = __instance.m_BlockSpecs;
                    for (int i = 0; i < specs.Count; i++)
                    {
                        var item = specs[i];
                        NameToRuntimeIDProtocol(ref item);
                        specs[i] = item;
                    }
                }
            }

            [HarmonyPatch(typeof(InventoryJsonConverter), "WriteJson")]
            static class InventoryJsonConverter_WriteJson
            {
                static bool Prefix(JsonWriter writer, object value)
                {
                    IInventory<BlockTypes> inventory = (IInventory<BlockTypes>)value;
                    if (inventory != null)
                    {
                        Dictionary<string, int> moddedBlocks = new Dictionary<string, int>();

                        writer.WriteStartObject();
                        // Standard inventory populator
                        writer.WritePropertyName("m_InventoryList");
                        writer.WriteStartArray();
                        foreach (KeyValuePair<BlockTypes, int> keyValuePair in inventory)
                        {
                            if ((int)keyValuePair.Key >= CapVanillaID)
                            {
                                moddedBlocks.Add(GetNameIDFromRuntimeID((int)keyValuePair.Key), keyValuePair.Value);
                                continue;
                            }
                            writer.WriteStartObject();
                            writer.WritePropertyName("m_BlockType");
                            writer.WriteValue((int)keyValuePair.Key);
                            writer.WritePropertyName("m_Quantity");
                            writer.WriteValue(keyValuePair.Value);
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                        // Special inventory populator
                        writer.WritePropertyName("m_InventoryListInjected");
                        writer.WriteStartArray();
                        foreach (KeyValuePair<string, int> keyValuePair in moddedBlocks)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("m_BlockType");
                            writer.WriteValue(keyValuePair.Key);
                            writer.WritePropertyName("m_Quantity");
                            writer.WriteValue(keyValuePair.Value);
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                        // End
                        writer.WriteEndObject();
                    }
                    return false; // Do not run the original method (Override the method)
                }
            }

            [HarmonyPatch(typeof(InventoryJsonConverter), "ReadJson")]
            static class InventoryJsonConverter_ReadJson
            {
                static bool Prefix(JsonReader reader, object existingValue, out object __result)
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        IInventory<BlockTypes> inventory = (IInventory<BlockTypes>)existingValue;
                        inventory.Clear();
                        JObject jobject = JObject.Load(reader);
                        foreach (JToken jtoken in jobject["m_InventoryList"]) // Standard inventory loader
                        {
                            JObject item = (JObject)jtoken;
                            inventory.SetBlockCount(
                                item["m_BlockType"].ToObject<BlockTypes>(), 
                                item["m_Quantity"].ToObject<int>());
                        }
                        foreach (JToken jtoken in jobject["m_InventoryListInjected"]) // Special inventory loader
                        {
                            JObject item = (JObject)jtoken;
                            string name = item["m_BlockType"].ToObject<string>();
                            if (NameIDToRuntimeIDTable.TryGetValue(name, out int injectedBlock))
                            {
                                int count = item["m_Quantity"].ToObject<int>();
                                inventory.SetBlockCount((BlockTypes)injectedBlock, count);
                            }
                            else
                            {
                                Console.WriteLine("InventoryJsonConverter.ReadJson Exception: Item (NameID: " + name + ") not found in current session table!");
                            }
                        }
                        __result = inventory;
                    }
                    else __result = null;
                    return false; // Do not run the original method (Override the method)
                }
            }
#endif

            [HarmonyPatch(typeof(ManSpawn), "Start")]
            private static class TankPrefabPatch
            {
                private static void Prefix(ref ManSpawn __instance)
                {
                    try
                    {
                        var techPrefab = (TankPrefab.GetValue(__instance) as Transform).gameObject;
                        foreach (Type tc in techComponentsToAdd) Console.WriteLine("Added " + (techPrefab.AddComponent(tc) ? tc.Name : "NOTHING") + " to TechPrefab");
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine("TankPrefabPatch FAILED: " + E);
                    }
                }
            }

            static Type BTT = typeof(BlockTypes);

            [HarmonyPatch(typeof(ManSpawn), "IsBlockAvailableOnPlatform")]
            private static class TableFix
            {
                private static void Postfix(ref bool __result, BlockTypes blockType)
                {
                    if (!__result && !Enum.IsDefined(BTT, blockType)) __result = true;
                }
            }

            [HarmonyPatch(typeof(BlockFilterTable), "CheckBlockAllowed")]
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

                private static void Postfix(ref string __result, int itemType, LocalisationEnums.StringBanks stringBank, string defaultString)
                {
                    if ((stringBank == LocalisationEnums.StringBanks.BlockNames || stringBank == LocalisationEnums.StringBanks.BlockDescription) && itemType >= NEW_BASE_ID) return;

                    if (__result == defaultString)
                    {
                        __result = $"MissingNo.{itemType} <{stringBank}>";
                    }
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

            [HarmonyPatch(typeof(ModuleLight), "EnableLights")]
            private static class OverrideEmission
            {
                private static void Postfix(ref ModuleLight __instance)
                {
                    ModuleCustomBlock cb = __instance.GetComponent<ModuleCustomBlock>();
                    if (cb != null && cb.EmissionMode != BlockPrefabBuilder.EmissionMode.None)
                    {
                        cb.Invoke("UpdateEmission", 1.5f);
                    }
                }
            }

 
            internal static class OfficialBlocks
            {
                public static void Patch(Harmony harmony)
                {
                    var GetBlockType = typeof(TankPreset.BlockSpec).GetMethod("GetBlockType");
                    if (GetBlockType != null)
                    {
                        harmony.Patch(GetBlockType, null, null, transpiler: new HarmonyMethod(typeof(BlockSpec_GetBlockType).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                        Console.WriteLine("Patched GetBlockType");
                    }

                    var RemoveCustomBlockRecipes = typeof(RecipeManager).GetMethod("RemoveCustomBlockRecipes");
                    if (RemoveCustomBlockRecipes != null)
                    {
                        harmony.Patch(RemoveCustomBlockRecipes, null, null, transpiler: new HarmonyMethod(typeof(RecipeManager_RemoveCustomBlockRecipes).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                        Console.WriteLine("Patched RemoveCustomBlockRecipes");
                    }

                    // Ambiguous match resolution: Try to get the specific method
                    /*var AutoAssignIDs = typeof(ManMods).GetMethod("AutoAssignIDs", new Type[] { typeof(ModSessionInfo), typeof(List<string>), typeof(List<string>), typeof(List<string>) });
                    if (AutoAssignIDs != null)
                    {
                        harmony.Patch(AutoAssignIDs, null, null, transpiler: new HarmonyMethod(typeof(ManMods_AutoAssignIDs).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                        Console.WriteLine("Patched AutoAssignIDs");
                    }
                    else
                    {
                        // Stable solution: Get the available method
                        AutoAssignIDs = typeof(ManMods).GetMethod("AutoAssignIDs");
                        if (AutoAssignIDs != null)
                        {
                            harmony.Patch(AutoAssignIDs, null, null, transpiler: new HarmonyMethod(typeof(ManMods_AutoAssignIDs).GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)));
                            Console.WriteLine("Patched AutoAssignIDs");
                        }
                    }*/
                }

                [HarmonyPatch(typeof(BlockUnlockTable), "RemoveModdedBlocks")]
                private static class BlockUnlockTable_RemoveModdedBlocks
                {
                    static bool Prefix(/*ref BlockUnlockTable __instance*/)
                    {
                        Console.WriteLine("PREFIX RemoveModdedBlocks");

                        if (CheckVersion("1.4.3.2", SKU.DisplayVersion) >= 0)
                        {
                            UnstableSupport_RemoveWhenStable.BlockUnlockTable_RemoveModdedBlocks_Prefix();
                        }
                        else
                        {
                            var corpBlockList = m_CorpBlockList.GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array;

                            foreach (var a in corpBlockList)
                            {
                                var gradeList = m_GradeList.GetValue(a) as Array;
                                foreach (var b in gradeList)
                                {
                                    var blockList = (m_BlockList.GetValue(b) as BlockUnlockTable.UnlockData[])
                                        .Where(ud => (int)ud.m_BlockType < NEW_BASE_ID).ToArray();
                                    m_BlockList.SetValue(b, blockList);
                                }
                            }
                        }
                        return false;
                    }
                }

                [HarmonyPatch(typeof(UIItemDisplay), "Setup", new Type[] { typeof(ItemTypeInfo), typeof(Color), typeof(Color), typeof(string), typeof(Color), typeof(bool), typeof(bool) })]
                private static class UIItemDisplay_Setup
                {
                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = instructions.ToList();
                        ChangeCheck(codes);
                        return codes;
                    }
                }

                [HarmonyPatch(typeof(StringLookup), "GetItemName", new Type[] { typeof(ObjectTypes), typeof(int) })]
                private static class StringLookup_GetItemName
                {
                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = instructions.ToList();
                        ChangeCheck(codes);
                        return codes;
                    }
                }

                [HarmonyPatch(typeof(StringLookup), "GetItemDescription", new Type[] { typeof(ObjectTypes), typeof(int) })]
                private static class StringLookup_GetItemDescription
                {
                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = instructions.ToList();
                        ChangeCheck(codes);
                        return codes;
                    }
                }

                [HarmonyPatch(typeof(ManMods), "AutoAssignIDs", new Type[] { typeof(Dictionary<int, string>), typeof(List<string>), typeof(int), typeof(int) })]
                private static class ManMods_AutoAssignIDs
                {
                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = instructions.ToList();
                        ChangeCheck(codes);
                        return codes;
                    }
                }

                static class BlockSpec_GetBlockType
                {
                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = instructions.ToList();
                        ChangeCheck(codes);
                        return codes;
                    }
                }

                static class RecipeManager_RemoveCustomBlockRecipes
                {
                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = instructions.ToList();
                        ChangeCheck(codes);
                        return codes;
                    }
                }

                static void ChangeCheck(List<CodeInstruction> codes)
                {
                    var check = codes.FirstOrDefault(ci => ci.operand is int i && i == BASE_ID);
                    if (check != null && check != default(CodeInstruction)) check.operand = NEW_BASE_ID;
                }
            }

            internal static class ModuleItemConsume_UnlockDeliveryBlockerRange
            {
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    var codes = instructions.ToList();
                    var check = codes.FirstOrDefault(ci => ci.opcode == OpCodes.Ldc_R4 && (int)ci.operand == 23);
                    if (check != null && check != default(CodeInstruction)) check.operand = 512;
                    return codes;
                }
            }

            internal static class ModuleItemConsume_CrashWrapper
            {
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    var codes = instructions.ToList();
                    //var check = codes.FirstOrDefault(ci => ci.opcode == OpCodes.Ldc_R4 && (int)ci.operand == 23);
                    //if (check != null && check != default(CodeInstruction)) check.operand = 512;
                    return codes;
                }
            }

            internal static class Projectile_UnlockColliderQuantity
            {
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    var codes = instructions.ToList();
                    //for (int i = 0; i < codes.Count; i++)
                    //    Console.WriteLine($">{i} {codes[i]}");
                    int stfld = codes.FindIndex(ci => ci.opcode == OpCodes.Stfld);
                    if (stfld != -1)
                    {
                        // load arg 0
                        // load arg 0
                        // call get_gameObject
                        codes.RemoveRange(3, stfld - 3); // Keeps the first 3 ILcodes
                        codes.Insert(3, new CodeInstruction(OpCodes.Call, typeof(Projectile_UnlockColliderQuantity).GetMethod("Projectile_GetCollider", BindingFlags.Public | BindingFlags.Static)));
                    }
                    Console.WriteLine($"Projectile_UnlockColliderQuantity: Transpiling removed {stfld - 3} IL lines, added 1");
                    //for (int i = 0; i < codes.Count; i++)
                    //    Console.WriteLine($">{i} {codes[i]}");
                    return codes;
                }

                public static Collider Projectile_GetCollider(GameObject go)
                {
                    foreach (var c in go.EnumerateHierarchy())
                    {
                        var col = c.GetComponent<Collider>();
                        if (col != null && !col.isTrigger) return col;
                    }
                    return null;
                }
            }
        }
           
        public static int CheckVersion(string baseVersion, string currentCheck)
        {
            var cArr = currentCheck.Split('.');
            var tArr = baseVersion.Split('.');
            int max = Math.Max(cArr.Length, tArr.Length);
            for (int i = 0; i < max; i++)
            {
                int currVal = 0, baseVal = 0;
                if (cArr.Length > i) currVal = int.Parse(cArr[i]);
                if (tArr.Length > i) baseVal = int.Parse(tArr[i]);

                if (currVal == baseVal) continue;
                if (currVal > baseVal) return 1;
                return -1;
            }
            return 0;
        }

        internal static void FixBlockUnlockTable(CustomBlock block)
        {
            try
            {
                ManLicenses.inst.DiscoverBlock((BlockTypes)block.RuntimeID);
                if (CheckVersion("1.4.3.2", SKU.DisplayVersion) >= 0)
                {
                    UnstableSupport_RemoveWhenStable.FixBlockUnlockTable(block);
                }
                else
                {
                    Array blockList = m_CorpBlockList.GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array;
                    object corpData = blockList.GetValue((int)block.Faction);
                    BlockUnlockTable.UnlockData[] unlocked = m_BlockList.GetValue((m_GradeList.GetValue(corpData) as Array).GetValue(block.Grade)) as BlockUnlockTable.UnlockData[];
                    Array.Resize(ref unlocked, unlocked.Length + 1);
                    unlocked[unlocked.Length - 1] = new BlockUnlockTable.UnlockData
                    {
                        m_BlockType = (BlockTypes)block.RuntimeID,
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
                        .Add((BlockTypes)block.RuntimeID, block.Grade);
                    ManLicenses.inst.DiscoverBlock((BlockTypes)block.RuntimeID);
                }
            }
            catch (Exception E)
            {
                Timer.AddToLast(" - FAILED: Could not add to block table. Could it be the grade level?");
                Console.WriteLine("Registering block failed: Could not add to block table. " + E);
            }

            if (block.DropFromCrates)
            {
                try
                {
                    Array corpRewardPools = m_CorpRewardPools.GetValue(ManLicenses.inst.GetRewardPoolTable()) as Array;
                    foreach (var corpRewardPool in corpRewardPools)
                    {
                        if ((FactionSubTypes)CorpRewardTiers_m_Corp.GetValue(corpRewardPool) == block.Faction)
                        {
                            var corpRewardTiers = m_Tiers.GetValue(corpRewardPool) as Array;
                            object corpRewardTier = corpRewardTiers.GetValue(block.Grade);
                            BlockRewardPoolTable.RewardBlockInfo[] rewardPool = m_RewardPool.GetValue(corpRewardTier) as BlockRewardPoolTable.RewardBlockInfo[];

                            Array.Resize(ref rewardPool, rewardPool.Length + 1);
                            rewardPool[rewardPool.Length - 1] = new BlockRewardPoolTable.RewardBlockInfo()
                            {
                                m_BlockType = (BlockTypes)block.RuntimeID,
                                m_PrerequisiteBlocks = Array.Empty<BlockTypes>()
                            };

                            m_RewardPool.SetValue(corpRewardTier, rewardPool);
                        }
                    }
                }
                catch (Exception E)
                {
                    Timer.AddToLast(" - FAILED: Could not add to block reward table.");
                    Console.WriteLine("Registering block failed: Could not add to block reward table. " + E);
                }

                if(block.PairedBlock != -1)
                {
                    var blockID = (BlockTypes)block.RuntimeID;
                    var pairedID = (BlockTypes)block.PairedBlock;
                    if (!Globals.inst.m_BlockPairsList.m_BlockPairs.Any(bp => bp.m_Block == blockID || bp.m_Block == pairedID))
                    {
                        var arr = Globals.inst.m_BlockPairsList.m_BlockPairs;
                        Array.Resize(ref arr, arr.Length + 1);
                        arr[arr.Length - 1] = new BlockPairsList.BlockPairs()
                        {
                            m_Block = blockID,
                            m_PairedBlock = pairedID
                        };
                        Globals.inst.m_BlockPairsList.m_BlockPairs = arr;
                    }
                }
            }
        }

        //static int lastFrameRendered;
        //static List<int> UnpermitSpriteGeneration = new List<int>();
        private static MethodInfo RenderSnapshotFromTechDataInternal = typeof(ManScreenshot).GetMethod("RenderSnapshotFromTechDataInternal", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool ResourceLookup_OnSpriteLookup(ObjectTypes objectType, int itemType, ref UnityEngine.Sprite result)
        {
            if (objectType == ObjectTypes.Block)
            {
                if (CustomBlocks.TryGetValue(itemType, out CustomBlock block))
                {
                    result = block.DisplaySprite;
                    //if (result == null && !UnpermitSpriteGeneration.Contains(itemType)) // Create a sprite right now
                    //{
                    //    /*try
                    //    {
                    //        //lastFrameRendered = Time.frameCount;
                    //        var b = new TankPreset.BlockSpec() { block = block.Name, m_BlockType = (BlockTypes)block.RuntimeID, m_SkinID = 0, m_VisibleID = -1, orthoRotation = 0, position = IntVector3.zero, saveState = new Dictionary<int, Module.SerialData>(), textSerialData = new List<string>() };
                    //        Texture2D image = RenderSnapshotFromTechDataInternal.Invoke(ManScreenshot.inst, new object[] { new TechData() { m_BlockSpecs = new List<TankPreset.BlockSpec> { b } }, new IntVector2(256, 256) }) as Texture2D; // Devs why did you make this private

                    //        //float x = image.height / (float)image.width;
                    //        //float x = 1f;
                    //        result = GameObjectJSON.SpriteFromImage(image);//GameObjectJSON.CropImage(image, new Rect((1f - x) * 0.5f, 0f, x, 1f)));
                    //    }
                    //    catch { UnpermitSpriteGeneration.Add(itemType); }*/
                    //    block.DisplaySprite = result;
                    //}
                    return result != null;
                }
            }
            else if (objectType == ObjectTypes.Chunk)
            {
                if (CustomBlocks.TryGetValue(itemType, out CustomBlock block))
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
                        return !string.IsNullOrEmpty(Result);
                    }
                    break;

                case LocalisationEnums.StringBanks.BlockDescription:
                    if (CustomBlocks.TryGetValue(EnumValue, out block))
                    {
                        Result = block.Description;
                        return !string.IsNullOrEmpty(Result);
                    }
                    break;
                case LocalisationEnums.StringBanks.ChunkName:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Name;
                        return !string.IsNullOrEmpty(Result);
                    }
                    break;

                case LocalisationEnums.StringBanks.ChunkDescription:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Description;
                        return !string.IsNullOrEmpty(Result);
                    }
                    break;

                case LocalisationEnums.StringBanks.Corporations:
                    if (CustomCorps.TryGetValue(EnumValue, out CustomCorporation corp))
                    {
                        Result = corp.Name;
                        return !string.IsNullOrEmpty(Result);
                    }
                    break;
            }
            return false;
        }
    }
}