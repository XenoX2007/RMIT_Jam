using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows one head icon per HP. A lost head pops, then fades to a dark ghost.
// Builds the icons from iconTemplate, so changing Max Health on PlayerHealth changes the row.
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [Tooltip("A disabled Image used as the template for each head.")]
    [SerializeField] private Image iconTemplate;
    [SerializeField] private Color fullColor = Color.white;
    [SerializeField] private Color lostColor = new Color(0.15f, 0.15f, 0.15f, 0.45f);
    [SerializeField] private float popScale = 1.35f;
    [SerializeField] private float popTime = 0.35f;

    private readonly List<Image> icons = new List<Image>();
    private int shown = -1;

    private void OnEnable()
    {
        if (health != null) health.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
    }

    private void Start()
    {
        if (health != null) OnHealthChanged(health.Health, health.MaxHealth);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (icons.Count != max) Build(max);

        for (int i = 0; i < icons.Count; i++)
        {
            bool full = i < current;
            // Animate only heads that were just lost
            if (!full && shown > i)
                StartCoroutine(LoseIcon(icons[i]));
            else
            {
                icons[i].color = full ? fullColor : lostColor;
                icons[i].rectTransform.localScale = Vector3.one;
            }
        }
        shown = current;
    }

    private void Build(int max)
    {
        foreach (Image icon in icons)
            if (icon != null) Destroy(icon.gameObject);
        icons.Clear();

        for (int i = 0; i < max; i++)
        {
            Image icon = Instantiate(iconTemplate, iconTemplate.transform.parent);
            icon.name = "Head " + (i + 1);
            icon.gameObject.SetActive(true);
            icons.Add(icon);
        }
        iconTemplate.gameObject.SetActive(false);
        shown = max;
    }

    private IEnumerator LoseIcon(Image icon)
    {
        RectTransform rt = icon.rectTransform;
        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float k = t / popTime;
            k = k * k * (3f - 2f * k);
            // Up to popScale in the first half, back down in the second, fading to lost
            float scale = k < 0.5f ? Mathf.Lerp(1f, popScale, k * 2f) : Mathf.Lerp(popScale, 1f, (k - 0.5f) * 2f);
            rt.localScale = Vector3.one * scale;
            icon.color = Color.Lerp(fullColor, lostColor, k);
            yield return null;
        }
        rt.localScale = Vector3.one;
        icon.color = lostColor;
    }
}
