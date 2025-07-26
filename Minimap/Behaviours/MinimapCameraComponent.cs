using UnityEngine;
using UnityEngine.UI;

namespace Minimap.Behaviours;

[DisallowMultipleComponent]
public class MinimapCameraComponent : MonoBehaviour
{
    private static readonly Vector2 MinimapSize = new(160f, 160f);
    private static bool RotateWithPlayer => MinimapPlugin.Instance.RotateWithPlayer.Value;
    
    private float _cameraHeight = 60f;

    private Camera _minimapCamera;
    private RenderTexture _minimapRenderTexture;
    private RawImage _minimapImage;
    private RectTransform _minimapRect;
    private GameObject _minimapRoot;
    private Transform _cameraTransform;

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
        var compass = FindObjectOfType<CompassSpinner>();
        if (!compass)
        {
            MinimapPlugin.Logger.LogInfo("Failed to find compass for UI injection");
            return;
        }

        _minimapRoot = new GameObject("Minimap");
        _minimapRoot.transform.SetParent(compass.transform.parent, false);
        _minimapRect = _minimapRoot.AddComponent<RectTransform>();
        _minimapRect.sizeDelta = MinimapSize;
        _minimapRect.anchorMin = new Vector2(0f, 1f);
        _minimapRect.anchorMax = new Vector2(0f, 1f);
        _minimapRect.pivot = new Vector2(0f, 1f);
        _minimapRect.anchoredPosition = new Vector2(60f, -200f);

        var maskImage = _minimapRoot.AddComponent<Image>();
        maskImage.sprite = CreateCircleSprite((int)MinimapSize.x);

        var mask = _minimapRoot.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var imageObj = new GameObject("MinimapImage");
        imageObj.transform.SetParent(_minimapRoot.transform, false);
        _minimapImage = imageObj.AddComponent<RawImage>();
        var imageRect = imageObj.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        
        // TODO: Add border overlay
    }

    private void CreateMinimapCamera()
    {
        _minimapRenderTexture = new RenderTexture((int)MinimapSize.x, (int)MinimapSize.y, 16, RenderTextureFormat.ARGB32)
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

        // TODO: Make keybinds configurable, clamp ranges, smooth zoom step.
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            _cameraHeight -= 10;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            _cameraHeight += 10;
        }

        var playerPos2D = Player.PlayerPos2D;
        _minimapCamera.transform.position = new Vector3(playerPos2D.x, _cameraHeight, playerPos2D.y);
        _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (RotateWithPlayer)
        {
            var yaw = _cameraTransform.eulerAngles.y;
            _minimapImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, yaw);
        }
        else
        {
            _minimapImage.rectTransform.localEulerAngles = Vector3.zero;
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
        if (_minimapRenderTexture != null)
        {
            _minimapRenderTexture.Release();
            Destroy(_minimapRenderTexture);
        }

        if (_minimapCamera != null)
        {
            Destroy(_minimapCamera.gameObject);
        }

        if (_minimapRoot != null)
        {
            Destroy(_minimapRoot);
        }
    }
}