using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Multiplayer voice chat. Captures the microphone chosen in Settings → Voice (so it never picks the wrong
    /// one), push-to-talk (default G) or open mic with a sensitivity threshold, compresses 20 ms frames with IMA
    /// ADPCM and sends them unreliably (a lost packet is just a 20 ms gap). The host relays voice to everyone.
    /// Incoming voice plays from each player's head (3D proximity) or as a radio (2D), with per-player mute.
    /// </summary>
    public sealed class VoiceChat : MonoBehaviour
    {
        public static VoiceChat I { get; private set; }

        // ---- capture
        AudioClip micClip;
        string micDevice;
        int micRate, micPos;
        float[] micTemp = new float[4096];
        readonly List<float> pending = new List<float>(4096);
        float resamplePhase;
        int encPredictor, encIndex;
        ushort sendSeq;
        readonly List<byte> outFrames = new List<byte>(Adpcm.FrameBytes * 3);
        int framesInPacket;
        float holdOpen;            // open-mic hang time so words don't get chopped
        public bool Transmitting { get; private set; }
        public float InputLevel { get; private set; }  // 0..1 meter for the settings screen
        public bool TestMode;      // settings "test microphone": capture + hear yourself, no network
        public string MicError { get; private set; } = "";

        // ---- playback
        sealed class Stream
        {
            public AudioRing Ring = new AudioRing(Adpcm.SampleRate * 2);
            public AudioClip Clip;
            public AudioSource Source;
            public float LastPacket = -99f;
            public bool Primed;
        }
        readonly Dictionary<byte, Stream> streams = new Dictionary<byte, Stream>();
        Stream loopback;
        public readonly HashSet<string> Muted = new HashSet<string>();   // by player name (session)
        readonly float[] decodeBuf = new float[Adpcm.FrameSamples];

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("VoiceChat");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<VoiceChat>();
        }

        public static string[] Devices
        {
            get
            {
                try { return Microphone.devices; } catch { return new string[0]; }
            }
        }

        public static string KeyHint
        {
            get
            {
                var d = Settings.Data;
                return d.voiceMode == 2 ? "voice chat off (Settings > Voice)" : d.voiceMode == 1 ? "voice: open mic" : "hold " + ((KeyCode)d.pushToTalkKey) + " to talk";
            }
        }

        bool WantsMic => TestMode || (NetState.Active && NetManager.I != null && NetManager.I.InGame && Settings.Data.voiceMode != 2);

        void Update()
        {
            if (WantsMic) EnsureMic(); else StopMic();
            if (micClip != null) Capture();
            float vol = Settings.Data.incomingVoiceVolume * Settings.Data.masterVolume;
            foreach (var s in streams.Values) if (s.Source != null) s.Source.volume = vol;
            // Drop playback streams of players who left.
            foreach (var id in new List<byte>(streams.Keys))
                if (NetManager.I == null || !NetManager.I.Players.ContainsKey(id)) { DestroyStream(streams[id]); streams.Remove(id); }
        }

        // ================================================================== microphone

        /// <summary>Restarts capture (call after changing the device in Settings).</summary>
        public void RestartMic() { StopMic(); }

        void EnsureMic()
        {
            string want = Settings.Data.micDevice;
            var devs = Devices;
            if (devs.Length == 0) { MicError = "No microphone found."; return; }
            if (!string.IsNullOrEmpty(want) && System.Array.IndexOf(devs, want) < 0) want = null; // unplugged → default
            if (string.IsNullOrEmpty(want)) want = null;
            if (micClip != null && micDevice == want) return;
            StopMic();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                MicError = "Waiting for microphone permission...";
                return;
            }
#endif
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) { Application.RequestUserAuthorization(UserAuthorization.Microphone); MicError = "Waiting for microphone permission..."; return; }
            try
            {
                Microphone.GetDeviceCaps(want, out int min, out int max);
                micRate = (min == 0 && max == 0) ? Adpcm.SampleRate : Mathf.Clamp(Adpcm.SampleRate, min, max);
                micClip = Microphone.Start(want, true, 1, micRate);
                micDevice = want;
                micPos = 0;
                pending.Clear();
                MicError = micClip == null ? "Couldn't open the microphone." : "";
                Debug.Log("[Voice] Microphone: " + (want ?? "system default") + " @ " + micRate + " Hz");
            }
            catch (System.Exception e) { MicError = "Microphone error: " + e.Message; micClip = null; }
        }

        void StopMic()
        {
            if (micClip == null) return;
            try { Microphone.End(micDevice); } catch { }
            micClip = null;
            Transmitting = false;
            InputLevel = 0f;
        }

        void Capture()
        {
            int pos = Microphone.GetPosition(micDevice);
            if (pos < 0) return;
            int len = micClip.samples;
            int available = (pos - micPos + len) % len;
            if (available <= 0) return;
            if (micTemp.Length < available) micTemp = new float[available];
            // Read in up to two parts (the clip is a 1 s ring).
            int first = Mathf.Min(available, len - micPos);
            var part = new float[first];
            micClip.GetData(part, micPos);
            System.Array.Copy(part, 0, micTemp, 0, first);
            if (available > first)
            {
                var rest = new float[available - first];
                micClip.GetData(rest, 0);
                System.Array.Copy(rest, 0, micTemp, first, rest.Length);
            }
            micPos = pos;

            // Resample to 16 kHz with gain.
            float ratio = micRate / (float)Adpcm.SampleRate, gain = Settings.Data.micGain;
            for (; resamplePhase < available - 1; resamplePhase += ratio)
            {
                int i = (int)resamplePhase;
                float t = resamplePhase - i;
                pending.Add(Mathf.Clamp((micTemp[i] * (1 - t) + micTemp[i + 1] * t) * gain, -1f, 1f));
            }
            resamplePhase -= available;
            if (resamplePhase < 0) resamplePhase = 0;

            var frame = new float[Adpcm.FrameSamples];
            while (pending.Count >= Adpcm.FrameSamples)
            {
                pending.CopyTo(0, frame, 0, Adpcm.FrameSamples);
                pending.RemoveRange(0, Adpcm.FrameSamples);
                ProcessFrame(frame);
            }
        }

        void ProcessFrame(float[] frame)
        {
            float sum = 0f;
            for (int i = 0; i < frame.Length; i++) sum += frame[i] * frame[i];
            float rms = Mathf.Sqrt(sum / frame.Length);
            InputLevel = Mathf.Lerp(InputLevel, Mathf.Clamp01(rms * 6f), 0.4f);

            var d = Settings.Data;
            bool talk;
            if (TestMode && !NetState.Active) talk = rms > d.voiceThreshold || Input.GetKey((KeyCode)d.pushToTalkKey);
            else if (d.voiceMode == 0) talk = Input.GetKey((KeyCode)d.pushToTalkKey) && !UIRoot.DevPanelCapturing && !UIRoot.Contains<ChatScreen>();
            else if (d.voiceMode == 1)
            {
                if (rms > d.voiceThreshold) holdOpen = 0.35f; else holdOpen -= 0.02f;
                talk = holdOpen > 0f;
            }
            else talk = false;
            Transmitting = talk;
            if (!talk) { FlushPacket(); return; }

            int start = outFrames.Count;
            for (int i = 0; i < Adpcm.FrameBytes; i++) outFrames.Add(0);
            var tmp = new byte[Adpcm.FrameBytes];
            Adpcm.EncodeFrame(frame, 0, tmp, 0, ref encPredictor, ref encIndex);
            for (int i = 0; i < tmp.Length; i++) outFrames[start + i] = tmp[i];
            framesInPacket++;
            if (TestMode && !NetState.Active) { PlayLoopback(tmp); outFrames.Clear(); framesInPacket = 0; return; }
            if (framesInPacket >= 2) FlushPacket(); // 40 ms per packet
        }

        void FlushPacket()
        {
            if (framesInPacket == 0) return;
            var nm = NetManager.I;
            if (nm != null && NetState.Active && nm.InGame)
            {
                var w = nm.Writer();
                w.UShort(sendSeq++);
                w.Byte((byte)framesInPacket);
                var arr = outFrames.ToArray();
                w.Bytes(arr, 0, arr.Length);
                nm.SendVoice(w);
            }
            outFrames.Clear();
            framesInPacket = 0;
        }

        // ================================================================== playback

        /// <summary>Called by NetManager with frames from player <paramref name="id"/>.</summary>
        public void Receive(byte id, byte[] data, int offset, int frames)
        {
            var nm = NetManager.I;
            var p = nm != null ? nm.Get(id) : null;
            if (p == null || Muted.Contains(p.Name)) return;
            if (!streams.TryGetValue(id, out var s)) { s = MakeStream(p); streams[id] = s; }
            if (s.Source == null || (p.Avatar != null && s.Source != p.Avatar.VoiceSource)) Attach(s, p);
            for (int f = 0; f < frames; f++)
            {
                int off = offset + f * Adpcm.FrameBytes;
                if (off + Adpcm.FrameBytes > data.Length) break;
                Adpcm.DecodeFrame(data, off, decodeBuf, 0);
                s.Ring.Write(decodeBuf, 0, decodeBuf.Length);
            }
            s.LastPacket = Time.unscaledTime;
        }

        Stream MakeStream(NetPlayer p)
        {
            var s = new Stream();
            s.Clip = AudioClip.Create("Voice " + p.Name, Adpcm.SampleRate, 1, Adpcm.SampleRate, true, data => ReadStream(s, data));
            Attach(s, p);
            return s;
        }

        void Attach(Stream s, NetPlayer p)
        {
            AudioSource src = p.Avatar != null ? p.Avatar.VoiceSource : null;
            if (src == null)
            {
                var go = new GameObject("Voice2D " + p.Name);
                go.transform.SetParent(transform, false);
                src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
            }
            s.Source = src;
            src.clip = s.Clip;
            src.loop = true;
            src.volume = Settings.Data.incomingVoiceVolume * Settings.Data.masterVolume;
            src.Play();
        }

        static void ReadStream(Stream s, float[] data)
        {
            // Jitter buffer: wait for ~60 ms before starting, re-prime after running dry.
            if (!s.Primed)
            {
                if (s.Ring.Count < Adpcm.SampleRate * 6 / 100) { System.Array.Clear(data, 0, data.Length); return; }
                s.Primed = true;
            }
            int got = s.Ring.Read(data, data.Length);
            if (got < data.Length) s.Primed = false;
        }

        void PlayLoopback(byte[] frame)
        {
            if (loopback == null)
            {
                loopback = new Stream();
                loopback.Clip = AudioClip.Create("VoiceTest", Adpcm.SampleRate, 1, Adpcm.SampleRate, true, data => ReadStream(loopback, data));
                var go = new GameObject("VoiceTest");
                go.transform.SetParent(transform, false);
                loopback.Source = go.AddComponent<AudioSource>();
                loopback.Source.spatialBlend = 0f;
                loopback.Source.clip = loopback.Clip;
                loopback.Source.loop = true;
                loopback.Source.Play();
            }
            Adpcm.DecodeFrame(frame, 0, decodeBuf, 0);
            loopback.Ring.Write(decodeBuf, 0, decodeBuf.Length);
            loopback.Source.volume = Settings.Data.incomingVoiceVolume * Settings.Data.masterVolume;
        }

        public void StopTest()
        {
            TestMode = false;
            if (loopback != null) { if (loopback.Source != null) Destroy(loopback.Source.gameObject); loopback = null; }
        }

        void DestroyStream(Stream s)
        {
            if (s.Source != null && s.Source.gameObject.name.StartsWith("Voice2D")) Destroy(s.Source.gameObject);
            else if (s.Source != null) s.Source.Stop();
        }

        public bool IsTalking(byte id) => streams.TryGetValue(id, out var s) && Time.unscaledTime - s.LastPacket < 0.3f;

        public void ToggleMute(NetPlayer p)
        {
            if (p == null) return;
            if (!Muted.Remove(p.Name)) Muted.Add(p.Name);
            if (streams.TryGetValue(p.Id, out var s)) s.Ring.Clear();
        }

        void OnApplicationQuit() => StopMic();
    }
}
