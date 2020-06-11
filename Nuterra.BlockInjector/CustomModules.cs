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
        if (!DamageTouch || _CurrentHits > MaxHits) return;

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
        if (!DamageTouch || _CurrentHits > MaxHits) return;

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
        if (DamageStuck && _Projectile.Stuck)
        {
            if (!_stuck)
            {
                _stuckOn = transform.parent.GetComponentInParent<Damageable>();
                _stuck = true;
            }
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
