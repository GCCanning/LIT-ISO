using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// In-game gameplay recorder for review sessions (owner request, 2026-07-02).
    ///
    /// F10 starts/stops a clip. While recording it saves a full-resolution PNG frame
    /// every <see cref="CaptureInterval"/> seconds into
    /// <c>&lt;project-or-build&gt;/Recordings/rec_&lt;timestamp&gt;/frame_NNNN.png</c>
    /// and writes a <c>session.json</c> track (per frame: wall time, player position,
    /// clock, phase, biome). PNG frames + a JSON track were chosen deliberately over a
    /// video file: both the owner AND a reviewing AI can read them directly, with no
    /// codec, package, or upload step. ~2.5 fps for up to ~60 s per clip keeps a clip
    /// in the tens of MB.
    ///
    /// ScreenCapture writes asynchronously at end-of-frame — no per-frame stall beyond
    /// the engine's own readback. Recording state is presentation-only: nothing is
    /// simulated differently, and nothing persists in saves.
    /// </summary>
    public sealed class FoundationGameplayRecorder : MonoBehaviour
    {
        const float CaptureInterval = 0.4f; // ~2.5 fps
        const int MaxFrames = 150;          // ~60 s per clip

        IsoFoundationPlayer _player;
        DayNightSystem _dayNight;
        FoundationInteractionOverlay _overlay;

        bool _recording;
        float _timer;
        int _frame;
        string _dir;
        StringBuilder _meta;

        public bool IsRecording => _recording;

        public void Init(IsoFoundationPlayer player, DayNightSystem dayNight,
            FoundationInteractionOverlay overlay)
        {
            _player = player;
            _dayNight = dayNight;
            _overlay = overlay;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (_recording) Stop("stopped");
                else Begin();
            }
            if (!_recording) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = CaptureInterval;
            Capture();
            if (_frame >= MaxFrames) Stop("clip length cap");
        }

        void OnDestroy()
        {
            if (_recording) Stop("session ended");
        }

        void Begin()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
            _dir = Path.Combine(root, "rec_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            try { Directory.CreateDirectory(_dir); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Recorder] cannot create {_dir}: {e.Message}");
                _overlay?.Flash("Recorder: could not create Recordings folder.", 3f);
                return;
            }

            _recording = true;
            _frame = 0;
            _timer = 0f;
            _meta = new StringBuilder("[\n");
            _overlay?.Flash("● REC — F10 stops (~60 s max)", 2.5f);
            Debug.Log($"[Recorder] recording to {_dir}");
        }

        void Capture()
        {
            _frame++;
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"frame_{_frame:0000}.png"));

            Vector2 g = _player != null ? _player.Ground : Vector2.zero;
            if (_frame > 1) _meta.Append(",\n");
            _meta.Append(string.Format(CultureInfo.InvariantCulture,
                "  {{\"frame\":{0},\"t\":{1:0.00},\"x\":{2:0.0},\"y\":{3:0.0},\"height\":{4},\"clock\":\"{5}\",\"phase\":\"{6}\",\"biome\":\"{7}\"}}",
                _frame, Time.unscaledTime, g.x, g.y,
                _player != null ? _player.Height : 0,
                _dayNight != null ? _dayNight.Clock : "",
                _dayNight != null ? _dayNight.PhaseLabel : "",
                (FoundationBiomeDiscovery.ActiveBiomeDisplay ?? "").Replace("\"", "")));
        }

        void Stop(string reason)
        {
            _recording = false;
            if (_meta != null)
            {
                try { File.WriteAllText(Path.Combine(_dir, "session.json"), _meta.Append("\n]\n").ToString()); }
                catch (Exception e) { Debug.LogWarning($"[Recorder] session.json write failed: {e.Message}"); }
            }
            _overlay?.Flash($"■ REC saved — {_frame} frames ({reason})", 4f);
            Debug.Log($"[Recorder] saved {_frame} frames to {_dir}");
        }
    }
}
