using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nuterra.BlockInjector
{
    static class UnstableSupport_RemoveWhenStable
    {
        internal static void FixBlockUnlockTable(CustomBlock block)
        {
            BlockUnlockTable blockList = ManLicenses.inst.GetBlockUnlockTable();
            BlockUnlockTable.CorpBlockData corpData = blockList.GetCorpBlockData((int)block.Faction);
            BlockUnlockTable.UnlockData[] unlocked = corpData.m_GradeList[block.Grade].m_BlockList;

            Array.Resize(ref unlocked, unlocked.Length + 1);
            unlocked[unlocked.Length - 1] = new BlockUnlockTable.UnlockData
            {
                m_BlockType = (BlockTypes)block.RuntimeID,
                m_BasicBlock = true,
                m_DontRewardOnLevelUp = true,
                //m_HideOnLevelUpScreen = true // Could parameterize
            };

            corpData.m_GradeList[block.Grade].m_BlockList = unlocked;

            (BlockLoader.m_CorpBlockLevelLookup.GetValue(blockList) as Dictionary<int, Dictionary<BlockTypes, int>>)[(int)block.Faction].Add((BlockTypes)block.RuntimeID, block.Grade);

            ManLicenses.inst.DiscoverBlock((BlockTypes)block.RuntimeID);
        }

        internal static void BlockUnlockTable_RemoveModdedBlocks_Prefix()
        {
            var corpBlockList = ManLicenses.inst.GetBlockUnlockTable();

            foreach (var corpPair in corpBlockList)
            {
                foreach (var b in corpPair.Value.m_GradeList)
                {
                    b.m_BlockList = b.m_BlockList.Where(
                        ud => (int)ud.m_BlockType < BlockLoader.Patches.NEW_BASE_ID || BlockLoader.CustomBlocks.ContainsKey((int)ud.m_BlockType)).ToArray();
                }
            }
        }
    }
}
