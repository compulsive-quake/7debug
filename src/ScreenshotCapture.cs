using System;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace SevenDebug
{
    /// <summary>
    /// MonoBehaviour that runs on the main thread to capture screenshots
    /// and execute queued actions when requested by the HTTP server.
    /// </summary>
    public class ScreenshotCapture : MonoBehaviour
    {
        public ConcurrentQueue<ScreenshotRequest> RequestQueue { get; set; }

        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        public void QueueMainThreadAction(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        private void Update()
        {
            if (RequestQueue != null && RequestQueue.TryDequeue(out var req))
            {
                StartCoroutine(CaptureScreenshot(req));
            }

            while (_mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error($"[7debug] Main thread action failed: {ex.Message}");
                }
            }
        }

        private IEnumerator CaptureScreenshot(ScreenshotRequest req)
        {
            // Wait for end of frame so we get the fully rendered frame
            yield return new WaitForEndOfFrame();

            try
            {
                var width = Screen.width;
                var height = Screen.height;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                req.PngData = tex.EncodeToPNG();
                Destroy(tex);

                Log.Out($"[7debug] Screenshot captured: {width}x{height}, {req.PngData.Length} bytes");
            }
            catch (Exception ex)
            {
                Log.Error($"[7debug] Screenshot failed: {ex.Message}");
                req.PngData = null;
            }

            req.WaitHandle.Set();
        }
    }
}
