using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum MoveEase
{
    Linear,
    InOut,   // tăng tốc rồi giảm tốc (đi bộ)
    Out      // lao nhanh rồi dừng lại (chạy)
}

// Di chuyển nhân vật từ điểm From tới điểm To trong thời gian của clip.
// Đi thì quay mặt theo hướng đi, tới nơi thì quay theo hướng của điểm To.
// Trước clip đầu tiên nhân vật đứng ở From, sau clip cuối cùng đứng ở To.
// Đổi đường đi bằng cách kéo các điểm (Transform trống) trong scene.
[Serializable]
public class MoveClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> from;
    public ExposedReference<Transform> to;
    public MoveEase ease = MoveEase.InOut;
    [Tooltip("Face the direction of travel while moving (then turn to To's direction at the end).")]
    public bool faceTravel = true;

    public ClipCaps clipCaps => ClipCaps.None;

    // Track tự tính vị trí theo thời gian (MoveMixer), clip chỉ giữ dữ liệu
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return Playable.Create(graph);
    }

    public void Evaluate(Transform target, IExposedPropertyTable resolver, float k)
    {
        Transform a = from.Resolve(resolver), b = to.Resolve(resolver);
        if (a == null || b == null || target == null) return;
        k = Mathf.Clamp01(k);
        float e = ease == MoveEase.InOut ? k * k * (3f - 2f * k)
                : ease == MoveEase.Out ? 1f - (1f - k) * (1f - k)
                : k;
        target.position = Vector3.Lerp(a.position, b.position, e);

        Quaternion start = Yaw(a.rotation);
        Quaternion end = Yaw(b.rotation);
        Vector3 travel = b.position - a.position;
        travel.y = 0f;
        if (!faceTravel || travel.sqrMagnitude < 0.01f)
        {
            target.rotation = Quaternion.Slerp(start, end, e);
            return;
        }
        Quaternion facing = Quaternion.LookRotation(travel);
        if (k < 0.15f) target.rotation = Quaternion.Slerp(start, facing, Smooth(k / 0.15f));
        else if (k > 0.8f) target.rotation = Quaternion.Slerp(facing, end, Smooth((k - 0.8f) / 0.2f));
        else target.rotation = facing;
    }

    static Quaternion Yaw(Quaternion q)
    {
        return Quaternion.Euler(0f, q.eulerAngles.y, 0f);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
