using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Nuterra.BlockInjector;

#region Projectiles

public class ProjectileMoneyOnVanish : MonoBehaviour
{
    /// <summary>
    /// <see cref="float"/>
    /// </summary>
    static FieldInfo Projectile_LifeTime = typeof(Projectile).GetField("m_LifeTime", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

    Projectile _projectile;
    float _projectile_lifetime;
    float _waittime;
    public int Money;



    void OnSpawn()
    {
        _waittime = Time.time + _projectile_lifetime;
    }

    void OnPool()
    {
        _projectile = GetComponent<Projectile>(); // Just in case
        if (_projectile == null) Console.WriteLine("ProjectileMoneyOnVanish was added on an object without a Projectile component!");
        else
        {
            _projectile_lifetime = (float)Projectile_LifeTime.GetValue(_projectile) - 0.1f;
            if (_projectile_lifetime <= 0f)
            {
                Console.WriteLine("ProjectileMoneyOnVanish : Projectile m_LifeTime is undefined!");
            }
        }
    }
    void OnRecycle()
    {
        if (_projectile != null && (Time.time >= _waittime && (_projectile.Shooter == null || _projectile.Shooter.IsFriendly())))
        {
            Singleton.Manager<ManPlayer>.inst.AddMoney(Money);
            WorldPosition position = Singleton.Manager<ManOverlay>.inst.WorldPositionForFloatingText(_projectile.Shooter.visible);
            Singleton.Manager<ManOverlay>.inst.AddFloatingTextOverlay(Singleton.Manager<Localisation>.inst.GetMoneyStringWithSymbol(Money), position);
            if (Singleton.Manager<ManNetwork>.inst.IsServer)
            {
                PopupNumberMessage message = new PopupNumberMessage
                {
                    m_Type = PopupNumberMessage.Type.Money,
                    m_Number = Money,
                    m_Position = position
                };
                Singleton.Manager<ManNetwork>.inst.SendToAllExceptHost(TTMsgType.AddFloatingNumberPopupMessage, message);
                return;
            }
            _waittime = float.PositiveInfinity;
        }
    }
}

#endregion

#region Modules

[RequireComponent(typeof(ModuleEnergyStore))]
[RequireComponent(typeof(ModuleEnergy))]
public class ModuleConsumeEnergyToShell : Module
{
    public ModuleWeaponWrapper WeaponWrapper;
    public ModuleEnergyStore EnergyStore;
    public ModuleEnergy Energy;

    public float EnergyCost = 50f;
    public float EnergyCapacity = 100f;
    public float LowestPermittedEnergy = 50f;
    public float TimeBeforeRetry = 1f;

    public CustomGauge[] _gauges = Array.Empty<CustomGauge>();

    public CustomGaugeSerializer CustomGauge
    {
        set
        {
            _gauges = new CustomGauge[1] { value.GaugeObject.gameObject.AddComponent<CustomGauge>() };
            _gauges[0].ApplyData(value);
        }
    }

    public CustomGaugeSerializer[] CustomGauges
    {
        set
        {
            _gauges = new CustomGauge[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                _gauges[i] = value[i].GaugeObject.gameObject.AddComponent<CustomGauge>();
                _gauges[i].ApplyData(value[i]);
            }
        }
    }

    float _TimeFault;

    public bool IsContinuous => WeaponWrapper.WrapType == ModuleWeaponWrapper.WeaponType.Continuous;

    public float ActualCurrentEnergy
    {
        get
        {
            var e = block.tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            return e.storageTotal - e.spareCapacity;
        }
    }

    void OnSpawn()
    {
        if (_gauges.Length != 0)
            foreach(var gauge in _gauges) 
                gauge.HardSet(0f);
    }

    void Update()
    {
        if (block.tank != null && _gauges.Length != 0)
        {
            float energy = ActualCurrentEnergy;
            foreach (var gauge in _gauges)
                gauge.Set(energy);
        }
    }

    public void PrePool()
    {
        WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();
        EnergyStore = GetComponent<ModuleEnergyStore>();
        //Energy = GetComponent<ModuleEnergy>();
        EnergyStore.m_Capacity = EnergyCapacity;

        // Backup fallback
        foreach (CustomGauge gauge in _gauges)
        {
            if (gauge.MinValue == gauge.MaxValue && gauge.MinValue == 0f)
            {
                gauge.MinValue = LowestPermittedEnergy;
                gauge.MaxValue = EnergyCost;
            }
            else
            {
                if (gauge.MaxValue == 0f) gauge.MaxValue = EnergyCost;
                if (gauge.MinValue == gauge.MaxValue) gauge.MaxValue++;
            }
        }
    }

