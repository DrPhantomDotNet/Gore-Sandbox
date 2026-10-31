using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Your phone's brain: voice calls, text messages and money transfers with other players (all relayed by the
    /// host). During a call you hear the other player at full volume wherever they are, and your mic is open
    /// (unless you mute it). The screens are in PhoneScreen.
    /// </summary>
    public static class OWPhone
    {
        public const long MaxTransfer = 100000;
        public enum CallState { None, Incoming, Calling, Talking }
        public static CallState Call { get; private set; }
        public static byte Peer { get; private set; } = 255;
        public static float CallSince { get; private set; }
        public static bool Muted;

        public sealed class Text { public bool Mine; public string Body; public string Time; }
        public static readonly Dictionary<byte, List<Text>> Threads = new Dictionary<byte, List<Text>>();
        public static readonly Dictionary<byte, int> Unread = new Dictionary<byte, int>();
        static readonly Dictionary<byte, string> names = new Dictionary<byte, string>();
        static float nextRing;

        public static bool InCallWith(byte id) => Call == CallState.Talking && Peer == id;
        /// <summary>VoiceChat: keep the mic open during a call.</summary>
        public static bool Transmit => OWDirector.Active && Call == CallState.Talking && !Muted;
        public static int TotalUnread { get { int n = 0; foreach (var v in Unread.Values) n += v; return n; } }

        public static void Reset()
        {
            Call = CallState.None; Peer = 255; Muted = false;
            Threads.Clear(); Unread.Clear(); names.Clear();
        }

        public static string NameOf(byte id)
        {
            var nm = NetManager.I;
            if (nm != null && nm.Players.TryGetValue(id, out var p)) { names[id] = p.Name; return p.Name; }
            return names.TryGetValue(id, out var n) ? n : "Player " + id;
        }

        /// <summary>Everyone else on the server (for the contacts list).</summary>
        public static List<byte> Contacts()
        {
            var list = new List<byte>();
            var nm = NetManager.I;
            if (!NetState.Active || nm == null) return list;
            foreach (var p in nm.Players.Values) if (p.Id != nm.LocalId && p.Ready | NetState.IsClient) list.Add(p.Id);
            list.Sort();
            return list;
        }

        static List<Text> Thread(byte id)
        {
            if (!Threads.TryGetValue(id, out var t)) { t = new List<Text>(); Threads[id] = t; }
            NameOf(id);
            return t;
        }

        static string Clock() => System.DateTime.Now.ToString("HH:mm");

        // ------------------------------------------------------------------ texts

        public static void SendText(byte to, string body)
        {
            body = (body ?? "").Trim();
            if (body.Length == 0) return;
            if (body.Length > 160) body = body.Substring(0, 160);
            Thread(to).Add(new Text { Mine = true, Body = body, Time = Clock() });
            NetManager.I.OWRequest(OWReq.Text, body, 0, default, 0, to);
            AudioManager.Ui("ui_click");
        }

        public static void ReceiveText(byte from, string body)
        {
            Thread(from).Add(new Text { Mine = false, Body = body, Time = Clock() });
            if (PhoneScreen.OpenThread != from || !UIRoot.Contains<PhoneScreen>()) { Unread.TryGetValue(from, out var u); Unread[from] = u + 1; }
            AudioManager.Ui("phone_ping");
            Toasts.Show("TEXT - " + NameOf(from), body.Length > 60 ? body.Substring(0, 60) + "..." : body, new Color(0.35f, 0.75f, 1f));
        }

        // ------------------------------------------------------------------ money

        public static bool SendMoney(byte to, long amount)
        {
            if (amount <= 0) return false;
            if (amount > MaxTransfer) { GameEvents.ShowToast("You can send up to $" + MaxTransfer.ToString("N0") + " at a time."); return false; }
            if (!OWSave.Spend(amount)) { GameEvents.ShowToast("Not enough cash."); AudioManager.Ui("ui_error"); return false; }
            NetManager.I.OWRequest(OWReq.Money, "", amount, default, 0, to);
            Thread(to).Add(new Text { Mine = true, Body = "[Sent $" + amount.ToString("N0") + "]", Time = Clock() });
            AudioManager.Ui("cash");
            GameEvents.ShowToast("Sent $" + amount.ToString("N0") + " to " + NameOf(to) + ".");
            return true;
        }

        public static void ReceiveMoney(byte from, long amount)
        {
            if (amount <= 0 || amount > MaxTransfer) return;
            OWSave.Receive(amount);
            OWHud.Money(amount, "From " + NameOf(from));
            Thread(from).Add(new Text { Mine = false, Body = "[Sent you $" + amount.ToString("N0") + "]", Time = Clock() });
            AudioManager.Ui("cash");
        }

        // ------------------------------------------------------------------ calls (0 ring, 1 accept, 2 hang up / decline, 3 busy)

        public static void StartCall(byte to)
        {
            if (Call != CallState.None) return;
            if (Settings.Data.voiceMode == 2) { GameEvents.ShowToast("Voice chat is off (Settings > Voice). Turn it on to make calls."); return; }
            Call = CallState.Calling; Peer = to; CallSince = Time.unscaledTime; Muted = false;
            NetManager.I.OWRequest(OWReq.Call, "", 0, default, 0, to);
        }

        public static void Accept()
        {
            if (Call != CallState.Incoming) return;
            Call = CallState.Talking; CallSince = Time.unscaledTime; Muted = false;
            NetManager.I.OWRequest(OWReq.Call, "", 1, default, 0, Peer);
            AudioManager.Ui("ui_success");
        }

        public static void HangUp()
        {
            if (Call == CallState.None) return;
            NetManager.I.OWRequest(OWReq.Call, "", 2, default, 0, Peer);
            End(null);
        }

        static void End(string why)
        {
            if (why != null) GameEvents.ShowToast(why);
            Call = CallState.None; Peer = 255;
            AudioManager.Ui("ui_close");
        }

        public static void OnCall(byte from, int kind)
        {
            switch (kind)
            {
                case 0:
                    if (Call != CallState.None) { NetManager.I.OWRequest(OWReq.Call, "", 3, default, 0, from); break; }
                    Call = CallState.Incoming; Peer = from; CallSince = Time.unscaledTime; nextRing = 0f;
                    Toasts.Show("INCOMING CALL", NameOf(from) + (Settings.TouchControlsActive ? " - tap PHONE to answer" : " - press P to answer"), UI.Good);
                    break;
                case 1:
                    if (Call == CallState.Calling && Peer == from) { Call = CallState.Talking; CallSince = Time.unscaledTime; AudioManager.Ui("ui_success"); }
                    break;
                case 2: if (Peer == from && Call != CallState.None) End(NameOf(from) + (Call == CallState.Calling ? " declined the call." : " hung up.")); break;
                case 3: if (Peer == from && Call == CallState.Calling) End(NameOf(from) + " is on another call."); break;
            }
        }

        public static void Tick()
        {
            if (Call == CallState.None) return;
            float t = Time.unscaledTime - CallSince;
            if (NetState.Active && NetManager.I != null && !NetManager.I.Players.ContainsKey(Peer)) { End("The call dropped."); return; }
            if (Call == CallState.Incoming && t > 25f) { NetManager.I.OWRequest(OWReq.Call, "", 2, default, 0, Peer); End("Missed call from " + NameOf(Peer) + "."); return; }
            if (Call == CallState.Calling && t > 27f) { HangUp(); GameEvents.ShowToast("No answer."); return; }
            if ((Call == CallState.Incoming || Call == CallState.Calling) && Time.unscaledTime > nextRing)
            {
                nextRing = Time.unscaledTime + 1.7f;
                AudioManager.Ui("phone_ring", Call == CallState.Incoming ? 0.9f : 0.35f);
            }
        }

        public static string CallLine
        {
            get
            {
                switch (Call)
                {
                    case CallState.Incoming: return "Incoming call: " + NameOf(Peer);
                    case CallState.Calling: return "Calling " + NameOf(Peer) + "...";
                    case CallState.Talking:
                        int s = Mathf.FloorToInt(Time.unscaledTime - CallSince);
                        return "On a call with " + NameOf(Peer) + "  " + (s / 60) + ":" + (s % 60).ToString("00") + (Muted ? "  (muted)" : "");
                }
                return null;
            }
        }
    }
}
