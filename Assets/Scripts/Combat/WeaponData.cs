using UnityEngine;

namespace PixelProject.Combat
{
    /// <summary>
    /// ScriptableObject defining weapon properties and behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "New Weapon", menuName = "Pixel Project/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string weaponId;
        public string weaponName;
        [TextArea] public string description;
        public Sprite weaponSprite;
        public Sprite iconSprite;

        [Header("Combat Stats")]
        public float damage = 10f;
        public float fireRate = 2f; // Shots per second
        public float projectileSpeed = 15f;
        public int projectilesPerShot = 1;
        public float spreadAngle = 0f;
        public float accuracy = 2f; // Random spread in degrees
        public bool piercing = false;
        public int piercingCount = 1;

        [Header("Ammo")]
        public bool usesAmmo = true;
        public int magazineSize = 30;
        public float reloadTime = 1.5f;

        [Header("Projectile")]
        public GameObject projectilePrefab;
        public float projectileLifetime = 3f;
        public bool explosive = false;
        public float explosionRadius = 0f;

        [Header("Special")]
        public WeaponAbility secondaryAbility;
        public AudioClip fireSound;
        public AudioClip reloadSound;
        public GameObject muzzleFlashPrefab;

        [Header("Rarity")]
        public WeaponRarity rarity = WeaponRarity.Common;
        public int unlockCost = 0;
    }

    public enum WeaponRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}
