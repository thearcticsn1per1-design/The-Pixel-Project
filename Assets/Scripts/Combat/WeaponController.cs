using UnityEngine;
using System.Collections.Generic;
using PixelProject.Core;
using PixelProject.Player;
using PixelProject.Utilities;

namespace PixelProject.Combat
{
    /// <summary>
    /// Controls weapon behavior including firing, reloading, and weapon switching.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private int maxWeaponSlots = 2;
        [SerializeField] private WeaponData startingWeapon;

        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform weaponVisual;

        private List<WeaponInstance> weapons = new List<WeaponInstance>();
        private int currentWeaponIndex = 0;
        private float fireTimer;
        private float reloadTimer;
        private bool isReloading;

        private PlayerStats playerStats;
        private PlayerController playerController;

        public WeaponInstance CurrentWeapon => weapons.Count > 0 ? weapons[currentWeaponIndex] : null;
        public bool IsReloading => isReloading;
        public int CurrentAmmo => CurrentWeapon?.CurrentAmmo ?? 0;
        public int MaxAmmo => CurrentWeapon?.Data.magazineSize ?? 0;

        private void Awake()
        {
            playerStats = GetComponentInParent<PlayerStats>();
            playerController = GetComponentInParent<PlayerController>();
        }

        private void Start()
        {
            if (startingWeapon != null)
            {
                AddWeapon(startingWeapon);
            }
        }

        private void Update()
        {
            UpdateTimers();
            HandleWeaponSwitch();
        }

        private void UpdateTimers()
        {
            if (fireTimer > 0)
            {
                fireTimer -= Time.deltaTime;
            }

            if (isReloading)
            {
                reloadTimer -= Time.deltaTime;
                if (reloadTimer <= 0)
                {
                    CompleteReload();
                }
            }
        }

        private void HandleWeaponSwitch()
        {
            if (weapons.Count <= 1) return;

            // Number keys for direct selection
            for (int i = 0; i < Mathf.Min(weapons.Count, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SwitchToWeapon(i);
                    return;
                }
            }

            // Scroll wheel for cycling
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0)
            {
                SwitchToWeapon((currentWeaponIndex + 1) % weapons.Count);
            }
            else if (scroll < 0)
            {
                SwitchToWeapon((currentWeaponIndex - 1 + weapons.Count) % weapons.Count);
            }
        }

        public void Fire()
        {
            if (CurrentWeapon == null || isReloading || fireTimer > 0) return;

            var weapon = CurrentWeapon;

            // Check ammo
            if (weapon.Data.usesAmmo && weapon.CurrentAmmo <= 0)
            {
                Reload();
                return;
            }

            // Calculate fire rate with player stats
            float attackSpeed = playerStats?.AttackSpeed ?? 1f;
            float fireRate = weapon.Data.fireRate * attackSpeed;
            fireTimer = 1f / fireRate;

            // Consume ammo
            if (weapon.Data.usesAmmo)
            {
                weapon.CurrentAmmo--;
            }

            // Fire projectiles
            FireProjectiles(weapon);

            // Publish event
            EventBus.Publish(new WeaponFiredEvent
            {
                WeaponId = weapon.Data.weaponId,
                Position = firePoint.position,
                Direction = playerController?.AimDirection ?? transform.right
            });
        }

        private void FireProjectiles(WeaponInstance weapon)
        {
            int projectileCount = weapon.Data.projectilesPerShot;

            // Apply player stat bonuses
            if (playerStats != null)
            {
                // Could add projectile count bonus from stats
            }

            Vector2 baseDirection = playerController?.AimDirection ?? transform.right;
            float spreadAngle = weapon.Data.spreadAngle;

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = 0f;

                if (projectileCount > 1)
                {
                    // Spread projectiles evenly
                    float totalSpread = spreadAngle * (projectileCount - 1);
                    angle = -totalSpread / 2f + spreadAngle * i;
                }

                // Add random spread
                angle += Random.Range(-weapon.Data.accuracy, weapon.Data.accuracy);

                Vector2 direction = RotateVector(baseDirection, angle);
                SpawnProjectile(weapon, direction);
            }
        }

        private void SpawnProjectile(WeaponInstance weapon, Vector2 direction)
        {
            GameObject projectileObj = ObjectPool.Instance?.Get(weapon.Data.projectilePrefab);

            if (projectileObj == null)
            {
                projectileObj = Instantiate(weapon.Data.projectilePrefab);
            }

            projectileObj.transform.position = firePoint.position;
            projectileObj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                float damage = CalculateDamage(weapon);
                bool isCrit = CheckCritical();

                if (isCrit)
                {
                    damage *= playerStats?.CritDamage ?? 1.5f;
                }

                projectile.Initialize(direction, weapon.Data.projectileSpeed, damage, isCrit, weapon.Data.piercing);
            }
        }

        private float CalculateDamage(WeaponInstance weapon)
        {
            float baseDamage = weapon.Data.damage;
            float playerDamage = playerStats?.Damage ?? 1f;

            return baseDamage * (playerDamage / 10f); // Normalize player damage
        }

        private bool CheckCritical()
        {
            float critChance = playerStats?.CritChance ?? 0.05f;
            return Random.value < critChance;
        }

        public void SecondaryFire()
        {
            if (CurrentWeapon == null || CurrentWeapon.Data.secondaryAbility == null) return;

            // Execute secondary ability
            CurrentWeapon.Data.secondaryAbility.Execute(gameObject);
        }

        public void Reload()
        {
            if (CurrentWeapon == null || isReloading) return;
            if (!CurrentWeapon.Data.usesAmmo) return;
            if (CurrentWeapon.CurrentAmmo >= CurrentWeapon.Data.magazineSize) return;

            isReloading = true;
            reloadTimer = CurrentWeapon.Data.reloadTime;

            Debug.Log($"Reloading {CurrentWeapon.Data.weaponName}...");
        }

        private void CompleteReload()
        {
            isReloading = false;
            if (CurrentWeapon != null)
            {
                CurrentWeapon.CurrentAmmo = CurrentWeapon.Data.magazineSize;
            }

            Debug.Log("Reload complete!");
        }

        public bool AddWeapon(WeaponData weaponData)
        {
            if (weapons.Count >= maxWeaponSlots)
            {
                // Replace current weapon
                weapons[currentWeaponIndex] = new WeaponInstance(weaponData);
                return true;
            }

            weapons.Add(new WeaponInstance(weaponData));
            return true;
        }

        public void SwitchToWeapon(int index)
        {
            if (index < 0 || index >= weapons.Count) return;
            if (index == currentWeaponIndex) return;

            // Cancel reload when switching
            isReloading = false;
            reloadTimer = 0f;

            currentWeaponIndex = index;
            UpdateWeaponVisual();

            Debug.Log($"Switched to {CurrentWeapon.Data.weaponName}");
        }

        private void UpdateWeaponVisual()
        {
            if (weaponVisual == null || CurrentWeapon == null) return;

            var spriteRenderer = weaponVisual.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && CurrentWeapon.Data.weaponSprite != null)
            {
                spriteRenderer.sprite = CurrentWeapon.Data.weaponSprite;
            }
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }

    [System.Serializable]
    public class WeaponInstance
    {
        public WeaponData Data;
        public int CurrentAmmo;
        public int Level;

        public WeaponInstance(WeaponData data)
        {
            Data = data;
            CurrentAmmo = data.magazineSize;
            Level = 1;
        }
    }
}
