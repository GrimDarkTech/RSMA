using RSMA.NetMQ;
using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class RSMACamera : MonoBehaviour
{
    [Header("Camera & Output Settings")]
    public Camera sourceCamera;
    public int width = 640;
    public int height = 480;

    [Header("Stream Settings")]
    [Range(10, 48)]
    public float targetFPS = 30f;
    public int cameraID = 0;

    private RenderTexture _renderTexture;
    private bool _isStreaming = false;

    private void Start()
    {
        InitializeRenderTexture();
        StartStreaming();
    }

    private void InitializeRenderTexture()
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();

        // Настраиваем RenderTexture с RGBA32
        _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1,
            filterMode = FilterMode.Point
        };

        sourceCamera.targetTexture = _renderTexture;
    }

    public void StartStreaming()
    {
        if (_isStreaming) return;
        _isStreaming = true;
        StartCoroutine(StreamLoop());
    }

    public void StopStreaming()
    {
        _isStreaming = false;
    }

    private IEnumerator StreamLoop()
    {
        while (_isStreaming)
        {
            float interval = 1f / targetFPS;

            CaptureFrameRaw();

            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void CaptureFrameRaw()
    {
        // Запрашиваем RGBA32 (нативный формат без сбоев конвертации VRAM)
        AsyncGPUReadback.Request(_renderTexture, 0, TextureFormat.RGBA32, request =>
        {
            if (request.hasError)
            {
                Debug.LogError("[CameraStreamer] Ошибка AsyncGPUReadback!");
                return;
            }

            NativeArray<byte> nativeArray = request.GetData<byte>();
            byte[] rawBytes = nativeArray.ToArray();

            // Отправляем сырой массив RGBA32 в PUB-сокет
            NetMQServer.PublishRaw($"Camera_{cameraID}", rawBytes, typeId: 2);
        });
    }

    private void OnDestroy()
    {
        StopStreaming();
        if (_renderTexture != null)
        {
            if (sourceCamera != null) sourceCamera.targetTexture = null;
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }
}