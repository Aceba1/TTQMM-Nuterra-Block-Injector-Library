using UnityEngine;

namespace Nuterra.BlockInjector
{
    public sealed class CustomBlock
    {
        public int BlockID { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public int Price { get; internal set; }
        public FactionSubTypes Faction { get; internal set; } = FactionSubTypes.EXP;
        public BlockCategories Category { get; internal set; } = BlockCategories.Standard;
        public BlockRarity Rarity { get; internal set; } = BlockRarity.Common;
        public GameObject Prefab { get; internal set; }
        public Sprite DisplaySprite { get; internal set; }
        public int Grade { get; internal set; }
        public void Register()
        {
            BlockLoader.Register(this);
        }
    }

    public sealed class CustomChunk
    {
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public int ChunkID { get; internal set; }
        public int SaleValue { get; internal set; }
        public float Mass { get; internal set; }
        public float FrictionStatic { get; internal set; }
        public float FrictionDynamic { get; internal set; }
        public float Restitution { get; internal set; }
        public Transform BasePrefab { get; internal set; }
        public void Register()
        {
            BlockLoader.Register(this);
        }
    }
}