    public void OnPool()
    {
        //WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        //if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();

        // Just in case...
        WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        EnergyStore = GetComponent<ModuleEnergyStore>();
        Energy = GetComponent<ModuleEnergy>();

        Energy.UpdateConsumeEvent.Subscribe(ConsumeFire);
        WeaponWrapper.CanFireEvent += CheckIfCanFire;
        WeaponWrapper.FireEvent += OnFire;
        WeaponWrapper.LockFiring(this, true);
        block.DetachEvent.Subscribe(OnDetach);
    }

    void OnDetach()
    {
        if (_gauges.Length != 0)
            foreach (var gauge in _gauges)
                gauge.HardSet(0f);
    }

    void OnFire(int amount)
    {
        float cost = amount * EnergyCost;
        if (IsContinuous)
            cost *= Time.deltaTime;

        _energyToConsume += cost;
        // Heck it
        block.tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric).spareCapacity += cost;
    }

    [NonSerialized]
    float _energyToConsume;

    void ConsumeFire()
    {
        if (_energyToConsume != 0f)
        {
            Energy.ConsumeUpToMax(EnergyRegulator.EnergyType.Electric, _energyToConsume);
            _energyToConsume = 0f;
        }
    }

    void CheckIfCanFire()
    {
        if (_TimeFault > Time.time) return;

        float energy = ActualCurrentEnergy;
        bool Allow;
        if (IsContinuous)
            Allow = energy > EnergyCost * Time.deltaTime + LowestPermittedEnergy;
        else
            Allow = energy >= EnergyCost + LowestPermittedEnergy;

        if (!Allow && WeaponWrapper.AllowedToFire)
            _TimeFault = Time.time + TimeBeforeRetry;
        WeaponWrapper.LockFiring(this, !Allow);
    }
}

public class ModuleConsumeResourceToShell : ModuleConsumeResource
{
    public ModuleWeaponWrapper WeaponWrapper;

    public CustomGauge[] _gauges = Array.Empty<CustomGauge>();

    public CustomGaugeSerializer CustomGauge
    { 
        set
        {
            _gauges = new CustomGauge[1] { value.GaugeObject.gameObject.AddComponent<CustomGauge>() };
            _gauges[0].ApplyData(value);
        } 
    }

    public CustomGaugeSerializer[] CustomGauges
    {
        set
        {
            _gauges = new CustomGauge[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                _gauges[i] = value[i].GaugeObject.gameObject.AddComponent<CustomGauge>();
                _gauges[i].ApplyData(value[i]);
            }
        }
    }

    public bool IsContinuous => WeaponWrapper.WrapType == ModuleWeaponWrapper.WeaponType.Continuous;

    public void PrePool()
    {
        WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();

        // Backup fallback
        foreach (CustomGauge gauge in _gauges)
        {
            if (gauge.MinValue == gauge.MaxValue && gauge.MinValue == 0f)
            {
                gauge.MinValue = 0f;
                gauge.MaxValue = MaxValue;
            }
            else
            {
                //if (gauge.MaxValue == 0f) gauge.MaxValue = MaxValue;
                if (gauge.MinValue == gauge.MaxValue) gauge.MinValue -= 0.01f;
            }
        }
    }

    public void OnPool()
    {
        //WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        //if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();
        ConsumeEvent += CheckIfCanFire;
        WeaponWrapper.FireEvent += OnFire;
        WeaponWrapper.LockFiring(this, true);
    }

    public void OnSpawn()
    {
        CurrentValue = 0f;
        if (_gauges.Length != 0)
            foreach (var gauge in _gauges)
                gauge.HardSet(0f);
    }

    void OnFire(int amount)
    {
        float cost = -amount;
        if (IsContinuous)
            cost *= Time.deltaTime;
        CurrentValue += cost;
        if (CurrentValue < 0f) CurrentValue = 0f;
        CheckIfCanFire(cost);
    }

    void CheckIfCanFire(float _)
    {
        if (_gauges.Length != 0)
            foreach (var gauge in _gauges)
                gauge.Set(CurrentValue);
        WeaponWrapper.LockFiring(this, IsContinuous ? (CurrentValue <= 0f) : (CurrentValue < 1f));
    }
}

