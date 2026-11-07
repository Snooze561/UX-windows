using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.MediaFoundation;

namespace UXWindows.Video;

/// <summary>
/// Lit une vidéo en boucle dans une fenêtre, sans son, avec le décodage matériel de la carte graphique.
///
/// Les images passent par DirectComposition : c'est la seule méthode affichée par Windows 11 24H2 et
/// plus quand la fenêtre est placée derrière les icônes du bureau (Progman).
/// Le nombre d'images par seconde est plafonné et le rendu s'arrête complètement en pause.
/// </summary>
public sealed class VideoRenderer : IDisposable
{
    private static int _mfStarted;

    private readonly int _width;
    private readonly int _height;
    private readonly int _maxFps;

    private readonly ID3D11Device _device;
    private readonly IDXGISwapChain1 _swapChain;
    private readonly IDCompositionDevice _dcomp;
    private readonly IDCompositionTarget _target;
    private readonly IDCompositionVisual _visual;
    private readonly IMFDXGIDeviceManager _deviceManager;
    private readonly IMFMediaEngine _engine;
    private readonly MediaEngineNotifyDelegate _notify; // gardé en mémoire tant que le moteur vit

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _resume = new(true);
    private volatile bool _stopping;
    private volatile bool _paused;

    /// <summary>Message d'erreur lisible si la vidéo ne peut pas être lue.</summary>
    public event Action<string>? Failed;

    public VideoRenderer(IntPtr hwnd, int width, int height, string path, int maxFps = 30)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _maxFps = Math.Clamp(maxFps, 1, 240);

        if (Interlocked.Exchange(ref _mfStarted, 1) == 0)
            MediaFactory.MFStartup(true).CheckError();

        // Carte graphique : support vidéo (décodage) + BGRA (composition).
        D3D11.D3D11CreateDevice(
            null, DriverType.Hardware,
            DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport,
            new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 },
            out _device!).CheckError();

        // Media Foundation utilise l'appareil depuis ses propres threads.
        using (var multithread = _device.QueryInterface<ID3D11Multithread>())
            multithread.SetMultithreadProtected(true);

        // Chaîne d'images pour la composition (modèle « flip », le plus économe).
        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();
        _swapChain = factory.CreateSwapChainForComposition(_device, new SwapChainDescription1
        {
            Width = (uint)_width,
            Height = (uint)_height,
            Format = Format.B8G8R8A8_UNorm,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = new SampleDescription(1, 0),
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = Vortice.DXGI.AlphaMode.Ignore,
        });

        _dcomp = DComp.DCompositionCreateDevice<IDCompositionDevice>(dxgiDevice);
        _dcomp.CreateTargetForHwnd(hwnd, true, out _target!).CheckError();
        _dcomp.CreateVisual(out _visual!).CheckError();
        _visual.SetContent(_swapChain).CheckError();
        _target.SetRoot(_visual).CheckError();

        ClearToBlack();
        _dcomp.Commit().CheckError();

        // Moteur de lecture Media Foundation en mode « serveur d'images ».
        _deviceManager = MediaFactory.MFCreateDXGIDeviceManager();
        _deviceManager.ResetDevice(_device).CheckError();

        using var attributes = MediaFactory.MFCreateAttributes(2);
        attributes.Set(MediaEngineAttributeKeys.DxgiManager.Guid, _deviceManager).CheckError();
        attributes.Set(MediaEngineAttributeKeys.VideoOutputFormat.Guid, (uint)Format.B8G8R8A8_UNorm).CheckError();

        using var engineFactory = new IMFMediaEngineClassFactory();
        _notify = OnEngineEvent;
        _engine = engineFactory.CreateInstance(MediaEngineCreateFlags.ForceMute, attributes, _notify);
        _engine.Muted = true;
        _engine.Loop = true;
        _engine.AutoPlay = true;
        _engine.SetSource(new Uri(path).AbsoluteUri).CheckError();

        _thread = new Thread(RenderLoop) { IsBackground = true, Name = "UXWindows video", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public bool IsPaused => _paused;

    public void Pause()
    {
        if (_paused) return;
        _paused = true;
        _resume.Reset();
        _engine.Pause();
    }

    public void Resume()
    {
        if (!_paused) return;
        _paused = false;
        _engine.Play();
        _resume.Set();
    }

    private void OnEngineEvent(MediaEngineEvent mediaEvent, UIntPtr param1, int param2)
    {
        if (mediaEvent == MediaEngineEvent.Error)
        {
            // param1 = code MF_MEDIA_ENGINE_ERR, param2 = HRESULT.
            string reason = (int)param1 switch
            {
                4 => "format ou codec non pris en charge (essaie un mp4 en H.264)",
                3 => "fichier vidéo abîmé",
                2 => "fichier introuvable ou inaccessible",
                _ => $"erreur de lecture (code {(int)param1}, 0x{param2:X8})",
            };
            Failed?.Invoke(reason);
        }
    }

    private void RenderLoop()
    {
        var frameTime = TimeSpan.FromSeconds(1.0 / _maxFps);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        while (!_stopping)
        {
            if (_paused)
            {
                // En pause : aucun calcul, le thread dort.
                _resume.Wait(500);
                continue;
            }

            var start = watch.Elapsed;
            try
            {
                if (_engine.OnVideoStreamTick(out _))
                    DrawCurrentFrame();
            }
            catch (SharpGenException)
            {
                // Image pas encore prête ou carte graphique réinitialisée : on réessaie au tour suivant.
            }

            var remaining = frameTime - (watch.Elapsed - start);
            if (remaining > TimeSpan.Zero) Thread.Sleep(remaining);
        }
    }

    private void DrawCurrentFrame()
    {
        _engine.GetNativeVideoSize(out int videoW, out int videoH);
        if (videoW <= 0 || videoH <= 0) return;

        // Remplissage de l'écran sans déformation : on rogne ce qui dépasse (comme « Remplir » dans Windows).
        float screenRatio = (float)_width / _height;
        float videoRatio = (float)videoW / videoH;
        var source = new VideoNormalizedRect { Left = 0, Top = 0, Right = 1, Bottom = 1 };
        if (videoRatio > screenRatio)
        {
            float keep = screenRatio / videoRatio;
            source.Left = (1 - keep) / 2;
            source.Right = 1 - source.Left;
        }
        else if (videoRatio < screenRatio)
        {
            float keep = videoRatio / screenRatio;
            source.Top = (1 - keep) / 2;
            source.Bottom = 1 - source.Top;
        }

        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _engine.TransferVideoFrame(backBuffer, source, new Vortice.RawRect(0, 0, _width, _height), new ColorBgra(0, 0, 0, 255));
        _swapChain.Present(1, PresentFlags.None);
    }

    private void ClearToBlack()
    {
        using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        using var view = _device.CreateRenderTargetView(backBuffer);
        _device.ImmediateContext.ClearRenderTargetView(view, new Color4(0, 0, 0, 1));
        _swapChain.Present(0, PresentFlags.None);
    }

    public void Dispose()
    {
        _stopping = true;
        _resume.Set();
        _thread.Join(2000);

        try
        {
            _engine.Shutdown();
            _target.SetRoot(null!);
            _dcomp.Commit();
        }
        catch (SharpGenException)
        {
            // On ferme quand même le reste.
        }
        _engine.Dispose();
        _deviceManager.Dispose();
        _visual.Dispose();
        _target.Dispose();
        _dcomp.Dispose();
        _swapChain.Dispose();
        _device.Dispose();
        _resume.Dispose();
    }
}
