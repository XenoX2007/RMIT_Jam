using System.Collections;
using UnityEngine;

// Đặt lên đám sương (ParticleSystem). Dissolve() làm sương ngừng sinh, tan dần trong fadeTime rồi tắt hẳn.
[RequireComponent(typeof(ParticleSystem))]
public class FogDissolve : MonoBehaviour
{
    [SerializeField] private float fadeTime = 3f;

    private bool dissolving;

    public void Dissolve()
    {
        if (dissolving || !gameObject.activeInHierarchy) return;
        StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        dissolving = true;
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in systems)
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        // Remember each particle's alpha, then fade them all together
        var particles = new ParticleSystem.Particle[systems.Length][];
        var startAlpha = new float[systems.Length][];
        for (int i = 0; i < systems.Length; i++)
        {
            particles[i] = new ParticleSystem.Particle[systems[i].particleCount];
            int n = systems[i].GetParticles(particles[i]);
            startAlpha[i] = new float[n];
            for (int j = 0; j < n; j++) startAlpha[i][j] = particles[i][j].startColor.a;
        }

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            float k = 1f - t / fadeTime;
            k = k * k * (3f - 2f * k);
            for (int i = 0; i < systems.Length; i++)
            {
                int n = systems[i].GetParticles(particles[i]);
                for (int j = 0; j < n && j < startAlpha[i].Length; j++)
                {
                    Color32 c = particles[i][j].startColor;
                    c.a = (byte)(startAlpha[i][j] * k);
                    particles[i][j].startColor = c;
                }
                systems[i].SetParticles(particles[i], n);
            }
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