#endregion

#region Utility

public struct CustomGaugeSerializer
{
    public Transform GaugeObject;
    public Vector3 PositionMin, PositionMax;
    public Vector3 ScaleMin, ScaleMax;
    public Vector3 RotationMin, RotationMax;
    public Color ColorMin, ColorMax;
    public float EnableAt;
    public float ParticlesAt;
    public float Dampen;
    public float MinValue, MaxValue;
    public float MinEnergy { set => MinValue = value; }
    public float MaxEnergy { set => MaxValue = value; }
}

public class CustomGauge : MonoBehaviour
{
    public Vector3 PositionMin, PositionMax;
    public Vector3 ScaleMin, ScaleMax;
    public Vector3 RotationMin, RotationMax;
    public Color ColorMin, ColorMax;
    public float EnableAt;
    public float ParticlesAt;
    public float MinValue, MaxValue = 1f;

    public float Dampen;

    float _target, _value;
    Renderer[] _renderers;
    Renderer[] Renderers
    {
        get
        {
            if (_renderers == null)
            {
                _renderers = GetComponents<Renderer>();
                if (_renderers.Length == 0)
                    GaugeType ^= AnimType.Color;
            }
            return _renderers;
        }
    }

    ParticleSystem[] _particles;
    ParticleSystem[] Particles
    {
        get
        {
            if (_particles == null)
            {
                _particles = GetComponentsInChildren<ParticleSystem>();
                if (_particles.Length == 0)
                    GaugeType ^= AnimType.Particles;
            }
            return _particles;
        }
    }

    public void ApplyData(CustomGaugeSerializer data)
    {
        PositionMin = data.PositionMin; PositionMax = data.PositionMax;
        ScaleMin = data.ScaleMin; ScaleMax = data.ScaleMax;
        RotationMin = data.RotationMin; RotationMax = data.RotationMax;
        ColorMin = data.ColorMin; ColorMax = data.ColorMax;
        EnableAt = data.EnableAt;
        ParticlesAt = data.ParticlesAt;
        MinValue = data.MinValue;
        MaxValue = data.MaxValue;
        SetType();
        Dampen = data.Dampen;
    }

    public AnimType GaugeType = AnimType.Invalid;

    void SetType()
    {
        GaugeType = (PositionMax != PositionMin ? AnimType.Translate : 0) |
            (ScaleMax != ScaleMin ? AnimType.Scale : 0) |
            (RotationMax != RotationMin ? AnimType.Rotate : 0) |
            (ColorMax != ColorMin ? AnimType.Color : 0) |
            (EnableAt > 0f ? AnimType.Enable : 0) |
            (ParticlesAt > 0f ? AnimType.Particles : 0);
    }

    [Flags]
    public enum AnimType
    {
        Invalid = -1,
        None = 0,
        Translate = 1,
        Scale = 2,
        Rotate = 4,
        Color = 8,
        Enable = 16,
        Particles = 32
    }

    public float Remap(float value)
    {
        return Mathf.Clamp01((value - MinValue) / (MaxValue - MinValue));
    }

    public void HardSet(float ratio)
    {
        enabled = false;
        SetRatio(ratio);
        _target = ratio;
        _value = ratio;
    }

    public void Set(float value)
    {
        float ratio = Remap(value);
        _target = ratio;

        if (Dampen != 0f)
            enabled = !_value.Approximately(_target);
        else if (!_value.Approximately(_target))
        {
            SetRatio(ratio);
            _value = ratio;
        }
    }

    void Update()
    {
        if (_value.Approximately(_target))
        {
            enabled = false;
            _value = _target;
        }
        else
        {
            float dampen = Dampen * Time.deltaTime;
            _value = (_target * (1f - dampen)) + (_value * dampen);
        }
        SetRatio(_value);
    }

