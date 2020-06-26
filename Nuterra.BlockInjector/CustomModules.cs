using System;
using UnityEngine;

public class ModuleDampener : Module
{
    public bool ApplyAtPosition = false;
    public float Strength = 10f;

    void FixedUpdate()
    {
        if (block.IsAttached && block.tank != null && !block.tank.beam.IsActive)
        {
            var rigidbody = block.tank.rbody;
            Vector3 val = rigidbody.velocity * -Mathf.Min(Strength, rigidbody.mass);
            if (ApplyAtPosition)
                rigidbody.AddForceAtPosition(val, block.CentreOfMass);
            else
                rigidbody.AddForce(val);
        }
    }
}

public class SetfuseTimer : MonoBehaviour
{
    void Spawn()
    {

    }
}

public class ModuleStopSpinnersOnDamage : Module
{
    public Spinner[] targetSpinners = null;
    public bool SetFullSpeedInstead = false;
    void OnPool()
    {
        block.visible.damageable.damageEvent.Subscribe(OnDamage);
        if (targetSpinners == null || targetSpinners.Length == 0)
        {
            targetSpinners = GetComponentsInChildren<Spinner>(true);
        }
    }

    void OnDamage(ManDamage.DamageInfo info)
    {
        foreach(Spinner target in targetSpinners)
        {
            Vector3 axis = target.m_RotationAxis, perp = new Vector3(0f, 1f - axis.y, axis.y);
            float angle = Vector3.SignedAngle(perp, target.trans.localRotation * perp, axis);
            target.SetAutoSpin(SetFullSpeedInstead);
            target.Reset();
            target.SetAngle(angle);
        }
    }
}

public class ModuleSpinWhenAnchored : Module
{
    public Spinner[] targetSpinners = null;
    public bool Invert = false;

    void OnPool()
    {
        block.AttachEvent.Subscribe(OnAttach);
        block.DetachEvent.Subscribe(OnDetach);
        if (targetSpinners == null || targetSpinners.Length == 0)
            targetSpinners = GetComponentsInChildren<Spinner>(true);
    }

    private void OnAttach()
    {
        block.tank.AnchorEvent.Subscribe(OnAnchor);
    }

    private void OnDetach() 
    { 
        if (block.tank != null) block.tank.AnchorEvent.Unsubscribe(OnAnchor); 
    }      

    private void OnAnchor(ModuleAnchor arg1, bool arg2, bool arg3)
    {
        foreach (Spinner target in targetSpinners)
            target.SetAutoSpin(arg2 != Invert);
    }
}

[RequireComponent(typeof(ModuleEnergyStore))]
[RequireComponent(typeof(ModuleEnergy))]
public class ModuleHealOverTime : Module
{
    public ModuleEnergyStore _EnergyStore;
    public ModuleEnergy _Energy;
    public float _timeout;

    public float EnergyDrain = 10f;
    public float HealAmount = 10;
    public float HealDelay = 1.0f;
    public float Capacity = 100f;
    public float EnergyMinLimit = 50f;

    public float ActualCurrentEnergy
    {
        get
        {
            var e = block.tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            return e.storageTotal - e.spareCapacity;
        }
    }

    void Update()
    {
        if (_timeout > 0)
        {
            _timeout -= Time.deltaTime;
        }
        else if (block.tank != null)
        {
            var damageable = block.visible.damageable;
            if (damageable.Health < damageable.MaxHealth - HealAmount)
                OnFire(damageable);
        }
    }

    public void PrePool()
    {
        _EnergyStore = GetComponent<ModuleEnergyStore>();
        _Energy = GetComponent<ModuleEnergy>();
        _EnergyStore.m_Capacity = Capacity;
    }

    public void OnPool()
    {
        block.AttachEvent.Subscribe(OnAttach);
    }

    void OnSpawn()
    {
        _timeout = HealDelay;
    }

    void OnAttach()
    {
        _timeout = HealDelay;
    }

