using System.Collections;
using System.IO;
using Flow.StatusEffects;
using UnityEngine;
using UnityEngine.UI;

namespace Minimap.Behaviours;

[DisallowMultipleComponent]
public class MinimapCameraComponent : MonoBehaviour
{
    public static MinimapCameraComponent Instance { get; private set; }
    
    private static readonly Vector2 MinimapSize = new(160f, 160f);
    private static readonly Vector2 OverlaySize = new(250f, 250f);
    private static readonly int DefaultMask = LayerMask.GetMask(
        "Default", 
        "Terrain", 
        "Water", 
        "Player", 
        "InteractIgnore", 
        "Construct",
        "Decoration"
    );

    private static readonly float OrthographicSizeFactor = 0.25f;

    private const string OrthographicWaterShader = "Legacy Shaders/Diffuse";
    private const string OrthographicShader = "Legacy Shaders/Diffuse";
    private const int OrthographicWaterLayer = 6;
    
    private const float MinCameraHeight = 30f;
    private const float MaxCameraHeight = 100f;
    private const float ZoomStep = 10f;

    private static bool RotateWithPlayer => MinimapPlugin.Instance.RotateWithPlayer.Value;

    private float _cameraHeightTarget = 60f;
    private float _cameraHeight = 60f;
    private bool _wasCompassReplaced;

    private RenderTexture _minimapRenderTexture;
    private GameObject _minimapContainer;
    private Transform _cameraTransform;
    private GameObject _overlayRoot;
    private RawImage _minimapImage;
    private Camera _minimapCamera;
    private GameObject _waterPlane;

    private void Start()
    {
        if (Camera.main)
        {
            _cameraTransform = Camera.main.transform;
        }

        if (!_cameraTransform)
        {
            MinimapPlugin.Logger.LogError("Could not find main camera.");
            enabled = false;
            return;
        }

        CreateMinimapUI();
        CreateMinimapCamera();
        InitializeBrightnessControl();

        if (Instance)
        {
            MinimapPlugin.Logger.LogWarning("Multiple Minimap instances detected, something has likely gone wrong.");
            MinimapPlugin.Logger.LogWarning("Existing instances will be disposed of; if you have enabled compass replacement your UI may adjust briefly.");
            Destroy(Instance);
        }
        
        Instance = this;
    }

    private Material _brightnessMaterial;
    private TOD_Sky _todSky;
    
    private void InitializeBrightnessControl()
    {
        _todSky = FindObjectOfType<TOD_Sky>();
        if (!_todSky)
        {
            MinimapPlugin.Logger.LogWarning("Could not find Sky controller, minimap brightness will be static");
        } 
        
        var brightnessShader = Shader.Find("UI/Default");
        if (!brightnessShader)
        {
            MinimapPlugin.Logger.LogError("Could not find UI/Default shader for brightness control");
            return;
        }
        
        _brightnessMaterial = new Material(brightnessShader);
        _minimapImage.material = _brightnessMaterial;

        if (!_todSky) return;
        
        TOD_Time.OnHour += UpdateMinimapBrightness;
        UpdateMinimapBrightness();
    }

    private void UpdateMinimapBrightness()
    {
        if (!_todSky || !_brightnessMaterial) return;
        
        var brightness = 1.0f;
        if (_todSky && _todSky.IsNight)
        {
            brightness = 1.15f;
        }
        
        _brightnessMaterial.color = new Color(brightness, brightness, brightness, 1f);
    }

