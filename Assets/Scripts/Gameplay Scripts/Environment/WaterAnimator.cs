using UnityEngine;

// Làm mặt nước gợn sóng: cuộn normal map chính và detail normal map theo 2 hướng khác nhau.
// Material cần có _BumpMap và _DetailNormalMap (URP Lit).
[RequireComponent(typeof(Renderer))]
public class WaterAnimator : MonoBehaviour
{
    [Tooltip("World size (metres) of one ripple tile.")]
    [SerializeField] float rippleSize = 10f;
    [Tooltip("Scroll speed of the main ripples, in tiles per second.")]
    [SerializeField] Vector2 flowA = new Vector2(0.02f, 0.012f);
    [Tooltip("Scroll speed of the small ripples, in tiles per second.")]
    [SerializeField] Vector2 flowB = new Vector2(-0.016f, 0.022f);
    [SerializeField] float detailScale = 1.7f;

    static readonly int BaseST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int DetailST = Shader.PropertyToID("_DetailAlbedoMap_ST");

    Renderer rend;
    MaterialPropertyBlock block;
    Vector2 tiling;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();

        // Giữ kích thước gợn sóng như nhau dù mặt nước to hay nhỏ
        Vector3 size = rend.bounds.size;
        tiling = new Vector2(size.x / rippleSize, size.z / rippleSize);
    }

    void Update()
    {
        float t = Time.time;
        Vector2 a = flowA * t, b = flowB * t;

        rend.GetPropertyBlock(block);
        block.SetVector(BaseST, new Vector4(tiling.x, tiling.y, a.x % 1f, a.y % 1f));
        block.SetVector(DetailST, new Vector4(tiling.x * detailScale, tiling.y * detailScale, b.x % 1f, b.y % 1f));
        rend.SetPropertyBlock(block);
    }
}
