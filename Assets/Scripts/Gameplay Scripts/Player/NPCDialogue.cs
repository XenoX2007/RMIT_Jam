using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Bong bóng hội thoại trên đầu NPC. Viết câu thoại trong Inspector (nhiều câu thì lần lượt đổi câu).
// Hiện lên khi người chơi lại gần (hoặc luôn hiện), chữ chạy từng ký tự, bong bóng luôn quay về camera.
public class NPCDialogue : MonoBehaviour
{
    [Header("What the NPC says")]
    [TextArea(2, 6)]
    [SerializeField] private string[] lines = { "Hello there!" };
    [Tooltip("Seconds each line stays before the next one (when there are several).")]
    [SerializeField] private float secondsPerLine = 4f;

    [Header("When it shows")]
    [SerializeField] private bool alwaysShow = false;
    [Tooltip("Shows when the player is closer than this (metres).")]
    [SerializeField] private float showDistance = 8f;

    [Header("Look")]
    [Tooltip("Height above the head bone (or above the object if it has no head).")]
    [SerializeField] private float heightAboveHead = 0.45f;
    [SerializeField] private float bubbleScale = 0.009f;
    [SerializeField] private float maxWidth = 560f;
    [SerializeField] private float fontSize = 30f;
    [SerializeField] private Color bubbleColor = new Color(0.98f, 0.95f, 0.88f, 0.95f);
    [SerializeField] private Color textColor = new Color(0.15f, 0.1f, 0.06f);
    [SerializeField] private float charsPerSecond = 45f;

    private Transform head;
    private Transform player;
    private Transform bubble;
    private CanvasGroup group;
    private TMP_Text label;
    private RectTransform panel;
    private int lineIndex;
    private float lineTimer;
    private float shownChars;
    private bool showing;

    private void Start()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        BuildBubble();
        SetLine(0);
        group.alpha = 0f;
    }

    // Change what the NPC says from another script
    public void SetLines(params string[] newLines)
    {
        lines = newLines;
        SetLine(0);
    }

    private void LateUpdate()
    {
        if (bubble == null) return;

        // follow the head, face the camera
        Vector3 top = head != null ? head.position : transform.position + Vector3.up * 1.8f;
        bubble.position = top + Vector3.up * heightAboveHead;
        Camera cam = Camera.main;
        if (cam != null) bubble.rotation = Quaternion.LookRotation(bubble.position - cam.transform.position, Vector3.up);

        bool show = alwaysShow || (player != null && Vector3.Distance(player.position, transform.position) < showDistance);
        if (show && !showing) shownChars = 0f;   // retype when it appears again
        showing = show;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.deltaTime * 4f);

        if (!show) return;

        shownChars += charsPerSecond * Time.deltaTime;
        label.maxVisibleCharacters = Mathf.FloorToInt(shownChars);

        if (lines != null && lines.Length > 1)
        {
            lineTimer += Time.deltaTime;
            if (lineTimer >= secondsPerLine) SetLine(lineIndex + 1);
        }
    }

    private void SetLine(int index)
    {
        if (label == null || lines == null || lines.Length == 0) return;
        lineIndex = (index % lines.Length + lines.Length) % lines.Length;
        lineTimer = 0f;
        shownChars = 0f;
        label.text = lines[lineIndex];
        label.maxVisibleCharacters = 0;

        // size the bubble to the text
        Vector2 size = label.GetPreferredValues(lines[lineIndex], maxWidth, 0f);
        size.x = Mathf.Min(size.x, maxWidth);
        size = label.GetPreferredValues(lines[lineIndex], size.x, 0f);
        panel.sizeDelta = new Vector2(Mathf.Min(size.x, maxWidth) + 44f, size.y + 30f);
    }

    private void BuildBubble()
    {
        var canvasGo = new GameObject("Dialogue Bubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        bubble = canvasGo.transform;
        bubble.SetParent(transform, false);
        bubble.localScale = Vector3.one * bubbleScale / Mathf.Max(0.0001f, transform.lossyScale.x);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        group = canvasGo.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        ((RectTransform)bubble).sizeDelta = new Vector2(maxWidth + 60f, 400f);

        Sprite rounded = RoundedSprite();

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel = (RectTransform)panelGo.transform;
        panel.SetParent(bubble, false);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = Vector2.zero;
        var img = panelGo.GetComponent<Image>();
        img.sprite = rounded; img.type = Image.Type.Sliced; img.color = bubbleColor; img.raycastTarget = false;

        // little tail under the bubble, pointing at the head
        var tailGo = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        var tail = (RectTransform)tailGo.transform;
        tail.SetParent(panel, false);
        tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
        tail.sizeDelta = new Vector2(26f, 26f);
        tail.anchoredPosition = new Vector2(0f, -4f);
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var tailImg = tailGo.GetComponent<Image>();
        tailImg.color = bubbleColor; tailImg.raycastTarget = false;
        tail.SetAsFirstSibling();

        var textGo = new GameObject("Text", typeof(RectTransform));
        var tr = (RectTransform)textGo.transform;
        tr.SetParent(panel, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(22f, 15f); tr.offsetMax = new Vector2(-22f, -15f);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize; tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        label = tmp;
    }

    // White rounded rectangle, 9-sliced so the bubble can be any size
    private static Sprite cachedSprite;
    private static Sprite RoundedSprite()
    {
        if (cachedSprite != null) return cachedSprite;
        const int size = 64, radius = 22;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        cachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return cachedSprite;
    }
}
