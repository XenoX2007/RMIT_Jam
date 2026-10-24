using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Track cử chỉ: gắn một nhân vật (hoặc cả một nhóm người, ví dụ đám đông) có CutsceneActor.
// Cả nhóm sẽ cùng làm cử chỉ (mỗi người lệch nhau một chút theo Gesture Delay).
// Cử chỉ chỉ hiện khi chạy game (Play), không hiện khi kéo thanh thời gian trong Timeline.
[TrackColor(0.95f, 0.6f, 0.2f)]
[TrackClipType(typeof(GestureClip))]
[TrackBindingType(typeof(GameObject))]
public class GestureTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<GestureMixer>.Create(graph, inputCount);
    }
}

public class GestureMixer : PlayableBehaviour
{
    CutsceneActor[] actors;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var root = playerData as GameObject;
        if (root == null) return;
        if (actors == null) actors = root.GetComponentsInChildren<CutsceneActor>(true);

        float shakeW = 0f, shakeT = 0f, nodW = 0f, nodT = 0f, reachW = 0f;
        Transform target = null;
        for (int i = 0; i < playable.GetInputCount(); i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;
            var input = (ScriptPlayable<GestureBehaviour>)playable.GetInput(i);
            GestureBehaviour b = input.GetBehaviour();
            float t = (float)input.GetTime();
            switch (b.gesture)
            {
                case GestureType.ShakeHead: if (w > shakeW) { shakeW = w; shakeT = t; } break;
                case GestureType.Nod: if (w > nodW) { nodW = w; nodT = t; } break;
                case GestureType.HandOnShoulder: if (w > reachW) { reachW = w; target = b.target; } break;
            }
        }

        foreach (CutsceneActor actor in actors)
            if (actor != null) actor.SetGestures(shakeW, shakeT, nodW, nodT, reachW, target);
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (actors == null) return;
        foreach (CutsceneActor actor in actors)
            if (actor != null) actor.SetGestures(0f, 0f, 0f, 0f, 0f, null);
    }
}
