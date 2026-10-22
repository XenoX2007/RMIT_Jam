using UnityEngine;

// Tạo hiệu ứng mưa bằng ParticleSystem ngay khi scene chạy.
// Gắn vào một GameObject con của Main Camera để mưa luôn ở trước camera,
// kể cả khi camera xoay sang màn hình Settings.
public class MenuRain : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] Material rainMaterial;
    [SerializeField] Color rainColor = new Color(0.75f, 0.85f, 1f, 0.35f);
    [SerializeField] Vector2 dropWidth = new Vector2(0.02f, 0.04f);
    [SerializeField] float streakLength = 0.04f;

    [Header("Amount & Motion")]
    [SerializeField] float dropsPerSecond = 1500f;
    [SerializeField] int maxDrops = 5000;
    [SerializeField] float fallSpeed = 25f;
    [SerializeField] float wind = -2f;
    [SerializeField] float lifetime = 1.4f;

    [Header("Area (local to this object)")]
    [SerializeField] Vector3 areaSize = new Vector3(60f, 1f, 35f);

    void Awake()
    {
        if (!TryGetComponent(out ParticleSystem rain))
            rain = gameObject.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = rain.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.duration = 5f;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(dropWidth.x, dropWidth.y);
        main.startColor = rainColor;
        main.maxParticles = maxDrops;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Mưa vẫn rơi khi game bị pause
        main.useUnscaledTime = true;

        var emission = rain.emission;
        emission.enabled = true;
        emission.rateOverTime = dropsPerSecond;

        var shape = rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = areaSize;

        // Rơi thẳng xuống theo trục thế giới, không phụ thuộc góc camera
        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(wind * 0.8f, wind * 1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed * 1.1f, -fallSpeed * 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = streakLength;
        renderer.lengthScale = 1f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (rainMaterial != null)
            renderer.sharedMaterial = rainMaterial;
        else
            Debug.LogWarning("MenuRain: chưa gán Rain Material, hạt mưa có thể hiện màu hồng.", this);

        rain.Play();
    }
}
