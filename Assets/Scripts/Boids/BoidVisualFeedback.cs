
using System.Collections;
using UnityEngine;

public class BoidVisualFeedback : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.blue;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color deadColor = Color.gray;

    [Header("Effects")]
    [SerializeField] private float damageFlashDuration = 0.25f;

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine flashCoroutine;

    private static readonly int BaseColorID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorID =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (propertyBlock != null)
            return;

        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
    }

    public void ShowDamage()
    {
        Initialize();

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(DamageFlash());
    }

    private IEnumerator DamageFlash()
    {
        SetColor(damageColor);

        yield return new WaitForSeconds(damageFlashDuration);

        SetColor(normalColor);
        flashCoroutine = null;
    }

    public void ShowDead()
    {
        Initialize();

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = null;
        SetColor(deadColor);
    }

    public void ShowAlive()
    {
        Initialize();

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = null;
        SetColor(normalColor);
    }

    private void SetColor(Color color)
    {
        Initialize();

        foreach (Renderer currentRenderer in renderers)
        {
            if (currentRenderer == null)
                continue;

            currentRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(BaseColorID, color);
            propertyBlock.SetColor(ColorID, color);

            currentRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}


