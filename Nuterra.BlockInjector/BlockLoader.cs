using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    public static class BlockLoader
    {
        private static readonly Dictionary<int, CustomBlock> CustomBlocks = new Dictionary<int, CustomBlock>();
        private static readonly Dictionary<int, CustomChunk> CustomChunks = new Dictionary<int, CustomChunk>();

        public static void Register(CustomBlock block)
        {
            Console.WriteLine($"Registering block: {block.GetType()} #{block.BlockID} '{block.Name}'");
            int blockID = block.BlockID;
            CustomBlocks.Add(blockID, block);
            int hashCode = ItemTypeInfo.GetHashCode(ObjectTypes.Block, blockID);
            ManSpawn spawnManager = ManSpawn.inst;
            spawnManager.VisibleTypeInfo.SetDescriptor<FactionSubTypes>(hashCode, block.Faction);
            spawnManager.VisibleTypeInfo.SetDescriptor<BlockCategories>(hashCode, block.Category);
            try
            {
                typeof(ManSpawn).GetMethod("AddBlockToDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(spawnManager, new object[] { block.Prefab });
                (typeof(RecipeManager).GetField("m_BlockPriceLookup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(RecipeManager.inst) as Dictionary<int, int>).Add(blockID, block.Price);
            }
            catch(Exception E)
            {
                UnityEngine.Debug.LogException(E);
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
            BaconBlock.Load();
            UnityEngine.Debug.Log("Created Example Block");
        }

        internal class Patches
        {
            [HarmonyPatch(typeof(ManSpawn), "IsBlockAvailableOnPlatform")]
            private static class TableFix
            {
                private static void Postfix(ref bool __result, BlockTypes blockType)
                {
                    if (!Enum.IsDefined(typeof(BlockTypes), blockType)) __result = true;
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


        internal static void FixBlockUnlockTable(CustomBlock block)
        {
            ManLicenses.inst.DiscoverBlock((BlockTypes)block.BlockID);
            //For now, all custom blocks are level 1
            BindingFlags bind = BindingFlags.Instance | BindingFlags.NonPublic;
            Type T_BlockUnlockTable = typeof(BlockUnlockTable);
            Type CorpBlockData = T_BlockUnlockTable.GetNestedType("CorpBlockData", bind);
            Type GradeData = T_BlockUnlockTable.GetNestedType("GradeData", bind);
            Array blockList = T_BlockUnlockTable.GetField("m_CorpBlockList", bind).GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array;


            object corpData = blockList.GetValue((int)block.Faction);
            BlockUnlockTable.UnlockData[] unlocked = GradeData.GetField("m_BlockList").GetValue(((CorpBlockData.GetField("m_GradeList").GetValue(corpData)) as Array).GetValue(block.Grade)) as BlockUnlockTable.UnlockData[];
            Array.Resize(ref unlocked, unlocked.Length + 1);
            unlocked[unlocked.Length - 1] = new BlockUnlockTable.UnlockData
            {
                m_BlockType = (BlockTypes)block.BlockID,
                m_BasicBlock = true,
                m_DontRewardOnLevelUp = true
            };
            GradeData.GetField("m_BlockList").SetValue(((CorpBlockData.GetField("m_GradeList").GetValue(corpData)) as Array).GetValue(block.Grade), unlocked);
        }

        private static bool ResourceLookup_OnSpriteLookup(ObjectTypes objectType, int itemType, ref UnityEngine.Sprite result)
        {
            if (objectType == ObjectTypes.Block)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    return true;
                }
            }
            else if (objectType == ObjectTypes.Chunk)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    return true;
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
                        return true;
                    }
                    break;

                case LocalisationEnums.StringBanks.BlockDescription:
                    if (CustomBlocks.TryGetValue(EnumValue, out block))
                    {
                        Result = block.Description;
                        return true;
                    }
                    break;
                case LocalisationEnums.StringBanks.ChunkName:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Name;
                        return true;
                    }
                    break;

                case LocalisationEnums.StringBanks.ChunkDescription:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Description;
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}