    void OnFire(Damageable damageable)
    {
        _timeout = HealDelay;
        if (ActualCurrentEnergy < EnergyDrain + EnergyMinLimit) return;
        _EnergyStore.AddEnergy(-EnergyDrain);
        damageable.Repair(HealAmount);
        block.visible.KeepAwake();
    }
}

public class ProjectileDamageOverTime : MonoBehaviour
{
    public float DamageOverTime = 50f;
    public int MaxHits = 1;
    public ManDamage.DamageType DamageType = ManDamage.DamageType.Standard;
    public bool FriendlyFire = false;
    public bool DamageTouch = true;
    public bool DamageStuck = true;
    int _CurrentHits;
    Projectile _Projectile;
    bool _stuck;
    Damageable _stuckOn;

    private void OnCollisionStay(Collision collision)
    {
        if (!enabled || !DamageTouch || _CurrentHits > MaxHits) return;

        ContactPoint[] contacts = collision.contacts;
        if (contacts.Length == 0)
            return;

        ContactPoint contactPoint = contacts[0];
        Damageable v = contactPoint.otherCollider.GetComponentInParent<Damageable>();
        if (v == null) v = contactPoint.thisCollider.GetComponentInParent<Damageable>();
        if (v == null) return;

        if (!FriendlyFire)
        {
            TankBlock block = v.Block;
            if (block != null && block.LastTechTeam == _Projectile.Shooter.Team)
                return;
        }
        ManDamage.inst.DealDamage(v, DamageOverTime * Time.fixedDeltaTime, DamageType, this);
        _CurrentHits++;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!enabled || !DamageTouch || _CurrentHits > MaxHits) return;

        Damageable v = other.GetComponentInParent<Damageable>();
        if (v == null) return;

        if (!FriendlyFire)
        {
            TankBlock block = v.Block;
            if (block != null && block.LastTechTeam == _Projectile.Shooter.Team)
                return;
        }
        ManDamage.inst.DealDamage(v, DamageOverTime * Time.fixedDeltaTime, DamageType, this);
        _CurrentHits++;
    }

    private void FixedUpdate()
    {
        if (!enabled) return;
        if (DamageStuck && _Projectile.Stuck && transform.parent != null)
        {
            if (!_stuck)
            {
                _stuckOn = transform.parent.GetComponentInParent<Damageable>();
                if (_stuckOn.Block != null && _stuckOn.Block.LastTechTeam == _Projectile.Shooter.Team)
                _stuck = true;
            }
            if (_stuckOn == null) return;
            _CurrentHits = 1;
            ManDamage.inst.DealDamage(_stuckOn, DamageOverTime * Time.fixedDeltaTime, DamageType, this);
        }
        else
        {
            _CurrentHits = 0;
            _stuck = false;
        }
    }

    void OnPool()
    {
        _Projectile = GetComponent<Projectile>();
    }
}

public class ModuleFloater : MotionBlocks.ModuleFloater { }

// ... 

namespace MotionBlocks
{
    public class ModuleFloater : Module
    {
        public float MinHeight = -85f;
        public float MaxHeight = 400f;
        public float MaxStrength = 14f;
        public float VelocityDampen = 0.08f;
        void FixedUpdate()
        {
            if (block.IsAttached && block.tank != null && !block.tank.beam.IsActive)
            {
                Vector3 blockCenter = block.centreOfMassWorld;
                float blockForce = (MaxStrength / MaxHeight) * (MaxHeight - blockCenter.y)
                      - block.tank.rbody.GetPointVelocity(blockCenter).y * VelocityDampen;
                if (MaxStrength > 0)
                    block.tank.rbody.AddForceAtPosition(Vector3.up * Mathf.Clamp(blockForce, 0f, MaxStrength * 1.25f),
                        blockCenter, ForceMode.Impulse);
                else
                    block.tank.rbody.AddForceAtPosition(Vector3.up * Mathf.Clamp(blockForce, MaxStrength * 1.25f, 0f),
                        blockCenter, ForceMode.Impulse);
            }
        }
    }
}
