
using System.Collections;
using UnityEngine;

public class HunterVisualFeedback : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color attackColor = Color.green;
    [SerializeField] private Color gatherColor = Color.yellow;

    [Header("Effects")]
    [SerializeField] private float attackFlashDuration = 1f;

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine attackCoroutine;

    private static readonly int BaseColorID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorID =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        Initialize();
        SetColor(normalColor);
    }

    private void Initialize()
    {
        if (propertyBlock != null)
            return;

        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
    }

    public void ShowAttack()
    {
        Initialize();

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackFlash());
    }

    private IEnumerator AttackFlash()
    {
        SetColor(attackColor);

        yield return new WaitForSeconds(attackFlashDuration);

        SetColor(normalColor);
        attackCoroutine = null;
    }

    public void ShowGather()
    {
        Initialize();
        StopAttackEffect();
        SetColor(gatherColor);
    }

    public void ShowNormal()
    {
        Initialize();
        StopAttackEffect();
        SetColor(normalColor);
    }

    private void StopAttackEffect()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = null;
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