    private void CreateMinimapUI()
    {
        var compass = FindObjectOfType<CompassSpinner>(true);
        if (!compass)
        {
            MinimapPlugin.Logger.LogInfo("Failed to find compass for UI injection");
            return;
        }

        if (MinimapPlugin.Instance.ReplaceCompass.Value)
        {
            StartCoroutine(ReplaceCompass());
        }

        _minimapContainer = new GameObject("MinimapContainer");
        _minimapContainer.transform.SetParent(compass.transform.parent, false);
        var containerRect = _minimapContainer.AddComponent<RectTransform>();
        containerRect.sizeDelta = OverlaySize;
        containerRect.anchorMin = new Vector2(0f, 1f);
        containerRect.anchorMax = new Vector2(0f, 1f);
        containerRect.pivot = new Vector2(0f, 1f);
        containerRect.anchoredPosition = new Vector2(40f, -200f);

        _overlayRoot = new GameObject("MinimapRotatingRoot");
        _overlayRoot.transform.SetParent(_minimapContainer.transform, false);
        var rotatingRect = _overlayRoot.AddComponent<RectTransform>();
        rotatingRect.sizeDelta = OverlaySize;
        rotatingRect.anchorMin = new Vector2(0.5f, 0.5f);
        rotatingRect.anchorMax = new Vector2(0.5f, 0.5f);
        rotatingRect.pivot = new Vector2(0.5f, 0.5f);
        rotatingRect.anchoredPosition = Vector2.zero;

        var minimapRoot = new GameObject("Minimap");
        minimapRoot.transform.SetParent(_overlayRoot.transform, false);
        var minimapRect = minimapRoot.AddComponent<RectTransform>();
        minimapRect.sizeDelta = MinimapSize;
        minimapRect.anchorMin = new Vector2(0.5f, 0.5f);
        minimapRect.anchorMax = new Vector2(0.5f, 0.5f);
        minimapRect.pivot = new Vector2(0.5f, 0.5f);
        minimapRect.anchoredPosition = Vector2.zero;

        var maskImage = minimapRoot.AddComponent<Image>();
        maskImage.sprite = CreateCircleSprite((int)MinimapSize.x);

        var mask = minimapRoot.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var imageObj = new GameObject("MinimapImage");
        imageObj.transform.SetParent(minimapRoot.transform, false);
        _minimapImage = imageObj.AddComponent<RawImage>();
        var imageRect = imageObj.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        var overlayImageObj = new GameObject("MinimapOverlayImage");
        overlayImageObj.transform.SetParent(_overlayRoot.transform, false);
        var overlayImage = overlayImageObj.AddComponent<Image>();
        overlayImage.sprite = LoadSpriteFromFile(@"H:\Minimap\Border\Border.png");

        var overlayImageRect = overlayImageObj.GetComponent<RectTransform>();
        overlayImageRect.sizeDelta = OverlaySize;
        overlayImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayImageRect.pivot = new Vector2(0.5f, 0.5f);
        overlayImageRect.anchoredPosition = Vector2.zero;
    }

    private IEnumerator ReplaceCompass()
    {
        StatusEffectSlots statusEffects = null;
        while (statusEffects is null)
        {
            statusEffects = FindObjectOfType<StatusEffectSlots>();
            yield return new WaitForSecondsRealtime(0.2f);
        }
        
        var compass = FindObjectOfType<CompassSpinner>();
        if (compass)
        {
            compass.gameObject.SetActive(false);
        }
        
        statusEffects.transform.localPosition = statusEffects.transform.localPosition with
        {
            x = 20f
        };

        _wasCompassReplaced = true;
    }

