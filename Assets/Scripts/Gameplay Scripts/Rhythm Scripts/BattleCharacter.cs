using System.Collections;
using UnityEngine;

// Nhân vật trong trận đấu (player hoặc boss): nhún nhẹ khi đứng yên,
// lao về phía đối thủ khi tấn công, rung + chớp đỏ khi bị đánh, ngã khi thua.
public class BattleCharacter : MonoBehaviour
{
    [SerializeField] Transform opponent;

    [Header("Idle")]
    [SerializeField] float idleBobHeight = 0.08f;
    [SerializeField] float idleBobSpeed = 3f;

    [Header("Attack")]
    [SerializeField] float lungeDistance = 1.2f;
    [SerializeField] float lungeDuration = 0.25f;

    [Header("Hurt")]
    [SerializeField] float hurtDuration = 0.3f;
    [SerializeField] float shakeAmount = 0.15f;
    [SerializeField] float knockback = 0.3f;
    [SerializeField] Color hurtColor = new Color(1f, 0.15f, 0.15f);

    [Header("Defeat")]
    [SerializeField] float defeatDuration = 1f;

    [Header("Animation (tùy chọn)")]
    [Tooltip("Animator của model. Để trống nếu nhân vật không có animation.")]
    [SerializeField] Animator animator;
    [SerializeField] string screamState = "Scream";
    [Tooltip("Đang hét chưa tới mốc này (0-1) thì không hét lại từ đầu, để miss liên tiếp không làm giật animation.")]
    [SerializeField, Range(0f, 1f)] float screamRestartAfter = 0.5f;
    [Tooltip("Tiếng gầm phát cùng animation Scream (theo SFX Volume trong menu).")]
    [SerializeField] AudioClip screamSound;
    [SerializeField, Range(0f, 1f)] float screamVolume = 1f;

    AudioSource voice;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock block;

    Vector3 homePosition;
    Quaternion homeRotation;
    Vector3 actionOffset;
    float bobPhase;
    bool defeated;
    Coroutine currentAction;

    void Awake()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        }
    }

    void Update()
    {
        float bob = defeated ? 0f : Mathf.Sin(Time.time * idleBobSpeed + bobPhase) * idleBobHeight;
        transform.position = homePosition + actionOffset + Vector3.up * bob;
    }

    public void Attack()
    {
        if (defeated) return;
        PlayAction(AttackRoutine());
    }

    public void Hurt()
    {
        if (defeated) return;
        PlayAction(HurtRoutine());
    }

    // Boss hét (animation Scream) mỗi khi player bị trừ thanh
    public void Scream()
    {
        if (defeated || animator == null) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName(screamState) && state.normalizedTime < screamRestartAfter) return;
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(screamState)) return;

        animator.CrossFadeInFixedTime(screamState, 0.12f, 0, 0f);
        PlayScreamSound();
    }

    // Gầm lại từ đầu mỗi lần hét (không chồng nhiều tiếng gầm lên nhau)
    void PlayScreamSound()
    {
        if (screamSound == null) return;
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }
        voice.clip = screamSound;
        voice.volume = screamVolume * GameAudio.Sfx;
        voice.Play();
    }

    public void Defeat()
    {
        if (defeated) return;
        defeated = true;
        PlayAction(DefeatRoutine());
    }

    void PlayAction(IEnumerator routine)
    {
        // Hủy động tác cũ để đòn đánh liên tiếp không bị lệch vị trí
        if (currentAction != null)
            StopCoroutine(currentAction);

        actionOffset = Vector3.zero;
        transform.rotation = homeRotation;
        SetFlash(0f);
        currentAction = StartCoroutine(routine);
    }

    Vector3 DirectionToOpponent()
    {
        if (opponent == null) return transform.forward;

        Vector3 dir = opponent.position - homePosition;
        dir.y = 0f;
        return dir.normalized;
    }

    IEnumerator AttackRoutine()
    {
        Vector3 dir = DirectionToOpponent();
        float elapsed = 0f;

        while (elapsed < lungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lungeDuration);

            // 40% đầu lao tới, 60% sau lùi về
            float amount = t < 0.4f ? Smooth(t / 0.4f) : 1f - Smooth((t - 0.4f) / 0.6f);
            actionOffset = dir * lungeDistance * amount;
            yield return null;
        }

        actionOffset = Vector3.zero;
    }

    IEnumerator HurtRoutine()
    {
        Vector3 back = -DirectionToOpponent();
        float elapsed = 0f;

        while (elapsed < hurtDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hurtDuration);
            float strength = 1f - Smooth(t);

            actionOffset = back * knockback * strength + Random.insideUnitSphere * shakeAmount * strength;
            SetFlash(strength);
            yield return null;
        }

        actionOffset = Vector3.zero;
        SetFlash(0f);
    }

    IEnumerator DefeatRoutine()
    {
        Vector3 back = -DirectionToOpponent();
        Vector3 tiltAxis = Vector3.Cross(Vector3.up, back);
        float elapsed = 0f;

        while (elapsed < defeatDuration)
        {
            elapsed += Time.deltaTime;
            float eased = Smooth(Mathf.Clamp01(elapsed / defeatDuration));

            // Ngã ngửa ra sau và chìm nhẹ xuống
            transform.rotation = Quaternion.AngleAxis(80f * eased, tiltAxis) * homeRotation;
            actionOffset = back * 0.5f * eased + Vector3.down * 0.3f * eased;
            SetFlash(1f - eased);
            yield return null;
        }
    }

    void SetFlash(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(baseColors[i], hurtColor, amount));
            renderers[i].SetPropertyBlock(block);
        }
    }

    static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
