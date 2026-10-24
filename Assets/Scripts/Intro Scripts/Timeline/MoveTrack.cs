using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Track di chuyển: gắn Transform của "rig" (cha của nhân vật). Nhân vật bên trong vẫn chạy animation riêng.
[TrackColor(0.3f, 0.75f, 1f)]
[TrackClipType(typeof(MoveClip))]
[TrackBindingType(typeof(Transform))]
public class MoveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<MoveMixer>.Create(graph, inputCount);
        var clips = new List<TimelineClip>(GetClips());
        clips.Sort((x, y) => x.start.CompareTo(y.start));
        playable.GetBehaviour().clips = clips;
        return playable;
    }

    // Khi xem thử trong cửa sổ Timeline, Unity trả vị trí về như cũ sau khi thoát xem thử
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        var t = director.GetGenericBinding(this) as Transform;
        if (t != null)
        {
            driver.AddFromName<Transform>(t.gameObject, "m_LocalPosition");
            driver.AddFromName<Transform>(t.gameObject, "m_LocalRotation");
        }
        base.GatherProperties(director, driver);
    }
}

public class MoveMixer : PlayableBehaviour
{
    public List<TimelineClip> clips;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var target = playerData as Transform;
        if (target == null || clips == null || clips.Count == 0) return;
        var director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null) return;
        double time = director.time;

        // Trước clip đầu: đứng ở From. Trong clip: đang đi. Giữa / sau các clip: đứng ở To của clip vừa xong.
        TimelineClip clip = clips[0];
        float k = 0f;
        foreach (TimelineClip c in clips)
        {
            if (time < c.start) break;
            clip = c;
            k = c.duration > 0.0001 ? (float)((time - c.start) / c.duration) : 1f;
        }
        if (clip.asset is MoveClip move) move.Evaluate(target, director, k);
    }
}
