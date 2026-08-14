using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class RSMACamera : MonoBehaviour
{
    [Header("Camera & Output Settings")]
    public Camera sourceCamera;
    public int width = 640;
    public int height = 480;

    [Header("Stream Settings")]
    [Range(24, 48)]
    public float targetFPS = 30f;
    public int cameraID = 0;

    private RenderTexture _renderTexture;
    private bool _isStreaming = false;
    private uint _frameCounter = 0;

    private void Start()
    {
        InitializeRenderTexture();
        StartStreaming();
    }

    private void InitializeRenderTexture()
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();

        // Используем 24-битный глубинный буфер и ARGB32 для рендеринга
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

            // Запрашиваем кадр асинхронно
            CaptureFrameRaw();

            // Ждем точный интервал времени до следующего кадра
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void CaptureFrameRaw()
    {
        _frameCounter++;
        uint currentFrameSeq = _frameCounter;
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Непосредственно асинхронное чтение из VRAM на CPU
        AsyncGPUReadback.Request(_renderTexture, 0, TextureFormat.RGB24, request =>
        {
            if (request.hasError)
            {
                Debug.LogError("[CameraStreamer] Ошибка AsyncGPUReadback!");
                return;
            }

            var nativeArray = request.GetData<byte>();
            byte[] rawBytes = nativeArray.ToArray();

            CameraFramePacket packet = new CameraFramePacket
            {
                width = this.width,
                height = this.height,
                channels = 3,
                timestamp = timestamp,
                frameSequence = currentFrameSeq,
                pixelData = rawBytes
            };

            DataBroker.Publish($"Camera_{cameraID}", packet);
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