    void SetRatio(float ratio)
    {
        if (GaugeType == AnimType.Invalid) SetType();
        
        if ((GaugeType & AnimType.Translate) != 0)
            transform.localPosition = Vector3.LerpUnclamped(PositionMin, PositionMax, ratio);
        
        if ((GaugeType & AnimType.Scale) != 0)
            transform.localScale = Vector3.LerpUnclamped(ScaleMin, ScaleMax, ratio);
        
        if ((GaugeType & AnimType.Rotate) != 0)
            transform.localEulerAngles = Vector3.LerpUnclamped(RotationMin, RotationMax, ratio);
        
        if ((GaugeType & AnimType.Color) != 0)
            foreach (var renderer in Renderers)
                renderer.material.color = Color.LerpUnclamped(ColorMin, ColorMax, ratio);
        
        if ((GaugeType & AnimType.Enable) != 0)
            gameObject.SetActive(EnableAt == 1f ? ratio.Approximately(1f, 0.003f) : ratio >= EnableAt);

        if ((GaugeType & AnimType.Particles) != 0)
            if (ParticlesAt == 1f ? ratio.Approximately(1f, 0.003f) : ratio >= ParticlesAt)
                foreach (var particle in Particles)
                    particle.Play();
            else
                foreach (var particle in Particles)
                    particle.Stop();
    }
}

public class ModuleWeaponWrapper : Module, IModuleWeapon
{
    public bool AllowedToFire => FireLockers.Count == 0;

    private List<Component> FireLockers = new List<Component>();

    public bool IsLockedBy(Component lockingModule) => FireLockers.Contains(lockingModule);

    public void LockFiring(Component lockingModule, bool state)
    {
        if (state)
        {
            if (!FireLockers.Contains(lockingModule))
            FireLockers.Add(lockingModule);
        }
        else
        {
            FireLockers.Remove(lockingModule);
        }
    }

    public IModuleWeapon WrappedModule;
    public ModuleWeapon WeaponModule;
    public WeaponType WrapType { get; private set; }
    /// <summary>
    /// <see cref="int"/> : EmitCount
    /// </summary>
    public Action<int> FireEvent;
    public Action CanFireEvent;
    public int FireCount;

    public enum WeaponType : byte
    {
        Pulse,
        Continuous
    }

    /// <summary>
    /// (<see cref="IModuleWeapon"/>) <see cref="ModuleWeapon"/>.m_WeaponComponent
    /// </summary>
    FieldInfo m_WeaponComponent = typeof(ModuleWeapon).GetField("m_WeaponComponent", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

    bool WrapOnSpawn = false;
    void OnPool()
    {
        WeaponModule = GetComponent<ModuleWeapon>();
        if (WeaponModule == null)
        {
            Console.WriteLine($"{name}.ModuleWeaponWrapper.OnPool() : There is no ModuleWeapon here!");
            Destroy(this);
            return;
        }
        WrapOnSpawn = true;
        GetTargetForWrap();
    }

    void OnSpawn()
    {
        if (WrapOnSpawn)
        {
            WrapOnSpawn = false;
            m_WeaponComponent.SetValue(WeaponModule, this);
        }
    }

    void GetTargetForWrap()
    {
        foreach (var weapon in GetComponents<IModuleWeapon>())
        {
            if (weapon is ModuleWeaponFlamethrower /*weaponFlamethrower*/)
            {
                WrapType = WeaponType.Continuous;
                WrappedModule = weapon;
                return;
            }
            if (weapon is ModuleWeaponGun weaponGun)
            {
                WrapType = weaponGun.GetComponentInChildren<BeamWeapon>() != null ? WeaponType.Continuous : WeaponType.Pulse;
                WrappedModule = weapon;
                return;
            }
        }
        Console.WriteLine($"{name}.ModuleWeaponWrapper.GetTargetForWrap() : There is no ModuleWeapon____ here to wrap!");
        Destroy(this);
        return;
    }

    // Active wrapper
    public int ProcessFiring(bool firing)
    {
        int projectileCount = WrappedModule.ProcessFiring(firing);
        FireEvent?.Invoke(projectileCount);
        FireCount += projectileCount;
        return projectileCount;
    }
    public bool ReadyToFire()
    {
        CanFireEvent?.Invoke();
        return AllowedToFire // Very important
            && WrappedModule.ReadyToFire();
    }

    // Passive wrapper
    public bool Deploy(bool deploy) => WrappedModule.Deploy(deploy);
    public bool PrepareFiring(bool prepareFiring) => WrappedModule.PrepareFiring(prepareFiring);
    public bool FiringObstructed() => WrappedModule.FiringObstructed();
    public bool IsAimingAtFloor(float limitedAngle) => WrappedModule.IsAimingAtFloor(limitedAngle);
    public float GetVelocity() => WrappedModule.GetVelocity();
    public float GetRange() => WrappedModule.GetRange();
    public bool AimWithTrajectory() => WrappedModule.AimWithTrajectory();
    public Transform GetFireTransform() => WrappedModule.GetFireTransform();
    public float GetFireRateFraction() => WrappedModule.GetFireRateFraction();
}

public class ModuleConsumeResource : Module
{
    public float MaxValue = 3f;
    //public float ConsumeCooldown = 0f;
    public int HolderCapacity = 3;

