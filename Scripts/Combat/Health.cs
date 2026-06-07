using UnityEngine;
using UnityEngine.Events;

namespace DragonWorld.Combat
{
    public class Health : MonoBehaviour
    {
        [Header("Settings")]
        public float maxHealth = 100f;
        public float currentHealth;
        
        [Header("Regeneration")]
        [Tooltip("If greater than 0, health will regenerate over time")]
        public float regenRate = 0f;
        [Tooltip("Time in seconds after taking damage before regeneration starts")]
        public float regenDelay = 3f;
        
        [Header("Events")]
        public UnityEvent OnDeath;
        public UnityEvent<float> OnDamageTaken;
        public UnityEvent<DamageInfo> OnDamageInfoTaken;
        public UnityEvent<float, float> OnHealthChanged;

        [Header("Audio")]
        [Tooltip("Optional: Sounds to play when taking damage")]
        public AudioClip[] painSounds;
        public AudioSource audioSource;

        private bool _isDead = false;
        private float _timeSinceLastDamage = 0f;
        private GameObject _lastAttacker = null;

        void Awake()
        {
            currentHealth = maxHealth;
        }

        void Start()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        void Update()
        {
            if (_isDead) return;

            if (regenRate > 0 && currentHealth < maxHealth)
            {
                _timeSinceLastDamage += Time.deltaTime;
                if (_timeSinceLastDamage >= regenDelay)
                {
                    currentHealth += regenRate * Time.deltaTime;
                    currentHealth = Mathf.Min(currentHealth, maxHealth);
                    OnHealthChanged?.Invoke(currentHealth, maxHealth);
                }
            }
        }

        public void TakeDamage(float amount)
        {
            if (_isDead) return;

            currentHealth -= amount;
            currentHealth = Mathf.Max(currentHealth, 0);
            _timeSinceLastDamage = 0f;
            
            OnDamageTaken?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            PlayPainSound();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (_isDead) return;

            if (damageInfo.Instigator != null)
            {
                _lastAttacker = damageInfo.Instigator;
            }

            currentHealth -= damageInfo.Damage;
            currentHealth = Mathf.Max(currentHealth, 0);
            _timeSinceLastDamage = 0f;

            // Trigger global AddForce if Rigidbody exists on the same level
            Rigidbody mainRb = GetComponent<Rigidbody>();
            if (mainRb != null && !mainRb.isKinematic)
            {
                // AddForceAtPosition automatically calculates and applies the necessary torque (rotation)
                // based on the impact force and the offset from the Rigidbody's center of mass.
                mainRb.AddForceAtPosition(damageInfo.HitDirection * damageInfo.ImpactForce, damageInfo.HitPoint, ForceMode.Impulse);
            }

            // Hit Stop logic via TimeManager
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.RequestSlowMo("HitStop_" + GetInstanceID(), 0.1f, 0.05f, 50);
            }

            OnDamageTaken?.Invoke(damageInfo.Damage);
            OnDamageInfoTaken?.Invoke(damageInfo);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            PlayPainSound();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Applies damage over time without triggering pain sounds. Used for effects like burning.
        /// </summary>
        /// <param name="amount">The amount of damage to apply.</param>
        public void TakeContinuousDamage(float amount, GameObject instigator = null)
        {
            if (_isDead) return;

            if (instigator != null)
            {
                _lastAttacker = instigator;
            }

            currentHealth -= amount;
            currentHealth = Mathf.Max(currentHealth, 0);
            _timeSinceLastDamage = 0f;

            OnDamageTaken?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            OnDeath?.Invoke();
            Debug.Log($"{gameObject.name} has died.");

            if (_lastAttacker != null)
            {
                DragonWorld.Economy.EconomyEvents.OnDiscreteActionTriggered?.Invoke(_lastAttacker, DragonWorld.Economy.ScoreEventType.EnemyKill_Small, transform.position);
            }
            
            // Optional: trigger death animation or ragdoll via DragonStateManager if needed
        }

        public void Heal(float amount)
        {
            if (_isDead) return;
            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void PlayPainSound()
        {
            if (painSounds != null && painSounds.Length > 0 && audioSource != null)
            {
                AudioClip clip = painSounds[Random.Range(0, painSounds.Length)];
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
