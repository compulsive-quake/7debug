using System.Threading;

namespace SevenDebug
{
    public class ScreenshotRequest
    {
        public byte[] PngData { get; set; }
        public ManualResetEvent WaitHandle { get; } = new ManualResetEvent(false);
    }
}
