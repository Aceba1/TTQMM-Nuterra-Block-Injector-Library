using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
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
                    UnpermitSpriteGeneration.Remove(runtimeID);
                }

                int hashCode = ItemTypeInfo.GetHashCode(ObjectTypes.Block, runtimeID);
                spawnManager.VisibleTypeInfo.SetDescriptor<FactionSubTypes>(hashCode, block.Faction);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockCategories>(hashCode, block.Category);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockRarity>(hashCode, block.Rarity);
                try
                {
                    if (Overwriting)
                    {
                        var prefabs = (BlockPrefabs.GetValue(ManSpawn.inst) as Dictionary<int, Transform>);
                        var previous = ManSpawn.inst.GetBlockPrefab((BlockTypes)runtimeID);

                        DepoolItems.Invoke(ComponentPool.inst, new object[] { LookupPool.Invoke(ComponentPool.inst, new object[] { previous }), int.MaxValue });
                        GameObject.Destroy(previous.gameObject);
                        prefabs[runtimeID] = block.Prefab.transform;

                    }
                    else
                    {
                        ManSpawn.inst.AddBlockToDictionary(block.Prefab);

                        (LoadedActiveBlocks.GetValue(ManSpawn.inst) as List<BlockTypes>).Add((BlockTypes)runtimeID);
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
            var factions = Enum.GetValues(typeof(FactionSubTypes));
            last = (FactionSubTypes)factions.GetValue(factions.Length - 1);

            if (Input.GetKey(KeyCode.T)) CapInjectedID++; // Debug test, offset everything by 1
            var harmony = HarmonyInstance.Create("nuterra.block.injector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            var running = false;
            var enumerator = DirectoryCorpLoader.LoadCorps(true, true);
            do
            {
                running = enumerator.MoveNext();
            } while (running);

            new GameObject().AddComponent<Timer>();
            BlockExamples.Load();
            PostStartEvent += NetHandler.Patches.INIT;

            var loader = new GameObject();
            /*var jsoncorploader = loader.AddComponent<JsonCorpCoroutine>();
            jsoncorploader.BeginCoroutine(true, true);*/

            var jsonblockloader = loader.AddComponent<JsonBlockCoroutine>();
            jsonblockloader.BeginCoroutine(true, false);
            PostStartEvent += delegate { jsonblockloader.BeginCoroutine(false, true); };
        }


        internal static Type T_BlockUnlockTable = typeof(BlockUnlockTable),
            CorpBlockData = T_BlockUnlockTable.GetNestedType("CorpBlockData", binding),
            GradeData = T_BlockUnlockTable.GetNestedType("GradeData", binding),
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
            m_CorpIcons = T_SpriteFetcher.GetField("m_CorpIcons", binding),
            m_SelectedCorpIcons = T_SpriteFetcher.GetField("m_SelectedCorpIcons", binding),
            m_ModernCorpIcons = T_SpriteFetcher.GetField("m_ModernCorpIcons", binding),
            m_LevelTitleStringID = T_UICorpLicense.GetField("m_LevelTitleStringID", binding),
            m_SkinInfos = T_ManCustomSkins.GetField("m_SkinInfos", binding),
            m_CorpSkinSelections = T_ManCustomSkins.GetField("m_CorpSkinSelections", binding),
            m_Corp = T_UICorpToggle.GetField("m_Corp", binding),
            m_Icon = T_UICorpToggle.GetField("m_Icon", binding),
            m_SelectedIcon = T_UICorpToggle.GetField("m_SelectedIcon", binding),
            m_TooltipComponent = T_UICorpToggle.GetField("m_TooltipComponent", binding),
            inst = T_ManPurchases.GetField("inst", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        static FactionSubTypes last = FactionSubTypes.BF;
        internal static readonly int CapVanillaID = EnumNamesIterator<BlockTypes>.Names.Length;
        internal static int CapInjectedID = CapVanillaID;

        internal static int GetNextAvailableID() => ++CapInjectedID;

        internal const string NameIDProtocolStart = "_C_BLOCK:";

        internal class Patches
        {
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

            [HarmonyPatch(typeof(ModuleLight), "EnableLights")]
            private static class OverrideEmission
            {
                private static void Postfix(ref ModuleLight __instance)
                {
                    ModuleCustomBlock cb = __instance.GetComponent<ModuleCustomBlock>();
                    if (cb != null && cb.EmissionMode != BlockPrefabBuilder.EmissionMode.None)
                    {
                        cb.UpdateEmission();
                    }
                }
            }
            
            private static class ManUIPatches
            {
                static Type T_Math = typeof(Math),
                    T_int = typeof(int),
                    T_object = typeof(object),
                    T_FactionSubTypes = typeof(FactionSubTypes),
                    T_Console = typeof(Console);

                [HarmonyPatch(typeof(ManUI), "Start")]
                private static class Start
                {
                    private static void Postfix(ref ManUI __instance)
                    {
                        if (CustomCorps.Count == 0) return;
                        /*var icons = ((Sprite[])m_CorpIcons.GetValue(__instance.m_SpriteFetcher)).ToList();
                        icons.Add(icons[1]);
                        m_CorpIcons.SetValue(__instance.m_SpriteFetcher, icons.ToArray());

                        icons = ((Sprite[])m_SelectedCorpIcons.GetValue(__instance.m_SpriteFetcher)).ToList();
                        icons.Add(icons[1]);
                        m_SelectedCorpIcons.SetValue(__instance.m_SpriteFetcher, icons.ToArray());

                        icons = ((Sprite[])m_ModernCorpIcons.GetValue(__instance.m_SpriteFetcher)).ToList();
                        icons.Add(icons[1]);
                        m_ModernCorpIcons.SetValue(__instance.m_SpriteFetcher, icons.ToArray());*/

                        List<Sprite> corpIcons = ((Sprite[])m_CorpIcons.GetValue(__instance.m_SpriteFetcher)).ToList(),
                            selectedCorpIcons = ((Sprite[])m_SelectedCorpIcons.GetValue(__instance.m_SpriteFetcher)).ToList(),
                            modernCorpIcons = ((Sprite[])m_ModernCorpIcons.GetValue(__instance.m_SpriteFetcher)).ToList();

                        var max = CustomCorps.Keys.Max();
                        corpIcons.Resize(max + 1, null);
                        selectedCorpIcons.Resize(max + 1, null);
                        modernCorpIcons.Resize(max + 1, null);
                        foreach (var cc in CustomCorps)
                        {
                            corpIcons.Insert(cc.Key, cc.Value.CorpIcon ?? corpIcons[(int)FactionSubTypes.GSO]);
                            selectedCorpIcons.Insert(cc.Key, cc.Value.SelectedCorpIcon ?? selectedCorpIcons[(int)FactionSubTypes.GSO]);
                            modernCorpIcons.Insert(cc.Key, cc.Value.ModernCorpIcon ?? modernCorpIcons[(int)FactionSubTypes.GSO]);
                        }

                        m_CorpIcons.SetValue(__instance.m_SpriteFetcher, corpIcons.ToArray());
                        m_SelectedCorpIcons.SetValue(__instance.m_SpriteFetcher, selectedCorpIcons.ToArray());
                        m_ModernCorpIcons.SetValue(__instance.m_SpriteFetcher, modernCorpIcons.ToArray());
                    }
                }

                /*[HarmonyPatch(typeof(ManUI), "GetCorpIcon")]
				private static class GetCorpIcon
				{
					//private static bool Prefix(ref ManUI __instance, ref FactionSubTypes faction, ref Sprite __result)
					//{
					//	Console.WriteLine(faction.ToString());
					//	var icons = m_CorpIcons.GetValue(__instance.m_SpriteFetcher) as Sprite[];
					//	if (faction > FactionSubTypes.BF)
					//	{
					//		__result = icons[(int)FactionSubTypes.GSO];
					//		return false;
					//	}
					//	return true;
					//}

                    private static void Postfix(ref ManUI __instance, ref FactionSubTypes faction)
                    {
                        Console.Write("");
                    }

                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = new List<CodeInstruction>(instructions);
                        codes.Insert(0, new CodeInstruction(OpCodes.Ldc_I4, (int)last));
                        codes.Insert(1, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(2, new CodeInstruction(OpCodes.Call, T_Math.GetMethod("Min", new Type[] { T_int, T_int })));
                        codes.Insert(3, new CodeInstruction(OpCodes.Starg_S, (byte)1));

                        codes.Insert(0, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(1, new CodeInstruction(OpCodes.Box, T_FactionSubTypes));
                        codes.Insert(2, new CodeInstruction(OpCodes.Call, T_Console.GetMethod("WriteLine", new Type[] { T_object })));
                        return codes;
                    }
                }

				[HarmonyPatch(typeof(ManUI), "GetSelectedCorpIcon")]
				private static class GetSelectedCorpIcon
				{

                    //IL_0000: ldarg.1
		            //IL_0001: box       FactionSubTypes
		            //IL_0006: call      void [mscorlib]System.Console::WriteLine(object)


		            //IL_0000: ldc.i4.7
		            //IL_0001: ldarg.1
		            //IL_0002: call      int32 [mscorlib]System.Math::Min(int32, int32)
		            //IL_0007: starg.s   faction


                    //private static bool Prefix(ref ManUI __instance, ref FactionSubTypes faction, ref Sprite __result)
					//{
					//	Console.WriteLine(faction.ToString());
					//	var icons = m_SelectedCorpIcons.GetValue(__instance.m_SpriteFetcher) as Sprite[];
					//	if (faction > FactionSubTypes.BF)
					//	{
					//		__result = icons[(int)FactionSubTypes.GSO];
					//		return false;
					//	}
					//	return true;
					//}


                    private static void Postfix(ref ManUI __instance, ref FactionSubTypes faction)
                    {
                        Console.Write("");
                    }

                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = new List<CodeInstruction>(instructions);
                        codes.Insert(0, new CodeInstruction(OpCodes.Ldc_I4, (int)last));
                        codes.Insert(1, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(2, new CodeInstruction(OpCodes.Call, T_Math.GetMethod("Min", new Type[] { T_int, T_int })));
                        codes.Insert(3, new CodeInstruction(OpCodes.Starg_S, (byte)1));

                        codes.Insert(0, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(1, new CodeInstruction(OpCodes.Box, T_FactionSubTypes));
                        codes.Insert(2, new CodeInstruction(OpCodes.Call, T_Console.GetMethod("WriteLine", new Type[] { T_object })));
                        return codes;
                    }
                }

				[HarmonyPatch(typeof(ManUI), "GetModernCorpIcon")]
				private static class GetModernCorpIcon
				{
					//private static bool Prefix(ref ManUI __instance, ref FactionSubTypes corp, ref Sprite __result)
					//{
                    //  var icons = m_ModernCorpIcons.GetValue(__instance.m_SpriteFetcher) as Sprite[];
					//	if (corp > FactionSubTypes.BF)
					//	{
					//		__result = icons[(int)FactionSubTypes.GSO];
					//		return false;
					//	}
					//	return true;
                    //}

                    private static void Postfix(ref ManUI __instance, ref FactionSubTypes corp)
                    {
                        Console.Write("");
                    }

                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = new List<CodeInstruction>(instructions);
                        codes.Insert(0, new CodeInstruction(OpCodes.Ldc_I4, (int)last));
                        codes.Insert(1, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(2, new CodeInstruction(OpCodes.Call, T_Math.GetMethod("Min", new Type[] { T_int, T_int })));
                        codes.Insert(3, new CodeInstruction(OpCodes.Starg_S, (byte)1));


                        codes.Insert(0, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(1, new CodeInstruction(OpCodes.Box, T_FactionSubTypes));
                        codes.Insert(2, new CodeInstruction(OpCodes.Call, T_Console.GetMethod("WriteLine", new Type[] { T_object })));
                        return codes;
                    }
                }*/
            }

            [HarmonyPatch(typeof(BlockUnlockTable), "Init")]
            private static class BlockUnlockTable_Init
            {
                private static void Prefix(ref BlockUnlockTable __instance)
                {
                    if (CustomCorps.Count == 0) return;
                    var blockList = m_CorpBlockList.GetValue(__instance) as Array;
                    var temp = Array.CreateInstance(CorpBlockData, CustomCorps.Keys.Max() + 1);
                    blockList.CopyTo(temp, 0);
                    blockList = temp;

                    foreach (var cc in CustomCorps)
                    {
                        blockList.SetValue(Activator.CreateInstance(CorpBlockData), cc.Key);

                        var gl = Array.CreateInstance(GradeData, cc.Value.GradesAmount);
                        for (int i = 0; i < gl.Length; i++)
                        {
                            var gd = Activator.CreateInstance(GradeData);
                            m_BlockList.SetValue(gd, new BlockUnlockTable.UnlockData[0]);
                            m_AdditionalUnlocks.SetValue(gd, new BlockTypes[0]);
                            gl.SetValue(gd, i);
                        }

                        m_GradeList.SetValue(blockList.GetValue(cc.Key), gl);
                    }

                    m_CorpBlockList.SetValue(__instance, blockList);

                    //ManCustomSkins.inst.Invoke("Awake", 0);
                    /*var ud = new BlockUnlockTable.UnlockData[] {
                        new BlockUnlockTable.UnlockData()
                        {
                            m_BasicBlock = true,
                            m_BlockType = BlockTypes.GCCockpit_222,
                            m_DontRewardOnLevelUp = true,
                            m_HideOnLevelUpScreen = true
                        },
                        new BlockUnlockTable.UnlockData()
                        {
                            m_BasicBlock = true,
                            m_BlockType = BlockTypes.GCCockpit_224,
                            m_DontRewardOnLevelUp = true,
                            m_HideOnLevelUpScreen = true
                        }
                    };
					var gd = Activator.CreateInstance(GradeData);
					m_BlockList.SetValue(gd, ud);
					m_AdditionalUnlocks.SetValue(gd, new BlockTypes[0]);
					var gl = Array.CreateInstance(GradeData, 1);
					gl.SetValue(gd, 0);
					m_GradeList.SetValue(temp.GetValue(8), gl);
					m_CorpBlockList.SetValue(__instance, temp);*/
                }
            }

            [HarmonyPatch(typeof(ManLicenses), "Start")]
            private static class ManLicenses_Start
            {
                private static void Postfix(ref ManLicenses __instance)
                {
                    /*__instance.m_ThresholdData.Add(new ManLicenses.ThresholdsTableEntry
					{
						faction = (FactionSubTypes)8,
						thresholds = new FactionLicense.Thresholds
						{
							m_MaxSupportedLevel = 1,
							m_XPLevels = new int[] { 10 }
						}
					});*/
                    if (CustomCorps.Count == 0) return;

                    foreach (var cc in CustomCorps)
                    {
                        if (cc.Value.HasLicense)
                        {
                            __instance.m_ThresholdData.Add(new ManLicenses.ThresholdsTableEntry
                            {
                                faction = (FactionSubTypes)cc.Key,
                                thresholds = new FactionLicense.Thresholds
                                {
                                    m_MaxSupportedLevel = cc.Value.GradesAmount,
                                    m_XPLevels = cc.Value.XPLevels
                                }
                            });
                        }
                    }

                    /*Console.WriteLine("\nCorps Levels");
                    foreach (var data in __instance.m_ThresholdData)
                    {
                        Console.WriteLine(data.faction.ToString());
                        Console.WriteLine(data.thresholds.MaxXP);
                        Console.WriteLine(data.thresholds.m_MaxSupportedLevel);
                        foreach (var lvl in data.thresholds.m_XPLevels)
                        {
                            Console.Write(lvl + " ");
                        }
                        Console.WriteLine("\n");
                    }*/
                }
            }

            [HarmonyPatch(typeof(ManPurchases), "Init")]
            private static class ManPurchases_Init
            {
                private static void Prefix(ref ManPurchases __instance)
                {
                    //if (!__instance.AvailableCorporations.Contains((FactionSubTypes)8)) __instance.AvailableCorporations.Add((FactionSubTypes)8);
                    if (CustomCorps.Count == 0) return;
                    foreach (var cc in CustomCorps)
                    {
                        if (!__instance.AvailableCorporations.Contains((FactionSubTypes)cc.Key)) __instance.AvailableCorporations.Add((FactionSubTypes)cc.Key);
                    }
                }
            }

            [HarmonyPatch(typeof(Enum), "GetNames")]
            private static class Enum_GetNames
            {
                private static void Postfix(ref Type enumType, ref string[] __result)
                {
                    if (CustomCorps.Count == 0) return;
                    if (enumType == typeof(FactionSubTypes))
                    {
                        var resList = __result.ToList();
                        foreach (var cc in CustomCorps)
                        {
                            resList.Add(cc.Value.Name);
                        }
                        __result = resList.ToArray();
                    }
                }
            }

            [HarmonyPatch(typeof(Enum), "GetValues")]
            private static class Enum_GetValues
            {
                private static void Postfix(ref Type enumType, ref Array __result)
                {
                    if (CustomCorps.Count == 0) return;
                    if (enumType == typeof(FactionSubTypes))
                    {
                        var resList = ((FactionSubTypes[])__result).ToList();
                        foreach (var cc in CustomCorps)
                        {
                            resList.Add((FactionSubTypes)cc.Value.CorpID);
                        }
                        __result = resList.ToArray();
                    }
                }
            }

            [HarmonyPatch(typeof(UICorpLicense), "Setup")]
            private static class UICorpLicense_Setup
            {
                private static void Postfix(ref UICorpLicense __instance)
                {
                    if (((FactionLicense)T_UICorpLicense.GetField("m_License", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance)).Corporation > FactionSubTypes.BF)
                    {
                        m_LevelTitleStringID.SetValue(__instance, 2);
                        T_UICorpLicense.GetMethod("SetToolTip", binding).Invoke(__instance, new object[] { 64 });
                    }
                }

                /*static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    var codes = new List<CodeInstruction>(instructions);
                    var start = codes.FindLastIndex(ci => ci.opcode == OpCodes.Ldc_I4_0);
                    var end = codes.FindLastIndex(ci => ci.opcode == OpCodes.Blt_S) + 1;
                    //var nop = new CodeInstruction(OpCodes.Nop);
                    for (int i = start; i < end; i++)
                    {
                        codes[i].opcode = OpCodes.Nop;
                    }
                    return codes;
                }*/
            }

            private static class UICorpTogglesPatches
            {
                [HarmonyPatch(typeof(UICorpToggles), "Setup")]
                private static class Setup
                {
                    private static void Prefix(ref UICorpToggles __instance, ref CorporationOrder optionalOrder)
                    {
                        optionalOrder = null;
                        foreach (var cc in CustomCorps)
                        {
                            if (!ManPurchases.inst.AvailableCorporations.Contains((FactionSubTypes)cc.Key)) ManPurchases.inst.AvailableCorporations.Add((FactionSubTypes)cc.Key);
                        }
                    }

                    private static void Postfix(ref UICorpToggles __instance)
                    {
                        try
                        {
                            //GameObject.DestroyImmediate(___m_SpawnedCorpSkinImages[8].GetComponent<UnityEngine.UI.Image>());
                            var images = typeof(UICorpToggles).GetField("m_SpawnedCorpSkinImages", binding).GetValue(__instance) as List<Transform>;
                            //if (images.Count > (int)last + 1) GameObject.DestroyImmediate(images[8].GetComponent<UnityEngine.UI.Image>());

                            if (images.Count > (int)last + 1)
                            {
                                for (int i = (int)last + 1; i < images.Count; i++)
                                {
                                    try
                                    {
                                        GameObject.DestroyImmediate(images[i].GetComponent<UnityEngine.UI.Image>());
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                    /*static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        var codes = new List<CodeInstruction>(instructions);
                        var m_CorpLayoutGroup = typeof(UICorpToggles).GetField("m_CorpLayoutGroup", binding);
                        var getTogglesParent = codes.Find(ci => ci.opcode == OpCodes.Ldfld && (FieldInfo)ci.operand == m_CorpLayoutGroup);
                        getTogglesParent.opcode = OpCodes.Call;
                        getTogglesParent.operand = typeof(BlockLoader).GetMethod("GetCorpToggleParent", BindingFlags.Public | BindingFlags.Static);
                        var start = codes.FindLastIndex(ci => ci.opcode == OpCodes.Blt) + 1;
                        var end = codes.FindLastIndex(ci => ci.opcode == OpCodes.Brfalse_S) - 3;
                        //var nop = new CodeInstruction(OpCodes.Nop);
                        for (int i = start; i < end; i++)
                        {
                            codes[i].opcode = OpCodes.Nop;
                        }
                        return codes;
                    }*/
                }

                [HarmonyPatch(typeof(UICorpToggles), "UpdateMiniPalette")]
                private static class UpdateMiniPalette
                {
                    private static List<FactionSubTypes> temp;
                    private static void Prefix(ref UICorpToggles __instance)
                    {
                        if (CustomCorps.Count == 0) return;
                        var corps = ManPurchases.inst.AvailableCorporations;
                        var start = corps.IndexOf(last) + 1;
                        temp = corps.GetRange(start, corps.Count - start);
                        ManPurchases.inst.AvailableCorporations.RemoveRange(start, corps.Count - start);
                    }

                    private static void Postfix(ref UICorpToggles __instance)
                    {
                        if (CustomCorps.Count == 0) return;
                        ManPurchases.inst.AvailableCorporations.AddRange(temp);
                    }
                }
            }

            /*[HarmonyPatch(typeof(UICorpToggle), "SetCorp")]
            private static class UICorpToggle_SetCorp
            {

                //private static bool Prefix(ref UICorpToggle __instance, ref FactionSubTypes corp)
                //{
                //    if (corp > last)
                //    {
                //        m_Corp.SetValue(__instance, corp);
                //        ((Image)m_Icon.GetValue(__instance)).sprite = Singleton.Manager<ManUI>.inst.GetCorpIcon(FactionSubTypes.GSO);
                //        ((Image)m_SelectedIcon.GetValue(__instance)).sprite = Singleton.Manager<ManUI>.inst.GetSelectedCorpIcon(FactionSubTypes.GSO);
                //        ((TooltipComponent)m_TooltipComponent.GetValue(__instance)).SetText("TEST");
                //        return false;
                //    }
                //    return true;
                //}

                private static void Postfix(ref UICorpToggle __instance, ref FactionSubTypes corp)
                {
                    if (corp > last)
                    {
                        ((TooltipComponent)m_TooltipComponent.GetValue(__instance)).SetText(CustomCorps[(int)corp].Name);
                    }
                }
            }*/

            /*[HarmonyPatch(typeof(UISkinsPaletteHUD), "OnSpawn")]
            private static class UISkinsPaletteHUD_OnSpawn
            {
                /*
                IL_0000: ldtoken FactionSubTypes
                IL_0005: call class [mscorlib] System.Type[mscorlib] System.Type::GetTypeFromHandle(valuetype[mscorlib] System.RuntimeTypeHandle)
                IL_000A: call class [mscorlib] System.Array[mscorlib] System.Enum::GetValues(class [mscorlib] System.Type)
                IL_000F: callvirt instance class [mscorlib] System.Collections.IEnumerator[mscorlib] System.Array::GetEnumerator()
                IL_0014: stloc.0
	            */


            /*
            IL_0000: ldsfld    !0 class Singleton/Manager`1<class ManPurchases>::inst
            IL_0005: callvirt  instance class [mscorlib]System.Collections.Generic.List`1<valuetype FactionSubTypes> ManPurchases::get_AvailableCorporations()
            IL_000A: callvirt  instance !0[] class [mscorlib]System.Collections.Generic.List`1<valuetype FactionSubTypes>::ToArray()
            IL_000F: callvirt  instance class [mscorlib]System.Collections.IEnumerator [mscorlib]System.Array::GetEnumerator()
            IL_0014: stloc.0
            *//*

            static MethodInfo get_AvailableCorporations = T_ManPurchases.GetMethod("get_AvailableCorporations", BindingFlags.Public | BindingFlags.Instance);
            static MethodInfo ToArray = typeof(List<FactionSubTypes>).GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance);

            private static List<FactionSubTypes> temp;
            private static void Prefix(ref UISkinsPaletteHUD __instance)
            {
                temp = ManPurchases.inst.AvailableCorporations.GetRange(7, ManPurchases.inst.AvailableCorporations.Count - 7);
                ManPurchases.inst.AvailableCorporations.RemoveRange(7, ManPurchases.inst.AvailableCorporations.Count - 7);
                ManPurchases.inst.AvailableCorporations.Insert(0, FactionSubTypes.NULL);
            }

            private static void Postfix(ref UISkinsPaletteHUD __instance)
            {
                ManPurchases.inst.AvailableCorporations.AddRange(temp);
                ManPurchases.inst.AvailableCorporations.RemoveAt(0);
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                codes[0] = new CodeInstruction(OpCodes.Ldsfld, inst);
                codes[1] = new CodeInstruction(OpCodes.Callvirt, get_AvailableCorporations);
                codes[2] = new CodeInstruction(OpCodes.Callvirt, ToArray);
                return codes;
            }
        }*/

            [HarmonyPatch(typeof(ManCustomSkins), "Awake")]
            private static class ManCustomSkins_Awake
            {
                private static void Prefix(ref ManCustomSkins __instance)
                {
                    if (CustomCorps.Count == 0) return;
                    var skinInfos = ((ManCustomSkins.CorporationSkins[])m_SkinInfos.GetValue(__instance)).ToList();
                    foreach (var cc in CustomCorps)
                    {
                        skinInfos.Add(skinInfos[(int)FactionSubTypes.EXP]);
                    }
                    m_SkinInfos.SetValue(__instance, skinInfos.ToArray());
                }

                private static void Postfix(ref ManCustomSkins __instance)
                {
                    if (CustomCorps.Count == 0) return;
                    var corpSkin = ((int[])m_CorpSkinSelections.GetValue(__instance)).ToList();
                    corpSkin.Resize(corpSkin.Count + CustomCorps.Count, 0);
                    m_CorpSkinSelections.SetValue(__instance, corpSkin.ToArray());
                }

                /*
                IL_000A: ldarg.0
		        IL_000B: ldsfld    !0[] valuetype EnumValuesIterator`1<valuetype FactionSubTypes>::Values
		        IL_0010: ldlen
		        IL_0011: conv.i4
		        IL_0012: newarr    [mscorlib]System.Int32
		        IL_0017: stfld     int32[] ManCustomSkins::m_CorpSkinSelections

                IL_000A: ldarg.0
		        IL_000B: ldtoken   FactionSubTypes
		        IL_0010: call      class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
		        IL_0015: call      class [mscorlib]System.Array [mscorlib]System.Enum::GetValues(class [mscorlib]System.Type)
		        IL_001A: callvirt  instance int32 [mscorlib]System.Array::get_Length()
		        IL_001F: newarr    [mscorlib]System.Int32
		        IL_0024: stfld     int32[] ManCustomSkins::m_CorpSkinSelections


                IL_0060: ldsfld    !0[] valuetype EnumValuesIterator`1<valuetype FactionSubTypes>::Values
		        IL_0065: stloc.0
		        IL_0066: ldc.i4.0
		        IL_0067: stloc.1
		        IL_0068: br        IL_011A

                IL_0060: ldtoken   FactionSubTypes
		        IL_0065: call      class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
		        IL_006A: call      class [mscorlib]System.Array [mscorlib]System.Enum::GetValues(class [mscorlib]System.Type)
		        IL_006F: castclass valuetype FactionSubTypes[]
		        IL_0074: stloc.0
		        IL_0075: ldc.i4.0
		        IL_0076: stloc.1
		        IL_0077: br        IL_0129
                */

                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    var GetTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static);
                    var GetValues = typeof(Enum).GetMethod("GetValues", BindingFlags.Public | BindingFlags.Static);
                    var codes = new List<CodeInstruction>(instructions);
                    var corpSkinSelectionsLengthI = codes.FindIndex(ci => ci.opcode == OpCodes.Ldsfld);
                    var cssli = corpSkinSelectionsLengthI;
                    codes[cssli] = new CodeInstruction(OpCodes.Ldtoken, typeof(FactionSubTypes));
                    codes[cssli + 1] = new CodeInstruction(OpCodes.Call, GetTypeFromHandle);
                    codes[cssli + 2] = new CodeInstruction(OpCodes.Call, GetValues);
                    codes.Insert(cssli + 3, new CodeInstruction(OpCodes.Callvirt, typeof(Array).GetMethod("get_Length", binding)));

                    var factionsListI = codes.FindIndex(ci => ci.opcode == OpCodes.Br) - 4;
                    var fli = factionsListI;
                    codes[fli] = new CodeInstruction(OpCodes.Ldtoken, typeof(FactionSubTypes));
                    codes.Insert(fli + 1, new CodeInstruction(OpCodes.Castclass, typeof(FactionSubTypes[])));
                    codes.Insert(fli + 1, new CodeInstruction(OpCodes.Call, GetValues));
                    codes.Insert(fli + 1, new CodeInstruction(OpCodes.Call, GetTypeFromHandle));

                    /*foreach (var ci in codes)
                    {
                        Console.WriteLine(ci.opcode.ToString());
                    }*/
                    return codes;
                }
            }

            [HarmonyPatch(typeof(ManCustomSkins), "ShowCorpInUI")]
            private static class ManCustomSkins_ShowCorpInUI
            {
                private static bool Prefix(ref FactionSubTypes corp, ref bool __result)
                {
                    if (corp > last)
                    {
                        __result = false;
                        return false;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(ManCustomSkins), "DoPaintBlock")]
            private static class ManCustomSkins_DoPaintBlock
            {
                private static bool Prefix(ref ManCustomSkins __instance, ref TankBlock block)
                {
                    return (m_CorpSkinSelections.GetValue(__instance) as Array).Length > (int)Singleton.Manager<ManSpawn>.inst.GetCorporation(block.BlockType);
                }
            }

            [HarmonyPatch(typeof(ManCustomSkins), "SkinIndexToID")]
            private static class ManCustomSkins_SkinIndexToID
            {
                private static bool Prefix(ref ManCustomSkins __instance, ref FactionSubTypes corp)
                {
                    return (m_CorpSkinSelections.GetValue(__instance) as Array).Length > (int)corp;
                }
            }

            [HarmonyPatch(typeof(ManCustomSkins), "SkinIDToIndex")]
            private static class ManCustomSkins_SkinIDToIndex
            {
                private static bool Prefix(ref ManCustomSkins __instance, ref FactionSubTypes corp)
                {
                    return (m_CorpSkinSelections.GetValue(__instance) as Array).Length > (int)corp;
                }
            }

            [HarmonyPatch(typeof(ManCustomSkins), "CanUseSkin")]
            private static class ManCustomSkins_CanUseSkin
            {
                private static bool Prefix(ref ManCustomSkins __instance, ref FactionSubTypes corp, ref int skinIndex, ref bool __result)
                {
                    if((m_CorpSkinSelections.GetValue(__instance) as Array).Length > (int)corp) return true;
                    
                    __result = skinIndex == 0;
                    return false;
                }
            }

            [HarmonyPatch(typeof(ManTechMaterialSwap), "Start")]
            private static class ManTechMaterialSwap_Start
            {
                private static void Prefix(ref ManTechMaterialSwap __instance)
                {
                    var m_MinEmissivePerCorporation = typeof(ManTechMaterialSwap).GetField("m_MinEmissivePerCorporation", binding);
                    var emissive = m_MinEmissivePerCorporation.GetValue(__instance) as float[];
                    /*for (int i = 0; i < emissive.Length; i++)
                    {
                        Console.WriteLine(((FactionSubTypes)i).ToString() + " " + emissive[i].ToString());
                    }*/
                    var emissiveList = emissive.ToList();
                    foreach (var item in CustomCorps)
                    {
                        emissiveList.Add(0f);
                    }
                    m_MinEmissivePerCorporation.SetValue(__instance, emissiveList.ToArray());
                }
            }

            [HarmonyPatch(typeof(TankBlock), "SetSkinIndex")]
            private static class TankBlock_SetSkinIndex
            {
                private static bool Prefix(ref TankBlock __instance)
                {
                    return (m_CorpSkinSelections.GetValue(ManCustomSkins.inst) as Array).Length > (int)Singleton.Manager<ManSpawn>.inst.GetCorporation(__instance.BlockType);
                }
            }

            [HarmonyPatch(typeof(UISkinsPaletteController), "SetSelectedSkinForCorp")]
            private static class UISkinsPaletteController_SetSelectedSkinForCorp
            {
                private static bool Prefix(ref UISkinsPaletteController __instance, ref FactionSubTypes corp)
                {
                    return corp <= last;
                }
            }

            [HarmonyPatch(typeof(UIPaletteBlockSelect), "OnSpawn")]
            private static class UIPaletteBlockSelect_OnSpawn
            {
                private static void Prefix(ref UIPaletteBlockSelect __instance)
                {
                    //CreateScrollView(__instance.GetType().GetField("m_CorpToggles", binding).GetValue(__instance) as UICorpToggles);
                }
            }

            [HarmonyPatch(typeof(UIShopBlockSelect), "OnSpawn")]
            private static class UIShopBlockSelect_OnSpawn
            {
                private static void Prefix(ref UIShopBlockSelect __instance)
                {
                    //CreateScrollView(__instance.GetType().GetField("m_CorpToggles", binding).GetValue(__instance) as UICorpToggles);
                }
            }
        }

        private static Dictionary<UICorpToggles, Component> corpToggleParents = new Dictionary<UICorpToggles, Component>();
        public static Component GetCorpToggleParent(UICorpToggles uiCorpToggles)
        {
            return corpToggleParents[uiCorpToggles];
        }

        internal static void CreateScrollView(UICorpToggles uiCorpToggles)
        {
            var togglesParent = (uiCorpToggles.GetType().GetField("m_CorpLayoutGroup", binding).GetValue(uiCorpToggles) as LayoutGroup).gameObject;
            try
            {
                GameObject.DestroyImmediate(togglesParent.GetComponent<HorizontalLayoutGroup>());
                GameObject.DestroyImmediate(togglesParent.GetComponent<VerticalLayoutGroup>());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            try
            {
                var lgrect = togglesParent.GetComponent<RectTransform>();

                GameObject scrollView = new GameObject("Scroll View");
                var swrect = scrollView.AddComponent<RectTransform>();
                swrect.anchoredPosition3D = lgrect.anchoredPosition3D;
                swrect.anchorMin = lgrect.anchorMin;
                swrect.anchorMax = lgrect.anchorMax;
                swrect.pivot = lgrect.pivot;
                swrect.sizeDelta = lgrect.sizeDelta;

                GameObject viewport = new GameObject("Viewport");
                viewport.layer = scrollView.layer;
                var vprect = viewport.AddComponent<RectTransform>();
                vprect.anchorMin = Vector2.zero;
                vprect.anchorMax = Vector2.one;
                vprect.sizeDelta = Vector2.zero;
                vprect.pivot = Vector2.up;
                var vpmask = viewport.AddComponent<Mask>();
                vpmask.showMaskGraphic = false;
                viewport.transform.SetParent(scrollView.transform, false);
                scrollView.transform.SetParent(togglesParent.transform.parent, false);
                togglesParent.transform.SetParent(viewport.transform, false);


                lgrect.anchorMin = Vector2.up;
                lgrect.anchorMax = Vector2.one;
                lgrect.pivot = Vector2.up;


                var glg = togglesParent.AddComponent<GridLayoutGroup>();
                glg.childAlignment = TextAnchor.UpperLeft;
                glg.padding = new RectOffset(8, 0, 16, 0);
                glg.cellSize = new Vector2(24f, 24f);
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = 1;
                glg.spacing = new Vector2(8f, 8f);

                var csf = togglesParent.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.MinSize;

                var swscrollrect = scrollView.AddComponent<ScrollRect>();
                swscrollrect.horizontal = false;
                swscrollrect.vertical = true;
                swscrollrect.viewport = vprect;
                swscrollrect.content = lgrect;
                swscrollrect.decelerationRate = 0.135f;
                swscrollrect.elasticity = 0.1f;
                swscrollrect.inertia = true;
                swscrollrect.movementType = ScrollRect.MovementType.Elastic;
                swscrollrect.verticalScrollbarSpacing = -3;
                swscrollrect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

                corpToggleParents.Add(uiCorpToggles, glg);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        internal static void FixBlockUnlockTable(CustomBlock block)
        {
            try
            {
                ManLicenses.inst.DiscoverBlock((BlockTypes)block.RuntimeID);
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
            catch (Exception E)
            {
                Timer.AddToLast(" - FAILED: Could not add to block table. Could it be the grade level?");
                Console.WriteLine("Registering block failed: Could not add to block table. " + E.Message);
            }
        }

        //static int lastFrameRendered;
        static List<int> UnpermitSpriteGeneration = new List<int>();
        private static MethodInfo RenderSnapshotFromTechDataInternal = typeof(ManScreenshot).GetMethod("RenderSnapshotFromTechDataInternal", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool ResourceLookup_OnSpriteLookup(ObjectTypes objectType, int itemType, ref UnityEngine.Sprite result)
        {
            if (objectType == ObjectTypes.Block)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    if (result == null && !UnpermitSpriteGeneration.Contains(itemType)) // Create a sprite right now
                    {
                        /*try
                        {
                            //lastFrameRendered = Time.frameCount;
                            var b = new TankPreset.BlockSpec() { block = block.Name, m_BlockType = (BlockTypes)block.RuntimeID, m_SkinID = 0, m_VisibleID = -1, orthoRotation = 0, position = IntVector3.zero, saveState = new Dictionary<int, Module.SerialData>(), textSerialData = new List<string>() };
                            Texture2D image = RenderSnapshotFromTechDataInternal.Invoke(ManScreenshot.inst, new object[] { new TechData() { m_BlockSpecs = new List<TankPreset.BlockSpec> { b } }, new IntVector2(256, 256) }) as Texture2D; // Devs why did you make this private

                            //float x = image.height / (float)image.width;
                            //float x = 1f;
                            result = GameObjectJSON.SpriteFromImage(image);//GameObjectJSON.CropImage(image, new Rect((1f - x) * 0.5f, 0f, x, 1f)));
                        }
                        catch { UnpermitSpriteGeneration.Add(itemType); }*/
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

                case LocalisationEnums.StringBanks.Corporations:
                    if (CustomCorps.TryGetValue(EnumValue, out CustomCorporation corp))
                    {
                        Result = corp.Name;
                        return Result != null && Result != "";
                    }
                    break;
            }
            return false;
        }
    }
}