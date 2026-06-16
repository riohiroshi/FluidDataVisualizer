using UnityEngine;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshCollider))] // 必須有 MeshCollider 才能獲取 UV
public class FluidDataVisualizer : MonoBehaviour
{
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;

    private static readonly int RippleCenterUVId = Shader.PropertyToID("_RippleCenterUV");
    private static readonly int RippleParamsId = Shader.PropertyToID("_RippleParams");

    [Header("Ripple Animation")]
    public float rippleSpeed = 0.5f;       // 漣漪擴散速度（降低，讓擴散過程可見）
    public float maxRippleStrength = 0.15f; // 最大扭曲強度
    public float rippleFrequency = 40.0f;  // 波紋密集度
    public float rippleRingWidth = 0.05f;  // 環寬度（窄 = 清晰環形）
    public float rippleDecay = 1.2f;       // 衰減速率（越小涟漪持續越久）

    private Vector2 _currentRippleCenter;
    private float _currentRippleTime = 0.0f;
    private float _currentRippleStrength = 0.0f;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        
        // ★ 關鍵：移除 SphereCollider/BoxCollider 等非 Mesh 碰撞體
        // hit.textureCoord 只有 MeshCollider 才會返回正確的 UV
        // 其他 Collider 永遠返回 (0,0)
        var colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            if (col is not MeshCollider)
            {
                Debug.LogWarning($"[FluidDataVisualizer] 移除 {col.GetType().Name}，" +
                                 $"因為 textureCoord 只有 MeshCollider 才能正確返回 UV");
                Destroy(col);
            }
        }
        
        // 確保 MeshCollider 存在
        var meshCol = GetComponent<MeshCollider>();
        if (meshCol == null)
        {
            meshCol = gameObject.AddComponent<MeshCollider>();
        }
    }

    void Update()
    {
        HandleMouseInput();
        UpdateRippleAnimation();
    }

    private void HandleMouseInput()
    {
        // 檢測鼠標左鍵點擊
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 射線檢測，確保點擊到這個物體
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    Vector2 uv = hit.textureCoord;
                    Debug.Log(string.Concat("[FluidDataVisualizer] Hit UV: (", uv.x.ToString("F3"), ", ", uv.y.ToString("F3"), ") | Collider: ", hit.collider.GetType().Name));

                    // ★ 安全檢查：如果 UV 是 (0,0) 且 Collider 不是 MeshCollider，警告
                    if (hit.collider is not MeshCollider)
                    {
                        Debug.LogError("[FluidDataVisualizer] Raycast 命中了非 MeshCollider！" +
                                       "textureCoord 將為 (0,0)，請確保只有 MeshCollider");
                        return;
                    }

                    TriggerRipple(uv);
                }
            }
        }
    }

    /// <summary>
    /// 在指定的 UV 座標觸發漣漪
    /// </summary>
    public void TriggerRipple(Vector2 uvHitPoint)
    {
        Debug.Log(string.Concat("[FluidDataVisualizer] TriggerRipple at UV: (", uvHitPoint.x.ToString("F3"), ", ", uvHitPoint.y.ToString("F3"), ")"));
        _currentRippleCenter = uvHitPoint;
        _currentRippleTime = 0.0f; // 重置時間，讓圈圈從中心開始
        _currentRippleStrength = maxRippleStrength; // 賦予初始強度
    }

    private void UpdateRippleAnimation()
    {
        // 時間推進，漣漪向外擴散
        _currentRippleTime += Time.deltaTime * rippleSpeed;
        
        // 強度隨時間衰減
        if (_currentRippleStrength > 0.001f)
        {
            _currentRippleStrength = Mathf.Lerp(_currentRippleStrength, 0.0f, Time.deltaTime * rippleDecay);
        }
        else
        {
            _currentRippleStrength = 0.0f; // 歸零，確保 GPU 上也清零
        }

        // ★ 每幀都更新 PropertyBlock，確保涟漪消失時 GPU 也收到 strength=0
        Vector4 rippleParams = new Vector4(
            _currentRippleTime, 
            _currentRippleStrength, 
            rippleFrequency, 
            rippleRingWidth
        );

        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetVector(RippleCenterUVId, new Vector4(_currentRippleCenter.x, _currentRippleCenter.y, 0, 0));
        _propBlock.SetVector(RippleParamsId, rippleParams);
        _renderer.SetPropertyBlock(_propBlock);
    }
}