    private static Sprite LoadSpriteFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return null;
        }

        var fileData = File.ReadAllBytes(filePath);
        var texture = new Texture2D((int)OverlaySize.x, (int)OverlaySize.y);

        if (texture.LoadImage(fileData))
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        MinimapPlugin.Logger.LogError("Failed to load image data");
        Destroy(texture);
        return null;
    }

    private void HandleZoomInput()
    {
        if (PauseMenu.Instance && PauseMenu.Instance.IsVisible) return;

        // TODO: Make keybinds configurable
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            _cameraHeightTarget = Mathf.Min(_cameraHeightTarget + ZoomStep, MaxCameraHeight);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            _cameraHeightTarget = Mathf.Max(_cameraHeightTarget - ZoomStep, MinCameraHeight);
        }
    }

    private void SmoothZoom()
    {
        _cameraHeight = Mathf.MoveTowards(_cameraHeight, _cameraHeightTarget, 40f * Time.deltaTime);
    }

    private void CreateMinimapCamera()
    {
        var isOrthographic = MinimapPlugin.Instance.RenderingStyle.Value == MinimapRenderStyle.Orthographic;
        
        _minimapRenderTexture =
            new RenderTexture((int)MinimapSize.x, (int)MinimapSize.y, 16, RenderTextureFormat.ARGB32)
            {
                name = "MinimapRT",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 2
            };
        _minimapRenderTexture.Create();

        var camObj = new GameObject("MinimapCamera");
        camObj.transform.SetParent(transform, false);
        _minimapCamera = camObj.AddComponent<Camera>();
        
        _minimapCamera.orthographic = isOrthographic;
        _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        _minimapCamera.backgroundColor = Color.clear;
        _minimapCamera.nearClipPlane = 1f;
        _minimapCamera.farClipPlane = 600f;
        _minimapCamera.fieldOfView = 45f;
        _minimapCamera.cullingMask = DefaultMask;
        _minimapCamera.targetTexture = _minimapRenderTexture;
        _minimapCamera.depth = -100;
        _minimapCamera.allowHDR = false;
        _minimapCamera.allowMSAA = true;

        if (_minimapCamera.orthographic)
        {
            SwitchToOrthographic();
        }
        else
        {
            SwitchToPerspective();
        }

        _minimapImage.texture = _minimapRenderTexture;
    }

    public void ApplyFlattenShader()
    {
        if (!_minimapCamera) return;
        var orthographicShader = Shader.Find(OrthographicShader);
        if (orthographicShader)
        {
            _minimapCamera.SetReplacementShader(orthographicShader, "RenderType");
        }
        else
        {
            MinimapPlugin.Logger.LogWarning($"Could not find \"{OrthographicShader}\" shader");
        }
    }

    public void RemoveFlattenShader()
    {
        if (!_minimapCamera) return;
        _minimapCamera.ResetReplacementShader();
    }

    public void SwitchToOrthographic()
    {
        if (!_minimapCamera) return;

        _minimapCamera.orthographic = true;
        _minimapCamera.orthographicSize = _cameraHeight * OrthographicSizeFactor;

        if (MinimapPlugin.Instance.Flatten.Value)
        {
            ApplyFlattenShader();
        }
        else
        {
            RemoveFlattenShader();
        }

        _minimapCamera.cullingMask |= 1 << OrthographicWaterLayer; 

        if (!_waterPlane)
        {
            CreateMinimapWaterPlane();
        }
    }

    public void SwitchToPerspective()
    {
        if (!_minimapCamera) return;

        _minimapCamera.orthographic = false;
        _minimapCamera.cullingMask = DefaultMask;
        
        RemoveFlattenShader();

        if (!_waterPlane) return;
        Destroy(_waterPlane);
        _waterPlane = null;
    }

    private void CreateMinimapWaterPlane()
    {
        _waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _waterPlane.name = "MinimapWater";

        var currentCameraPos = _minimapCamera.transform.position;
        _waterPlane.transform.position = new Vector3(currentCameraPos.x, 3.82f, currentCameraPos.z);
        _waterPlane.transform.localScale = new Vector3(200f, 1f, 200f);
        _waterPlane.layer = OrthographicWaterLayer;

        // ReSharper disable once UseObjectOrCollectionInitializer
        var waterMaterial = new Material(Shader.Find(OrthographicWaterShader));
        waterMaterial.color = new Color(0.2f, 0.55f, 0.92f, 1f);
        waterMaterial.renderQueue = 1000;

        var renderer = _waterPlane.GetComponent<Renderer>();
        renderer.material = waterMaterial;
        renderer.enabled = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Destroy(_waterPlane.GetComponent<Collider>());
    }

    private void Update()
    {
        if (!_cameraTransform || !_minimapCamera) return;

        HandleZoomInput();
        SmoothZoom();

        var playerPos2D = Player.PlayerPos2D;
        if (_minimapCamera.orthographic)
        {
            _minimapCamera.orthographicSize = _cameraHeight * OrthographicSizeFactor;
        }

        _minimapCamera.transform.position = new Vector3(playerPos2D.x, _cameraHeight, playerPos2D.y);
        _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (RotateWithPlayer)
        {
            var yaw = _cameraTransform.eulerAngles.y;
            _overlayRoot.transform.localEulerAngles = new Vector3(0f, 0f, yaw - 45f);
        }
        else
        {
            _overlayRoot.transform.localEulerAngles = Vector3.zero;
        }
    }

    // Circular mask
    private static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            name = "MinimapMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        var center = new Vector2(size / 2f, size / 2f);
        var radius = size / 2f;

        for (var y = 0; y < size; ++y)
        {
            for (var x = 0; x < size; ++x)
            {
                var dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[x + y * size] = dist <= radius ? Color.white : Color.clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        Instance = null;
        
        if (_wasCompassReplaced)
        {
            var compass = FindObjectOfType<CompassSpinner>();

            if (compass)
            {
                compass.gameObject.SetActive(true);
            }

            var statusEffects = FindObjectOfType<StatusEffectSlots>();
            if (statusEffects)
            {
                statusEffects.transform.localPosition = statusEffects.transform.localPosition with
                {
                    x = 100f
                };
            }
        }

        if (_todSky)
        {
            TOD_Time.OnHour -= UpdateMinimapBrightness;
            _todSky = null;
        }

        if (_brightnessMaterial)
        {
            Destroy(_brightnessMaterial);
        }

        if (_minimapRenderTexture)
        {
            _minimapRenderTexture.Release();
            Destroy(_minimapRenderTexture);
        }

        if (_minimapCamera)
        {
            Destroy(_minimapCamera.gameObject);
        }

        if (_minimapContainer)
        {
            Destroy(_minimapContainer);
        }

        if (_waterPlane)
        {
            Destroy(_waterPlane);
        }
    }
}