using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using Nuterra.BlockInjector;

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

    public void PrePool()
    {
        WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();
        EnergyStore = GetComponent<ModuleEnergyStore>();
        Energy = GetComponent<ModuleEnergy>();
        EnergyStore.m_Capacity = EnergyCapacity;
    }

    public void OnPool()
    {
        //WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        //if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();
        WeaponWrapper.CanFireEvent += CheckIfCanFire;
        WeaponWrapper.FireEvent += OnFire;
        WeaponWrapper.LockFiring(this, true);
    }

    void OnFire(int amount)
    {
        float cost = amount * EnergyCost;
        if (IsContinuous)
            cost *= Time.deltaTime;
        EnergyStore.AddEnergy(-cost);
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

    public bool IsContinuous => WeaponWrapper.WrapType == ModuleWeaponWrapper.WeaponType.Continuous;

    public void PrePool()
    {
        WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();
    }

    public void OnPool()
    {
        //WeaponWrapper = GetComponent<ModuleWeaponWrapper>();
        //if (WeaponWrapper == null) WeaponWrapper = gameObject.AddComponent<ModuleWeaponWrapper>();
        ConsumeEvent += CheckIfCanFire;
        WeaponWrapper.FireEvent += OnFire;
        WeaponWrapper.LockFiring(this, true);
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
        WeaponWrapper.LockFiring(this, IsContinuous ? (CurrentValue <= 0f) : (CurrentValue < 1f));
    }
}

#endregion

#region Utility

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
            if (weapon is ModuleWeaponFlamethrower weaponFlamethrower)
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
    public int MaxValue = 3;
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
            _overrideConnections[value.Length] = new ModuleItemHolder.APOverrideCollection { indices = new int[0] };
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

    private bool CanAcceptItem(Visible item, ModuleItemHolder.Stack fromStack, ModuleItemHolder.Stack toStack, ModuleItemHolder.PassType passType)
    {
        //Console.WriteLine("Attempting to receive item " + item.ItemType);
        var consumeStack = _EndStack.stack;
        if (toStack == consumeStack)
        {
            //Console.WriteLine("ToStack was consumeStack, false");
            return false;
        }
        if (passType == (ModuleItemHolder.PassType.Pass | ModuleItemHolder.PassType.Test) && item == null)
        {
            //Console.WriteLine("Just passing, true");
            return true;
        }
        if (toStack.IsFull || CurrentValue + GetExpectedValueFromStacks() > MaxValue)
        {
            //Console.WriteLine("inputStack is full, false");
            return false;
        }
        if (TryGetIndexOfChunk((ChunkTypes)item.ItemType, out _))
        {
            //Console.WriteLine("Is accepted type, true");
            return true;
        }
        //Console.WriteLine("Is not accepted type, false");
        return false;
    }

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
        var Holders = block.tank.Holders;
        Holders.UnregisterOperations(_Holder);
    }

    void OnAttach()
    {
        var Holders = block.tank.Holders;
        Holders.RegisterOperation(_Holder, new Func<TechHolders.OperationResult>(OnPullInput), 6);
        Holders.RegisterOperation(_Holder, new Func<TechHolders.OperationResult>(OnConsumeInput), 7);
        Holders.RegisterOperation(_Holder, new Func<TechHolders.OperationResult>(OnProcessInput), 8);
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
        if (!consumeStack.ReceivedThisHeartbeat && !consumeStack.IsEmpty) // Change to if permitted to  c o n s u m e
        {
            while (!consumeStack.IsEmpty)
            {
                var item = consumeStack.FirstItem;
                TryGetIndexOfChunk((ChunkTypes)item.ItemType, out int i);

                CurrentValue += ValuePerChunk[i];
                ConsumeEvent?.Invoke(ValuePerChunk[i]);
                anims.Dequeue();
                item.trans.Recycle();
            }
            return TechHolders.OperationResult.Effect;
        }
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