    public static int[] GetAPIndices(TankBlock block, Vector3 Position)
    {
        List<int> list = new List<int>();
        for (int k = 0; k < block.attachPoints.Length; k++)
        {
            Vector3 input = block.attachPoints[k] - Position;
            if (input.sqrMagnitude < 1f && input.SetY(0f).sqrMagnitude > 0.1f)
                list.Add(k);
        }
        return list.ToArray();
    }

    public struct ChunkInputStack
    {
        public Vector3 Position;
        public Vector3 ConsumePosition;
        public int[] APIndices;
    }

    public ChunkInputStack[] InputStacks // Write-only!
    {
        set
        {
            // 'Serialize' the stuff
            _overridePositions = new Vector3[value.Length + 1];
            _endPositions = new Vector3[value.Length];
            _overrideConnections = new ModuleItemHolder.APOverrideCollection[value.Length + 1];
            for (int i = 0; i < value.Length; i++)
            {
                _overridePositions[i] = value[i].Position;
                _endPositions[i] = value[i].ConsumePosition;
                _overrideConnections[i] = new ModuleItemHolder.APOverrideCollection { indices = value[i].APIndices };
            }
            _overridePositions[value.Length] = Vector3.one * 100;
            _overrideConnections[value.Length] = new ModuleItemHolder.APOverrideCollection { indices = Array.Empty<int>() };
        }
    }

    public ChunkInputStack InputStack { set => InputStacks = new ChunkInputStack[] { value }; } // Write-only!

    public ChunkTypes[] AcceptedChunks = new ChunkTypes[] { ChunkTypes.Wood, ChunkTypes.RubberJelly };
    public float[] ValuePerChunk = new float[] { 1f, 0.5f };

    public TechAudio.SFXType ConsumeSFXType = TechAudio.SFXType.ItemResourceProduced;

    public float CurrentValue { get; set; } = 0f;

    /// <summary>
    /// <see cref="float"/> : AddedValue
    /// </summary>
    public Action<float> ConsumeEvent;

    ModuleItemHolder _Holder;
    ModuleItemHolderBeam _Beam;
    ModuleItemHolder.StackHandle[] _Stacks;
    ModuleItemHolder.StackHandle _EndStack;

    const BindingFlags BF = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
    /// <summary>
    /// <see cref="Vector3"/>[]
    /// </summary>
    static FieldInfo OBP = typeof(ModuleItemHolder).GetField("m_OverrideBasePositons", BF); // why is this spelt Positon
    /// <summary>
    /// <see cref="ModuleItemHolder.APOverrideCollection"/>[]
    /// </summary>
    static FieldInfo OAP = typeof(ModuleItemHolder).GetField("m_OverrideAPConnections", BF); // why does the name not match the type

    [SerializeField]
    ModuleItemHolder.APOverrideCollection[] _overrideConnections;
    [SerializeField]
    Vector3[] _overridePositions;
    [SerializeField]
    Vector3[] _endPositions;

