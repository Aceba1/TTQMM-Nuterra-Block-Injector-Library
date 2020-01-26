using UnityEngine;
using System;
using System.Reflection;

namespace Nuterra.BlockInjector
{
    public sealed class CustomBlock
    {
        //public int RuntimeID { get; internal set; }
        public int RuntimeID { get => BlockID; internal set => BlockID = value; }
        //public string BlockID { get; internal set; }
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

    internal class ModuleCustomBlock : Module
    {
        public bool HasInjectedCenterOfMass;
        public Vector3 InjectedCenterOfMass;
        public BlockPrefabBuilder.EmissionMode EmissionMode;
        
        internal uint reparse_version_cache;

        private bool rbodyExists = false;
        private float emissionTimeDelay = 0f;


        static Type T_ComponentPool = typeof(ComponentPool);
        static FieldInfo m_ReturnToPoolIndex = T_ComponentPool.GetField("m_ReturnToPoolIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        void OnRecycle()
        {
            if (BlockPrefabBuilder.ReparseVersion[(int)block.BlockType] != reparse_version_cache)
            {
                m_ReturnToPoolIndex.SetValue(ComponentPool.inst, (int)m_ReturnToPoolIndex.GetValue(ComponentPool.inst) - 1);
                GameObject.Destroy(gameObject);
            }
        }

        public void SetEmissionColor(Color EmissionColor)
        {
            foreach (var ren in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (ren.material.IsKeywordEnabled("StandardTankBlock"))
                    ren.material.SetColor("_EmissionColor", EmissionColor);
            }
        }

        public void SetEmissionOn()
        {
            block.SwapMaterialTime(true);
            SetEmissionColor(Color.white);
        }
        public void SetEmissionOff()
        {
            block.SwapMaterialTime(false);
            SetEmissionColor(Color.black);
        }

        void ChangeTimeEmission(bool _)
        {
            emissionTimeDelay = UnityEngine.Random.value * 2f + 1f;
        }

        void ChangeAnchorEmission(ModuleAnchor _, bool isAnchored, bool __)
        {
            if (isAnchored)
                SetEmissionOn();
            else
                SetEmissionOff();
        }
        void HookAnchorEmission()
        {
            if (block.tank.IsAnchored) SetEmissionOn();
            block.tank.AnchorEvent.Subscribe(ChangeAnchorEmission);
        }
        void UnhookAnchorEmission()
        {
            block.tank.AnchorEvent.Unsubscribe(ChangeAnchorEmission);
            SetEmissionOff();
        }

        void OnSpawn()
        {
            switch (EmissionMode)
            {
                case BlockPrefabBuilder.EmissionMode.Active:
                    SetEmissionOn(); break;

                case BlockPrefabBuilder.EmissionMode.ActiveAtNight:
                    if (ManTimeOfDay.inst.NightTime)
                        SetEmissionOn();
                    else
                        SetEmissionOff();
                    break;
            }
        }

        void OnPool()
        {
            switch (EmissionMode)
            {
                case BlockPrefabBuilder.EmissionMode.ActiveAtNight:
                    ManTimeOfDay.inst.DayEndEvent.Subscribe(ChangeTimeEmission); break;

                case BlockPrefabBuilder.EmissionMode.ActiveWhenAnchored:
                    {
                        block.AttachEvent.Subscribe(HookAnchorEmission);
                        block.DetachEvent.Subscribe(UnhookAnchorEmission);
                        break;
                    }
                default: break;
            }
        }

        void Update()
        {
            if (emissionTimeDelay > 0f)
            {
                emissionTimeDelay -= Time.deltaTime;
                if (emissionTimeDelay <= 0f)
                {
                    emissionTimeDelay = 0f;
                    if (ManTimeOfDay.inst.NightTime)
                        SetEmissionOn();
                    else
                        SetEmissionOff();
                }
            }
            if (HasInjectedCenterOfMass)
            {
                bool re = block.rbody.IsNotNull();
                if (re != rbodyExists)
                {
                    rbodyExists = re;
                    if (re)
                    {
                        block.rbody.centerOfMass = InjectedCenterOfMass;
                    }
                }
            }
        }
    }
}