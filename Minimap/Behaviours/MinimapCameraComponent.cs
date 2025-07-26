using System.IO;
using Flow.StatusEffects;
using UnityEngine;
using UnityEngine.UI;

namespace Minimap.Behaviours;

[DisallowMultipleComponent]
public class MinimapCameraComponent : MonoBehaviour
{
    private static readonly Vector2 MinimapSize = new(160f, 160f);
    private static readonly Vector2 OverlaySize = new(250f, 250f);
    private static bool RotateWithPlayer => MinimapPlugin.Instance.RotateWithPlayer.Value;

    private const float MinCameraHeight = 30f;
    private const float MaxCameraHeight = 100f;
    private const float ZoomStep = 10f;

    private float _cameraHeightTarget = 60f;
    private float _cameraHeight = 60f;

    private RenderTexture _minimapRenderTexture;
    private GameObject _minimapContainer;
    private Transform _cameraTransform;
    private GameObject _overlayRoot;
    private RawImage _minimapImage;
    private Camera _minimapCamera;

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
    }

    private void CreateMinimapUI()
    {
        // TODO: Add support for toggling replacement of the vanilla compass
        
        var compass = FindObjectOfType<CompassSpinner>(true);
        if (!compass)
        {
            MinimapPlugin.Logger.LogInfo("Failed to find compass for UI injection");
            return;
        }

        var statusEffects = FindObjectOfType<StatusEffectSlots>();
        if (statusEffects)
        {
            statusEffects.transform.localPosition = statusEffects.transform.localPosition with
            {
                x = 20f
            };
        }

        compass.gameObject.SetActive(false);

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
        _minimapRenderTexture =
            new RenderTexture((int)MinimapSize.x, (int)MinimapSize.y, 16, RenderTextureFormat.ARGB32)
            {
                name = "MinimapRT"
            };
        _minimapRenderTexture.Create();

        var camObj = new GameObject("MinimapCamera");
        camObj.transform.SetParent(transform, false);
        _minimapCamera = camObj.AddComponent<Camera>();

        /*
         * Orthographic conflicts with the water shader.
         *
         * Probably possible to patch it, but that's beyond my current understanding.
         */
        _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        _minimapCamera.backgroundColor = Color.clear;
        _minimapCamera.nearClipPlane = 1f;
        _minimapCamera.farClipPlane = 600f;
        _minimapCamera.fieldOfView = 45f;
        _minimapCamera.cullingMask = LayerMask.GetMask("Default", "Terrain", "Water", "Player");
        _minimapCamera.targetTexture = _minimapRenderTexture;
        _minimapCamera.depth = -100;
        _minimapCamera.allowHDR = false;
        _minimapCamera.allowMSAA = false;

        _minimapImage.texture = _minimapRenderTexture;
    }

    private void Update()
    {
        if (!_cameraTransform || !_minimapCamera) return;

        HandleZoomInput();
        SmoothZoom();

        var playerPos2D = Player.PlayerPos2D;
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

        if (_minimapRenderTexture != null)
        {
            _minimapRenderTexture.Release();
            Destroy(_minimapRenderTexture);
        }

        if (_minimapCamera != null)
        {
            Destroy(_minimapCamera.gameObject);
        }

        if (_minimapContainer != null)
        {
            Destroy(_minimapContainer); // Destroy container (which destroys overlay and minimap too)
        }
    }
}