    static AnimationCurve easeouthalf = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.4f, 0f, 0f, 1f),
        new Keyframe(1f, 1f, 0f, 0f)
        );

    struct ConsumeAnim
    {
        public Transform Body;
        public Vector3 StartPos;
        public Vector3 EndPos;
    }
    Queue<ConsumeAnim> anims = new Queue<ConsumeAnim>();

    void Update()
    {
        foreach(var anim in anims)
        {
            var holders = block.tank.Holders;
            float temp = holders.CurrentHeartbeatInterval, Ratio;
            if (temp == 0f) Ratio = 1f;
            else Ratio = easeouthalf.Evaluate((holders.NextHeartBeatTime - Time.time) / temp);

            var tr = anim.Body;
            tr.position = transform.TransformPoint(Vector3.Lerp(anim.EndPos, anim.StartPos, Ratio));
            tr.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, Ratio);
        }
    }

    void PrePool()
    {
        // If CapacityPerChunk is not the same length as AcceptedChunks, resize and populate
        int a = AcceptedChunks.Length, c = ValuePerChunk != null ? ValuePerChunk.Length : 0;
        if (c != a)
        {
            float lastValue;
            if (c == 0)
            {
                lastValue = 1f;
                ValuePerChunk = new float[a];
            }
            else
            {
                lastValue = ValuePerChunk[c - 1];
                Array.Resize(ref ValuePerChunk, a);
            }
            for (int i = c; i < a; i++)
            {
                ValuePerChunk[i] = lastValue;
            }
        }

        _Holder = gameObject.AddComponent<ModuleItemHolder>();

        // Get the beam from a block with it set right
        _Beam = gameObject.AddComponent<ModuleItemHolderBeam>();
        GameObjectJSON.ShallowCopy(typeof(ModuleItemHolderBeam), ManSpawn.inst.GetBlockPrefab(BlockTypes.GSOSilo_111).GetComponent<ModuleItemHolderBeam>(), _Beam, new string[]{
            "m_BeamStrength",
            "m_BeamBaseHeight",
            "m_BeamQuadPrefab",
            "m_BeamColumnRadius",
            "m_HeightIncrementScale",
            "m_HoldingParticlesPrefab"
        });

        _Holder.OverrideStackCapacity(HolderCapacity);
        for (int i = 0; i < _overrideConnections.Length; i++)
        {
            if (_overrideConnections[i].indices == null)
                _overrideConnections[i] = new ModuleItemHolder.APOverrideCollection { indices = GetAPIndices(block, _overridePositions[i]) };
        }

        OBP.SetValue(_Holder, _overridePositions);
        OAP.SetValue(_Holder, _overrideConnections);
    }

    void OnPool()
    {
        _Beam = gameObject.GetComponent<ModuleItemHolderBeam>();
        _Holder = gameObject.GetComponent<ModuleItemHolder>();
        _Holder.SetAcceptFilterCallback(new Func<Visible, ModuleItemHolder.Stack, ModuleItemHolder.Stack, ModuleItemHolder.PassType, bool>(this.CanAcceptItem), false);
        _Holder.PreDetachEvent.Subscribe(new Action<int>(OnPreDetach));
        _Stacks = new ModuleItemHolder.StackHandle[_overridePositions.Length];
        for (int i = 0; i < _overridePositions.Length; i++)
        {
            _Stacks[i] = new ModuleItemHolder.StackHandle() { localPos = _overridePositions[i] };
            _Stacks[i].InitReference(_Holder);
        }
        _EndStack = new ModuleItemHolder.StackHandle() { localPos = Vector3.one * 100 };
        _EndStack.InitReference(_Holder);

        block.AttachEvent.Subscribe(OnAttach);
        block.DetachEvent.Subscribe(OnDetach);
    }

    void OnPreDetach(int _)
    {
        foreach (var stack in _Stacks)
            DropAllStackItems(stack.stack);
        ClearOutStack(_EndStack.stack);
        anims.Clear();
    }

    float GetExpectedValueFromStacks()
    {
        float result = 0f;
        foreach (var item in _EndStack.stack.IterateItems())
        {
            TryGetIndexOfChunk((ChunkTypes)item.ItemType, out int index);
            result += ValuePerChunk[index];
        }
        foreach (var stack in _Stacks)
            foreach (var item in stack.stack.IterateItems())
            {
                TryGetIndexOfChunk((ChunkTypes)item.ItemType, out int index);
                result += ValuePerChunk[index];
            }
        return result;
    }

    private bool CanAcceptItem(Visible item, ModuleItemHolder.Stack fromStack, ModuleItemHolder.Stack toStack, ModuleItemHolder.PassType passType) =>
        (
            (passType & ModuleItemHolder.PassType.Pass) == 0                                // |   If not pass
            ||                                                                              // OR
            _EndStack.stack != toStack                                                      // |   Not to EndStack

        )
        &&                                                                              // AND
        ( 
            passType == (ModuleItemHolder.PassType.Pass | ModuleItemHolder.PassType.Test)   // |   Is pass and test
            ||                                                                              // OR
            (
                TryGetIndexOfChunk((ChunkTypes)item.ItemType, out int i)                    // |   |   Accepts type
                &&                                                                          // |   AND
                !(toStack.IsFull || CurrentValue + GetExpectedValueFromStacks()             // |   |   Not full
                    + ValuePerChunk[i] > MaxValue)                                          // |   |   or will pass limit
            )
        );

    private void ClearOutStack(ModuleItemHolder.Stack stack)
    {
        if (stack != null)
        {
            while (!stack.IsEmpty)
            {
                Visible firstItem = stack.FirstItem;
                firstItem.SetHolder(null, true, false, true);
                firstItem.trans.Recycle();
            }
        }
    }

    private void DropAllStackItems(ModuleItemHolder.Stack stack)
    {
        if (stack != null)
        {
            while (!stack.IsEmpty)
            {
                stack.FirstItem.SetHolder(null, true, false, true);
            }
        }
    }

    void OnDetach()
    {
        block.tank?.Holders.UnregisterOperations(_Holder);
    }

    void OnAttach()
    {
        var Holders = block.tank.Holders;
        Holders.RegisterOperation(_Holder, new Func<TechHolders.OperationResult>(OnConsumeInput), 7);   //7
        Holders.RegisterOperation(_Holder, new Func<TechHolders.OperationResult>(OnPullInput), 6);      //6
        Holders.RegisterOperation(_Holder, new Func<TechHolders.OperationResult>(OnProcessInput), 8);   //8
    }

    TechHolders.OperationResult OnPullInput()
    {
        var result = TechHolders.OperationResult.None;

        foreach (var stack in _Stacks)
            result |= StackPullInput(stack.stack);
        
        return result;
    }
    TechHolders.OperationResult StackPullInput(ModuleItemHolder.Stack inputStack)
    {
        var result = TechHolders.OperationResult.None;
        foreach (ModuleItemHolder.Stack connectedStack in inputStack.ConnectedStacks)
        {
            foreach (Visible item in connectedStack.IterateItemsIncludingLinkedStacks(0))
            {
                result = inputStack.TryTakeOnHeartbeat(item);
                if (result != TechHolders.OperationResult.None)
                    return result;
            }
        }
        return result;
    }
    TechHolders.OperationResult OnConsumeInput()
    {
        var consumeStack = _EndStack.stack;

        if (!consumeStack.IsEmpty && !consumeStack.ReceivedThisHeartbeat)
            return TechHolders.OperationResult.Retry;

        var result = TechHolders.OperationResult.None;

        for (int i = 0; i < _Stacks.Length; i++)
            result |= StackConsumeInput(_Stacks[i].stack, i);

        return result;
    }
    TechHolders.OperationResult StackConsumeInput(ModuleItemHolder.Stack inputStack, int index)
    {
        if (inputStack.ReceivedThisHeartbeat)
            return TechHolders.OperationResult.None;
        if (inputStack.IsEmpty)
            return TechHolders.OperationResult.Retry;

        var item = inputStack.FirstItem;
        _Beam.ConfigureStack(_EndStack.stack.GetStackIndex(), false, ModuleItemHolderBeam.ItemMovementType.Static);
        _EndStack.stack.Take(item, true, true);
        TechAudio.AudioTickData data = TechAudio.AudioTickData.ConfigureOneshot(this, ConsumeSFXType);
        block.tank.TechAudio.PlayOneshot(data, null);
        item.EnablePhysics(false, false);
        anims.Enqueue(new ConsumeAnim { Body = item.trans, StartPos = transform.InverseTransformPoint(item.trans.position), EndPos = _endPositions[index] });
        return TechHolders.OperationResult.Effect;
    }

    TechHolders.OperationResult OnProcessInput()
    {
        var consumeStack = _EndStack.stack;
        if (/*!consumeStack.ReceivedThisHeartbeat && */!consumeStack.IsEmpty)
        {
            while (!consumeStack.IsEmpty)
            {
                var item = consumeStack.FirstItem;
                TryGetIndexOfChunk((ChunkTypes)item.ItemType, out int i);

                //anims.Dequeue();
                CurrentValue += ValuePerChunk[i];
                ConsumeEvent?.Invoke(ValuePerChunk[i]);
                item.trans.Recycle();
            }
            return TechHolders.OperationResult.Effect;
        }
        anims.Clear();
        return TechHolders.OperationResult.None;
    }

    bool TryGetIndexOfChunk(ChunkTypes chunk, out int index)
    {
        index = -1;
        for (int i = 0; i < AcceptedChunks.Length; i++)
        {
            if (AcceptedChunks[i] == chunk)
            {
                index = i;
                return true;
            }
        }
        return false;
    }
}

#endregion