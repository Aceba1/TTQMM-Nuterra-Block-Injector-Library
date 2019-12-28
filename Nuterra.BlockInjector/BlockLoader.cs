using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;
using UnityEngine.UI;

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
                    UnpermitSpriteGeneration.Remove(blockID);
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
        internal class Patches
        {
            /*
             * static int[] s_AttachPointsTemp;
             * static Dictionary<int, int[]> s_APFilledCellsPerBlock;
             * Pre_InitAPFilledCells()
             * {
             *     // Copy what is in InitAPFilledCells, but fill the int[] array inside the dictionary instead of the byte[] array
             *     s_AttachPointsTemp = block.attachPoints;
             *     block.attachPoints = new Vector3[0]; // This bit prevents the crash in InitFilledAPCells() base version by making it loop 0 times
             * }
             * Post_InitAPFilledCells()
             * {
             *     block.attackPoints = s_AttachPointsTemp;
             *     block.m_APFilledCells = new byte[s_AttachPointsTemp.Length];
             * }
             * Post_GetFilledCellForAPIndex()
             * {
             *     return filledCells[s_APFilledCellsPerBlock[block.blockID][index]];
             * }
             */
            //private static class FlansPatch
            //{
            //    static Dictionary<int, int[]> s_APFilledCellsPerBlock = new Dictionary<int, int[]>();
            //    static FieldInfo m_APFilledCells = typeof(TankBlock).GetField("m_APFilledCells", binding);

            //    [HarmonyPatch(typeof(TankBlock), "InitAPFilledCells")]
            //    private static class FlansPatch_InitAPFilledCells
            //    {
            //        private static bool Prefix(ref TankBlock __instance)
            //        {
            //            if (__instance.filledCells.Length > 255)
            //            {
            //                List<Vector3> croppedAPs = new List<Vector3>();
            //                List<int> APFilledCells = new List<int>();
            //                bool NotifyError = true;
            //                for (int i = 0; i < __instance.attachPoints.Length; i++)
            //                {
            //                    Vector3 attachPoint = __instance.attachPoints[i];
            //                    IntVector3 ScaledAP = attachPoint * 2f;
            //                    IntVector3 FlooredAP = ScaledAP.PadHalfDown();
            //                    IntVector3 RoofedAP = FlooredAP + ScaledAP.AxisUnit();
            //                    for (int cell = 0; cell < __instance.filledCells.Length; cell++)
            //                    {
            //                        bool FlooredCell = __instance.filledCells[cell] == FlooredAP,
            //                             RoofedCell = __instance.filledCells[cell] == RoofedAP;
            //                        if (FlooredCell || RoofedCell)
            //                        {
            //                            if (FlooredCell && RoofedCell)
            //                            {
            //                                Console.WriteLine($"Block {__instance.name} has an AP crushed between two cells! ({attachPoint})");
            //                                NotifyError = false;
            //                                break;
            //                            }
            //                            APFilledCells.Add(cell);
            //                            croppedAPs.Add(attachPoint);
            //                            NotifyError = false;
            //                            break;
            //                        }
            //                    }
            //                    if (NotifyError)
            //                    {
            //                        Console.WriteLine($"Block {__instance.name} has an AP without a cell! ({attachPoint})");
            //                    }
            //                }
            //                s_APFilledCellsPerBlock[__instance.GetComponent<Visible>().ItemType] = APFilledCells.ToArray();
            //                m_APFilledCells.SetValue(__instance, new byte[APFilledCells.Count]);
            //                __instance.attachPoints = croppedAPs.ToArray();
            //                return false;
            //            }
            //            return true;
            //        }
            //    }

            //    [HarmonyPatch(typeof(TankBlock), "GetFilledCellForAPIndex")]
            //    private static class FlansPatch_GetFilledCellForAPIndex
            //    {
            //        private static bool Prefix(ref TankBlock __instance, ref IntVector3 __result, int index)
            //        {
            //            if (s_APFilledCellsPerBlock.TryGetValue(__instance.visible.ID, out int[] APCellArray))
            //            {
            //                __result = __instance.filledCells[APCellArray[index]];
            //                return false;
            //            }
            //            return true;
            //        }
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

                    Console.WriteLine("\nCorps Levels");
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
                    }
                }
            }

            [HarmonyPatch(typeof(ManPurchases), "Init")]
            private static class ManPurchases_Init
            {
                private static void Postfix(ref ManPurchases __instance)
                {
                    //if (!__instance.AvailableCorporations.Contains((FactionSubTypes)8)) __instance.AvailableCorporations.Add((FactionSubTypes)8);
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
                    if (enumType == typeof(FactionSubTypes))
                    {
                        Array.Resize(ref __result, 9);
                        __result[8] = "8";
                    }
                }
            }

            [HarmonyPatch(typeof(Enum), "GetValues")]
            private static class Enum_GetValues
            {
                private static void Postfix(ref Type enumType, ref Array __result)
                {
                    if (enumType == typeof(FactionSubTypes))
                    {
                        var temp = new object[__result.Length + 1];
                        __result.CopyTo(temp, 0);
                        temp[8] = (FactionSubTypes)8;
                        __result = temp as Array;
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
                }

                [HarmonyPatch(typeof(UICorpToggles), "UpdateMiniPalette")]
                private static class UpdateMiniPalette
                {
                    private static List<FactionSubTypes> temp;
                    private static void Prefix(ref UICorpToggles __instance)
                    {
                        var corps = ManPurchases.inst.AvailableCorporations;
                        var start = corps.IndexOf(last) + 1;
                        temp = corps.GetRange(start, corps.Count - start);
                        ManPurchases.inst.AvailableCorporations.RemoveRange(start, corps.Count - start);
                    }

                    private static void Postfix(ref UICorpToggles __instance)
                    {
                        ManPurchases.inst.AvailableCorporations.AddRange(temp);
                    }
                }
            }

            [HarmonyPatch(typeof(UICorpToggle), "SetCorp")]
            private static class UICorpToggle_SetCorp
            {

                /*private static bool Prefix(ref UICorpToggle __instance, ref FactionSubTypes corp)
                {
                    if (corp > last)
                    {
                        m_Corp.SetValue(__instance, corp);
                        ((Image)m_Icon.GetValue(__instance)).sprite = Singleton.Manager<ManUI>.inst.GetCorpIcon(FactionSubTypes.GSO);
                        ((Image)m_SelectedIcon.GetValue(__instance)).sprite = Singleton.Manager<ManUI>.inst.GetSelectedCorpIcon(FactionSubTypes.GSO);
                        ((TooltipComponent)m_TooltipComponent.GetValue(__instance)).SetText("TEST");
                        return false;
                    }
                    return true;
                }*/

                private static void Postfix(ref UICorpToggle __instance, ref FactionSubTypes corp)
                {
                    if (corp > last)
                    {
                        ((TooltipComponent)m_TooltipComponent.GetValue(__instance)).SetText(CustomCorps[(int)corp].Name);
                    }
                }
            }

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
                    var skinInfos = ((ManCustomSkins.CorporationSkins[])m_SkinInfos.GetValue(__instance)).ToList();
                    foreach (var cc in CustomCorps)
                    {
                        skinInfos.Add(skinInfos[(int)FactionSubTypes.EXP]);
                    }
                    m_SkinInfos.SetValue(__instance, skinInfos.ToArray());
                }

                private static void Postfix(ref ManCustomSkins __instance)
                {
                    var corpSkin = ((int[])m_CorpSkinSelections.GetValue(__instance)).ToList();
                    corpSkin.Resize(corpSkin.Count + CustomCorps.Count);
                    m_CorpSkinSelections.SetValue(__instance, corpSkin.ToArray());
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

            [HarmonyPatch(typeof(UISkinsPaletteController), "SetSelectedSkinForCorp")]
            private static class UISkinsPaletteController_SetSelectedSkinForCorp
            {
                private static bool Prefix(ref UISkinsPaletteController __instance, ref FactionSubTypes corp)
                {
                    return corp <= last;
                }
            }
        }

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
        static List<int> UnpermitSpriteGeneration = new List<int>();
        private static bool ResourceLookup_OnSpriteLookup(ObjectTypes objectType, int itemType, ref UnityEngine.Sprite result)
        {
            if (objectType == ObjectTypes.Block)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    if (result == null && !UnpermitSpriteGeneration.Contains(itemType))// && lastFrameRendered != Time.frameCount)
                    {
                        /*try
                        {
                            //lastFrameRendered = Time.frameCount;
                            var b = new TankPreset.BlockSpec() { block = block.Name, m_BlockType = (BlockTypes)block.BlockID, m_SkinID = 0, m_VisibleID = -1, orthoRotation = 0, position = IntVector3.zero, saveState = new Dictionary<int, Module.SerialData>(), textSerialData = new List<string>() };
                            var image = ManScreenshot.inst.RenderSnapshotFromTechData(new TechData() { m_BlockSpecs = new List<TankPreset.BlockSpec> { b } }, new IntVector2(256, 256));

                            //float x = image.height / (float)image.width;
                            float x = 1f;
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
            }
            return false;
        }
    }
}