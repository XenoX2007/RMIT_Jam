using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum GestureType
{
    ShakeHead,       // lắc đầu: không
    Nod,             // gật đầu: có
    HandOnShoulder   // đặt tay lên vai (Target = xương vai của người kia)
}

// Một clip cử chỉ trên Gesture Track. Kéo dài / dời clip trong Timeline để đổi lúc làm và làm bao lâu.
// Ease In / Ease Out của clip = cử chỉ bắt đầu và kết thúc mượt.
[Serializable]
public class GestureClip : PlayableAsset, ITimelineClipAsset
{
    public GestureType gesture = GestureType.ShakeHead;
    [Tooltip("Hand On Shoulder only: what the hand reaches for (e.g. the other character's shoulder bone).")]
    public ExposedReference<Transform> target;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<GestureBehaviour>.Create(graph);
        GestureBehaviour behaviour = playable.GetBehaviour();
        behaviour.gesture = gesture;
        behaviour.target = target.Resolve(graph.GetResolver());
        return playable;
    }
}

public class GestureBehaviour : PlayableBehaviour
{
    public GestureType gesture;
    public Transform target;
}
