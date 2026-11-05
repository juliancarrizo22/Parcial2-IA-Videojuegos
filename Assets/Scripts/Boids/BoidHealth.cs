

using UnityEngine;

public class BoidHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;

    private BoidVisualFeedback visualFeedback;

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private void Awake()
    {
        visualFeedback = GetComponent<BoidVisualFeedback>();
        Revive();
    }

    public void TakeDamage(int damage)
    {
        if (IsDead)
            return;

        CurrentHealth -= damage;

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            IsDead = true;

            if (visualFeedback != null)
                visualFeedback.ShowDead();

            return;
        }

        if (visualFeedback != null)
            visualFeedback.ShowDamage();
    }

    public void Revive()
    {
        CurrentHealth = maxHealth;
        IsDead = false;

        if (visualFeedback != null)
            visualFeedback.ShowAlive();
    }
}


