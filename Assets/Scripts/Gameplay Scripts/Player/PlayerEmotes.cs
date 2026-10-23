using UnityEngine;
using UnityEngine.InputSystem;

// Emote board: Tab opens / closes a board with numbered slots.
// Click a number (or press 1-3 while it is open) -> the board closes and the character plays that emote.
// Moving, swimming, throwing or hiding cancels the emote; one-shot emotes (Backflip) end by themselves.
public class PlayerEmotes : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject board;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private Swimming swimming;
    [SerializeField] private PlayerThrower thrower;

    private static readonly int EmoteId = Animator.StringToHash("Emote");

    public static bool BoardOpen { get; private set; }
    public int Playing { get; private set; }   // 0 = none, otherwise the slot number

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (swimming == null) swimming = GetComponent<Swimming>();
        if (thrower == null) thrower = GetComponent<PlayerThrower>();
        BoardOpen = false;
        if (board != null) board.SetActive(false);
    }

    private void OnDisable()
    {
        BoardOpen = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (PauseMenu.IsPaused)
        {
            if (BoardOpen) CloseBoard();
            return;
        }

        if (keyboard.tabKey.wasPressedThisFrame)
        {
            if (BoardOpen) CloseBoard();
            else OpenBoard();
        }

        if (BoardOpen)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Play(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Play(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Play(3);
        }

        if (Playing != 0)
        {
            bool moving = moveAction != null && moveAction.action.ReadValue<Vector2>().sqrMagnitude > 0.01f;
            if (moving || Busy())
            {
                Stop();
            }
            else
            {
                // One-shot emote finished: back to idle
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsTag("Emote") && !info.loop && info.normalizedTime >= 0.98f && !animator.IsInTransition(0))
                    Stop();
            }
        }
    }

    public void OpenBoard()
    {
        if (board == null) return;
        BoardOpen = true;
        board.SetActive(true);
    }

    public void CloseBoard()
    {
        BoardOpen = false;
        if (board != null) board.SetActive(false);
    }

    // Slot buttons call this (1, 2, 3)
    public void Play(int slot)
    {
        CloseBoard();
        if (Busy()) return;

        Playing = slot;
        animator.SetInteger(EmoteId, slot);
    }

    public void Stop()
    {
        Playing = 0;
        animator.SetInteger(EmoteId, 0);
    }

    private bool Busy()
    {
        return (swimming != null && swimming.InWater)
            || (thrower != null && thrower.IsThrowing)
            || InteractionController.CurrentBarrel != null;
    }
}
