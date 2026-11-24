using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Backrooms
{
    public enum CoopRole
    {
        Offline = 0,
        Host = 1,
        Client = 2
    }

    /// <summary>Unified target snapshot used by EntityAgent &amp; EntityDirector to hunt, freeze on, or fear ANY Co-Op player.</summary>
    public struct PlayerTarget
    {
        public int slot;
        public Vector3 pos;
        public Vector3 head;
        public Vector3 lookDir;
        public bool flashlightOn;
        public float flashlightAngle;
        public float flashlightRange;
        public bool crouching;
        public float exposure;
        public float sanity01;
        public bool downed;
        public Transform transform;
    }

    /// <summary>
    /// Co-Op Networking for BackroomsEscape:
    /// - Unity Relay 6-letter Join Codes when Unity Cloud Services are linked
    /// - Automatic Direct UDP fallback on port 7778 + 1-click LAN Discovery beacon on port 7779
    /// - Syncs deterministic (RunSeed, LevelIndex) mazes, 3D Hazmat Explorer avatars, live proximity mic audio,
    ///   stolen player voice bait clips (VoiceMimicBank), shared 350-SCP entity puppets, downed/revive, and exits!
    /// </summary>
    public class BackroomsNet : MonoBehaviour
    {
        public static BackroomsNet Instance { get; private set; }
        public static bool IsOnline => Instance != null && Instance.Role != CoopRole.Offline && Instance.Ready;
        public static bool IsHost => Instance != null && Instance.Role == CoopRole.Host && Instance.Ready;
        public static bool IsClient => Instance != null && Instance.Role == CoopRole.Client && Instance.Ready;

        public const int MaxRemotePlayers = 7;
        public const ushort DirectPort = 7778;
        public const ushort LanBeaconPort = 7779;
        private const uint Magic = 0x424B524D; // "BKRM"

        // Packet types
        private const byte PHello = 1;          // Client -> Host (reliable): callsign
        private const byte PWelcome = 2;        // Host -> Client (reliable): assignedSlot, runSeed, levelIndex, modeByte
        private const byte PRunState = 3;       // Host -> Clients (reliable): runSeed, levelIndex, modeByte, killerName
        private const byte PClientPose = 4;     // Client -> Host (unreliable): pos, yaw, pitch, flags
        private const byte PWorldPlayers = 5;   // Host -> Clients (unreliable): all players' poses & flags
        private const byte PEntities = 6;       // Host -> Clients (unreliable): active SCP entity snapshots
        private const byte PEntityCue = 7;      // Host -> Clients (reliable): entity audio / stolen voice bait cue
        private const byte PVoiceLive = 8;      // Both ways (unreliable): speakerSlot + 100ms mu-law proximity voice
        private const byte PVoiceStolen = 9;    // Both ways (reliable): clipId, speakerSlot, speakerName, mu-law phrase
        private const byte PPickupTaken = 10;   // Both ways (reliable): cellX, cellY
        private const byte PExitUsed = 11;      // Client -> Host (reliable): teammate stepped through exit door
        private const byte PPlayerHit = 12;     // Host -> Clients (reliable): targetSlot, downedOnly, killerName
        private const byte PRevive = 13;        // Both ways (reliable): targetSlot revived

        public CoopRole Role { get; private set; } = CoopRole.Offline;
        public string JoinCode { get; private set; } = "";
        public string Status { get; private set; } = "";
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public bool IsDirectFallback { get; private set; }
        public int MySlot { get; private set; } = 0;
        public string MyName = "EXPLORER";

        private class ClientPeer
        {
            public int slot;
            public string name = "EXPLORER";
            public NetworkConnection conn;
            public Vector3 pos;
            public float yaw, pitch;
            public byte flags; // 1=crouch, 2=sprint, 4=flashlight, 8=speaking, 16=downed, 32=dead
        }

        private readonly List<ClientPeer> _peers = new List<ClientPeer>();
        public readonly Dictionary<int, RemotePlayerAvatar> Avatars = new Dictionary<int, RemotePlayerAvatar>();

        private NetworkDriver _driver;
        private NetworkPipeline _reliable, _unreliable;
        private NetworkConnection _toHost;
        private readonly List<NetworkConnection> _pending = new List<NetworkConnection>();
        private UdpClient _lanBeacon;
        private int _nextSlot = 1;
        private float _poseTimer;
        private float _entityTimer;
        private bool _destroyed;

        public int TotalPlayerCount => Role == CoopRole.Offline ? 1 : 1 + Avatars.Count;

        private void Awake()
        {
            Instance = this;
            MyName = PlayerPrefs.GetString("callsign", "EXPLORER-" + UnityEngine.Random.Range(10, 99));
        }

        private void OnDestroy()
        {
            _destroyed = true;
            StopNetwork();
        }

        public void SetCallsign(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            MyName = raw.Trim().Length > 16 ? raw.Trim().Substring(0, 16) : raw.Trim();
            PlayerPrefs.SetString("callsign", MyName);
        }

        // ------------------------------------------------------------------ Start / Host / Join / LAN
        public async void StartHost(Action onReady = null)
        {
            StopNetwork();
            Role = CoopRole.Host;
            MySlot = 0;
            Failed = false;
            Ready = false;
            IsDirectFallback = false;
            Status = "OPENING CO-OP ROOM...";

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (_destroyed) return;
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (_destroyed) return;

                Allocation a = await RelayService.Instance.CreateAllocationAsync(MaxRemotePlayers);
                if (_destroyed) return;
                string code = await RelayService.Instance.GetJoinCodeAsync(a.AllocationId);
                if (_destroyed) return;

                var ep = PickEndpoint(a.ServerEndpoints);
                var data = new RelayServerData(ep.Host, (ushort)ep.Port, a.AllocationIdBytes, a.ConnectionData, a.ConnectionData, a.Key, ep.Secure, false);
                CreateDriver(ref data);
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) throw new Exception("Relay bind failed");
                if (_driver.Listen() != 0) throw new Exception("Relay listen failed");

                JoinCode = code.ToUpperInvariant();
                Ready = true;
                Status = $"JOIN CODE: {JoinCode}";
                GUIUtility.systemCopyBuffer = JoinCode;
                Debug.Log($"[Backrooms Co-Op] Hosting Relay room {JoinCode}");
                onReady?.Invoke();
            }
            catch (Exception relayEx)
            {
                StartDirectHostFallback(relayEx, onReady);
            }
        }

        private void StartDirectHostFallback(Exception relayEx, Action onReady)
        {
            Debug.LogWarning($"[Backrooms Co-Op] Relay unavailable ({relayEx?.Message}); using Direct UDP port {DirectPort} + LAN beacon.");
            DisposeDriver();
            try
            {
                CreateDirectDriver();
                if (_driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(DirectPort)) != 0)
                    throw new Exception($"UDP port {DirectPort} busy");
                if (_driver.Listen() != 0)
                    throw new Exception($"Could not listen on {DirectPort}");

                StartLanBeaconServer();
                string ip = GetLocalIPv4();
                IsDirectFallback = true;
                JoinCode = $"{ip}:{DirectPort}";
                Ready = true;
                Status = $"CO-OP IP: {JoinCode} (COPIED)";
                GUIUtility.systemCopyBuffer = JoinCode;
                onReady?.Invoke();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public async void JoinGame(string codeOrIp)
        {
            StopNetwork();
            string raw = (codeOrIp ?? "").Trim();
            if (raw.Length == 0) { Status = "ENTER A JOIN CODE OR IP FIRST"; Failed = true; return; }

            Role = CoopRole.Client;
            Failed = false;
            Ready = false;

            if (raw.Contains(".") || raw.Contains(":") || string.Equals(raw, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                ConnectDirect(raw);
                return;
            }

            try
            {
                Status = $"CONNECTING TO {raw.ToUpperInvariant()}...";
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (_destroyed) return;
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (_destroyed) return;

                string code = raw.ToUpperInvariant();
                JoinAllocation j = await RelayService.Instance.JoinAllocationAsync(code);
                if (_destroyed) return;
                var ep = PickEndpoint(j.ServerEndpoints);
                var data = new RelayServerData(ep.Host, (ushort)ep.Port, j.AllocationIdBytes, j.ConnectionData, j.HostConnectionData, j.Key, ep.Secure, false);
                CreateDriver(ref data);
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) throw new Exception("Relay client bind failed");
                _toHost = _driver.Connect(data.Endpoint);
                JoinCode = code;
                Status = $"HANDSHAKING {code}...";
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public async void DiscoverLanAndJoin()
        {
            Status = "SCANNING LAN FOR CO-OP HOST...";
            string foundEndpoint = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using var udp = new UdpClient();
                    udp.EnableBroadcast = true;
                    udp.Client.ReceiveTimeout = 1100;
                    byte[] probe = Encoding.UTF8.GetBytes("BKRM_DISCOVER");
                    udp.Send(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, LanBeaconPort));
                    udp.Send(probe, probe.Length, new IPEndPoint(IPAddress.Loopback, LanBeaconPort));

                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    string txt = Encoding.UTF8.GetString(resp);
                    if (txt.StartsWith("BKRM_HERE:"))
                    {
                        string portStr = txt.Substring("BKRM_HERE:".Length).Trim();
                        foundEndpoint = $"{remote.Address}:{portStr}";
                    }
                }
                catch { }
            });

            if (_destroyed) return;
            if (!string.IsNullOrEmpty(foundEndpoint))
            {
                JoinGame(foundEndpoint);
            }
            else
            {
                // Fallback to localhost:7778 in case two instances are tested on the same PC with broadcast blocked
                Status = "NO LAN BEACON — TRYING LOCALHOST:7778...";
                JoinGame("127.0.0.1:7778");
            }
        }

        private void ConnectDirect(string raw)
        {
            try
            {
                string host = raw;
                ushort port = DirectPort;
                int colon = raw.LastIndexOf(':');
                if (colon > 0 && colon < raw.Length - 1 && ushort.TryParse(raw.Substring(colon + 1), out ushort p))
                {
                    host = raw.Substring(0, colon);
                    port = p;
                }
                if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) host = "127.0.0.1";
                if (!NetworkEndpoint.TryParse(host, port, out var ep, NetworkFamily.Ipv4))
                {
                    var addrs = Dns.GetHostAddresses(host);
                    IPAddress v4 = null;
                    foreach (var a in addrs) if (a.AddressFamily == AddressFamily.InterNetwork) { v4 = a; break; }
                    if (v4 == null || !NetworkEndpoint.TryParse(v4.ToString(), port, out ep, NetworkFamily.Ipv4))
                        throw new Exception($"Invalid address {raw}");
                }

                CreateDirectDriver();
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) throw new Exception("Socket bind failed");
                _toHost = _driver.Connect(ep);
                IsDirectFallback = true;
                JoinCode = $"{host}:{port}";
                Status = $"CONNECTING TO {JoinCode}...";
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void StartLanBeaconServer()
        {
            StopLanBeacon();
            try
            {
                _lanBeacon = new UdpClient(LanBeaconPort);
                _lanBeacon.EnableBroadcast = true;
                _lanBeacon.BeginReceive(OnLanProbe, null);
            }
            catch { /* Multiple local instances on same machine: ignore beacon port conflict */ }
        }

        private void OnLanProbe(IAsyncResult ar)
        {
            if (_lanBeacon == null) return;
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _lanBeacon.EndReceive(ar, ref remote);
                string msg = Encoding.UTF8.GetString(data);
                if (msg == "BKRM_DISCOVER" && Role == CoopRole.Host && Ready)
                {
                    byte[] reply = Encoding.UTF8.GetBytes($"BKRM_HERE:{DirectPort}");
                    _lanBeacon.Send(reply, reply.Length, remote);
                }
                _lanBeacon.BeginReceive(OnLanProbe, null);
            }
            catch { }
        }

        private void StopLanBeacon()
        {
            if (_lanBeacon != null)
            {
                try { _lanBeacon.Close(); } catch { }
                _lanBeacon = null;
            }
        }

        public void StopNetwork()
        {
            StopLanBeacon();
            DisposeDriver();
            foreach (var kv in Avatars) if (kv.Value != null) Destroy(kv.Value.gameObject);
            Avatars.Clear();
            _peers.Clear();
            _pending.Clear();
            Role = CoopRole.Offline;
            Ready = false;
            Failed = false;
            MySlot = 0;
            JoinCode = "";
            Status = "";
        }

        private void DisposeDriver()
        {
            if (_driver.IsCreated)
            {
                try { _driver.Dispose(); } catch { }
            }
            _driver = default;
            _toHost = default;
        }

        private void Fail(Exception ex)
        {
            Failed = true;
            Ready = false;
            Status = "CO-OP FAILED: " + (ex?.Message ?? "Disconnected");
            Debug.LogWarning("[Backrooms Co-Op] " + Status);
        }

        private static RelayServerEndpoint PickEndpoint(List<RelayServerEndpoint> eps)
        {
            foreach (var e in eps) if (e.ConnectionType == "dtls") return e;
            foreach (var e in eps) if (e.ConnectionType == "udp") return e;
            return eps[0];
        }

        private void CreateDriver(ref RelayServerData data)
        {
            var s = new NetworkSettings();
            s.WithRelayParameters(ref data);
            s.WithFragmentationStageParameters(payloadCapacity: 32 * 1024);
            s.WithReliableStageParameters(windowSize: 64);
            _driver = NetworkDriver.Create(s);
            _reliable = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            _unreliable = _driver.CreatePipeline(typeof(FragmentationPipelineStage));
        }

        private void CreateDirectDriver()
        {
            var s = new NetworkSettings();
            s.WithFragmentationStageParameters(payloadCapacity: 32 * 1024);
            s.WithReliableStageParameters(windowSize: 64);
            _driver = NetworkDriver.Create(s);
            _reliable = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            _unreliable = _driver.CreatePipeline(typeof(FragmentationPipelineStage));
        }

        private static string GetLocalIPv4()
        {
            try
            {
                using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                sock.Connect("8.8.8.8", 65530);
                if (sock.LocalEndPoint is IPEndPoint ep) return ep.Address.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        // ------------------------------------------------------------------ Update Pump
        private void Update()
        {
            if (!_driver.IsCreated || Role == CoopRole.Offline) return;
            _driver.ScheduleUpdate().Complete();

            if (Role == CoopRole.Host) PumpHost();
            else if (Role == CoopRole.Client) PumpClient();

            if (!Ready) return;

            _poseTimer -= Time.unscaledDeltaTime;
            if (_poseTimer <= 0f)
            {
                _poseTimer = 0.05f; // 20 Hz player sync
                if (Role == CoopRole.Client) SendClientPose();
                else if (Role == CoopRole.Host) BroadcastWorldPlayers();
            }

            if (Role == CoopRole.Host)
            {
                _entityTimer -= Time.unscaledDeltaTime;
                if (_entityTimer <= 0f)
                {
                    _entityTimer = 0.066f; // 15 Hz entity sync
                    BroadcastEntities();
                }
            }
        }

        private void PumpHost()
        {
            NetworkConnection incoming;
            while ((incoming = _driver.Accept()) != default)
                _pending.Add(incoming);

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var c = _pending[i];
                if (!c.IsCreated) { _pending.RemoveAt(i); continue; }
                NetworkEvent.Type ev;
                while ((ev = _driver.PopEventForConnection(c, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Data)
                    {
                        if (ReadPacketHeader(ref reader, out byte type, out byte[] body) && type == PHello)
                        {
                            AcceptNewPeer(c, body);
                            _pending.RemoveAt(i);
                            break;
                        }
                    }
                    else if (ev == NetworkEvent.Type.Disconnect)
                    {
                        _pending.RemoveAt(i);
                        break;
                    }
                }
            }

            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var peer = _peers[i];
                NetworkEvent.Type ev;
                while ((ev = _driver.PopEventForConnection(peer.conn, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Data)
                    {
                        if (ReadPacketHeader(ref reader, out byte type, out byte[] body))
                            HandleClientPacketOnHost(peer, type, body);
                    }
                    else if (ev == NetworkEvent.Type.Disconnect)
                    {
                        RemovePeer(peer);
                        _peers.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void PumpClient()
        {
            if (!_toHost.IsCreated) return;
            NetworkEvent.Type ev;
            while ((ev = _driver.PopEventForConnection(_toHost, out var reader)) != NetworkEvent.Type.Empty)
            {
                if (ev == NetworkEvent.Type.Connect)
                {
                    Status = "CONNECTED — JOINING SQUAD...";
                    using var ms = new MemoryStream();
                    using var bw = new BinaryWriter(ms);
                    bw.Write(MyName ?? "EXPLORER");
                    Send(_toHost, _reliable, PHello, ms.ToArray());
                }
                else if (ev == NetworkEvent.Type.Data)
                {
                    if (ReadPacketHeader(ref reader, out byte type, out byte[] body))
                        HandleHostPacketOnClient(type, body);
                }
                else if (ev == NetworkEvent.Type.Disconnect)
                {
                    GameUI.Instance?.Toast("DISCONNECTED FROM CO-OP HOST", 4f);
                    StopNetwork();
                    GameBoot.Instance?.ToMenu();
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ Framing
        private void Send(NetworkConnection conn, NetworkPipeline pipe, byte type, byte[] body)
        {
            if (!_driver.IsCreated || !conn.IsCreated) return;
            int len = body != null ? body.Length : 0;
            if (_driver.BeginSend(pipe, conn, out var writer) != 0) return;
            writer.WriteUInt(Magic);
            writer.WriteByte(type);
            writer.WriteInt(len);
            if (len > 0)
            {
                using var na = new NativeArray<byte>(body, Allocator.Temp);
                writer.WriteBytes(na);
            }
            _driver.EndSend(writer);
        }

        private void BroadcastToClients(NetworkPipeline pipe, byte type, byte[] body, int excludeSlot = -1)
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                if (_peers[i].slot == excludeSlot) continue;
                Send(_peers[i].conn, pipe, type, body);
            }
        }

        private static bool ReadPacketHeader(ref DataStreamReader reader, out byte type, out byte[] body)
        {
            type = 0; body = null;
            if (reader.Length < 9) return false;
            uint magic = reader.ReadUInt();
            if (magic != Magic) return false;
            type = reader.ReadByte();
            int len = reader.ReadInt();
            if (len < 0 || len > 32 * 1024 || reader.Length - reader.GetBytesRead() < len) return false;
            body = new byte[len];
            if (len > 0)
            {
                using var na = new NativeArray<byte>(len, Allocator.Temp);
                reader.ReadBytes(na);
                na.CopyTo(body);
            }
            return true;
        }

        // ------------------------------------------------------------------ Host Logic
        private void AcceptNewPeer(NetworkConnection conn, byte[] body)
        {
            using var br = new BinaryReader(new MemoryStream(body));
            string callsign = br.ReadString();
            int slot = _nextSlot++;
            var peer = new ClientPeer { slot = slot, name = callsign, conn = conn };
            _peers.Add(peer);

            var boot = GameBoot.Instance;
            Vector3 spawnPos = boot != null && boot.Player != null ? boot.Player.transform.position + new Vector3(1.1f, 0, 0) : Vector3.zero;
            peer.pos = spawnPos;
            var av = RemotePlayerAvatar.Create(slot, callsign, spawnPos);
            Avatars[slot] = av;

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(slot);
            bw.Write(boot != null ? boot.RunSeed : 1);
            bw.Write(boot != null ? boot.LevelIndex : 0);
            bw.Write((byte)(boot != null ? boot.State : GameBoot.Mode.Playing));
            Send(conn, _reliable, PWelcome, ms.ToArray());

            // Share existing stolen voice phrases so late-joiners also hear recorded voices!
            for (int i = 0; i < VoiceMimicBank.Phrases.Count; i++)
            {
                var p = VoiceMimicBank.Phrases[i];
                Send(conn, _reliable, PVoiceStolen, PackStolenVoice(p.clipId, p.speakerSlot, p.speakerName, p.mulaw));
            }

            GameUI.Instance?.Toast($"{callsign} NOCLIPPED INTO YOUR RUN!", 3.5f);
        }

        private void RemovePeer(ClientPeer peer)
        {
            if (Avatars.TryGetValue(peer.slot, out var av) && av != null) Destroy(av.gameObject);
            Avatars.Remove(peer.slot);
            GameUI.Instance?.Toast($"{peer.name} DISCONNECTED", 3f);
            CheckSquadWipeOnHost("THE BACKROOMS");
        }

        private void HandleClientPacketOnHost(ClientPeer peer, byte type, byte[] body)
        {
            using var br = new BinaryReader(new MemoryStream(body));
            switch (type)
            {
                case PClientPose:
                {
                    peer.pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    peer.yaw = br.ReadSingle();
                    peer.pitch = br.ReadSingle();
                    byte clientFlags = br.ReadByte();
                    // Keep authoritative downed/dead bits on Host
                    peer.flags = (byte)((clientFlags & 0x0F) | (peer.flags & 0x30));
                    if (Avatars.TryGetValue(peer.slot, out var av) && av != null)
                        ApplyAvatarFlags(av, peer.pos, peer.yaw, peer.pitch, peer.flags);

                    // Remote sprint footsteps also emit noise on Host so listeners hunt loud teammates!
                    if ((peer.flags & 2) != 0 && (peer.flags & 0x30) == 0)
                        NoiseSystem.Emit(peer.pos, GameConfig.Current.player.noiseSprint * 0.8f);
                    break;
                }
                case PVoiceLive:
                {
                    int speaker = br.ReadInt32();
                    int n = br.ReadInt32();
                    byte[] mulaw = br.ReadBytes(n);
                    if (Avatars.TryGetValue(peer.slot, out var av) && av != null)
                    {
                        av.PushVoicePacket(mulaw);
                        // Monsters on Host hear remote players' microphones in real time!
                        if (!av.Dead) NoiseSystem.Emit(av.transform.position, 19f);
                    }
                    BroadcastToClients(_unreliable, PVoiceLive, body, excludeSlot: peer.slot);
                    break;
                }
                case PVoiceStolen:
                {
                    UnpackStolenVoice(br, out int clipId, out int speakerSlot, out string speakerName, out byte[] mulaw);
                    VoiceMimicBank.AddPhrase(clipId, speakerSlot, speakerName, mulaw, localCapture: false);
                    BroadcastToClients(_reliable, PVoiceStolen, body, excludeSlot: peer.slot);
                    break;
                }
                case PPickupTaken:
                {
                    int cx = br.ReadInt32(), cy = br.ReadInt32();
                    GameBoot.Instance?.Chunks?.MarkCollectedFromNet(new Vector2Int(cx, cy));
                    BroadcastToClients(_reliable, PPickupTaken, body, excludeSlot: peer.slot);
                    break;
                }
                case PExitUsed:
                {
                    GameBoot.Instance?.ExitReached();
                    break;
                }
                case PRevive:
                {
                    int targetSlot = br.ReadInt32();
                    ApplyReviveAuthoritative(targetSlot);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Client Logic
        private void HandleHostPacketOnClient(byte type, byte[] body)
        {
            using var br = new BinaryReader(new MemoryStream(body));
            switch (type)
            {
                case PWelcome:
                {
                    MySlot = br.ReadInt32();
                    int seed = br.ReadInt32();
                    int lvl = br.ReadInt32();
                    var mode = (GameBoot.Mode)br.ReadByte();
                    Ready = true;
                    Status = $"JOINED ROOM {JoinCode}";
                    GameBoot.Instance?.ApplySyncedRunStateFromHost(seed, lvl, mode, "");
                    break;
                }
                case PRunState:
                {
                    int seed = br.ReadInt32();
                    int lvl = br.ReadInt32();
                    var mode = (GameBoot.Mode)br.ReadByte();
                    string killer = br.ReadString();
                    GameBoot.Instance?.ApplySyncedRunStateFromHost(seed, lvl, mode, killer);
                    break;
                }
                case PWorldPlayers:
                {
                    int count = br.ReadInt32();
                    var seenSlots = new HashSet<int>();
                    for (int i = 0; i < count; i++)
                    {
                        int slot = br.ReadInt32();
                        string pName = br.ReadString();
                        Vector3 pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        float yaw = br.ReadSingle();
                        float pitch = br.ReadSingle();
                        byte flags = br.ReadByte();
                        if (slot == MySlot) continue;
                        seenSlots.Add(slot);
                        if (!Avatars.TryGetValue(slot, out var av) || av == null)
                            Avatars[slot] = av = RemotePlayerAvatar.Create(slot, pName, pos);
                        else
                            av.SetName(pName);
                        ApplyAvatarFlags(av, pos, yaw, pitch, flags);
                    }
                    // Prune disconnected avatars
                    var toRemove = new List<int>();
                    foreach (var kv in Avatars) if (!seenSlots.Contains(kv.Key)) toRemove.Add(kv.Key);
                    foreach (int s in toRemove)
                    {
                        if (Avatars[s] != null) Destroy(Avatars[s].gameObject);
                        Avatars.Remove(s);
                    }
                    break;
                }
                case PEntities:
                {
                    EntityDirector.Instance?.ApplyClientSnapshots(br);
                    break;
                }
                case PEntityCue:
                {
                    int netId = br.ReadInt32();
                    byte cueKind = br.ReadByte();
                    int clipId = br.ReadInt32();
                    EntityDirector.Instance?.PlayClientEntityCue(netId, cueKind, clipId);
                    break;
                }
                case PVoiceLive:
                {
                    int speaker = br.ReadInt32();
                    int n = br.ReadInt32();
                    byte[] mulaw = br.ReadBytes(n);
                    if (speaker != MySlot && Avatars.TryGetValue(speaker, out var av) && av != null)
                        av.PushVoicePacket(mulaw);
                    break;
                }
                case PVoiceStolen:
                {
                    UnpackStolenVoice(br, out int clipId, out int speakerSlot, out string speakerName, out byte[] mulaw);
                    VoiceMimicBank.AddPhrase(clipId, speakerSlot, speakerName, mulaw, localCapture: false);
                    break;
                }
                case PPickupTaken:
                {
                    int cx = br.ReadInt32(), cy = br.ReadInt32();
                    GameBoot.Instance?.Chunks?.MarkCollectedFromNet(new Vector2Int(cx, cy));
                    break;
                }
                case PPlayerHit:
                {
                    int targetSlot = br.ReadInt32();
                    bool downedOnly = br.ReadBoolean();
                    string killer = br.ReadString();
                    if (targetSlot == MySlot && GameBoot.Instance != null && GameBoot.Instance.Player != null)
                    {
                        if (downedOnly) GameBoot.Instance.Player.SetDowned(true, killer);
                        else GameBoot.Instance.Player.Kill(null, killer);
                    }
                    break;
                }
                case PRevive:
                {
                    int targetSlot = br.ReadInt32();
                    if (targetSlot == MySlot && GameBoot.Instance != null && GameBoot.Instance.Player != null)
                        GameBoot.Instance.Player.SetDowned(false, "");
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Public Sync Helpers
        private byte BuildLocalFlags()
        {
            var p = GameBoot.Instance != null ? GameBoot.Instance.Player : null;
            if (p == null) return 0;
            byte f = 0;
            if (p.Crouching) f |= 1;
            if (p.Sprinting) f |= 2;
            if (p.Flashlight != null && p.Flashlight.enabled) f |= 4;
            if (VoiceMimicSystem.Instance != null && VoiceMimicSystem.Instance.IsSpeaking) f |= 8;
            if (p.Downed) f |= 16;
            if (p.Dead) f |= 32;
            return f;
        }

        private static void ApplyAvatarFlags(RemotePlayerAvatar av, Vector3 pos, float yaw, float pitch, byte flags)
        {
            av.TargetPos = pos;
            av.TargetYaw = yaw;
            av.TargetPitch = pitch;
            av.Crouching = (flags & 1) != 0;
            av.Sprinting = (flags & 2) != 0;
            av.FlashlightOn = (flags & 4) != 0;
            if ((flags & 8) != 0) av.Speaking = true;
            av.Downed = (flags & 16) != 0;
            av.Dead = (flags & 32) != 0;
        }

        private void SendClientPose()
        {
            var p = GameBoot.Instance != null ? GameBoot.Instance.Player : null;
            if (p == null) return;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            Vector3 pos = p.transform.position;
            bw.Write(pos.x); bw.Write(pos.y); bw.Write(pos.z);
            bw.Write(p.Yaw);
            bw.Write(p.Pitch);
            bw.Write(BuildLocalFlags());
            Send(_toHost, _unreliable, PClientPose, ms.ToArray());
        }

        private void BroadcastWorldPlayers()
        {
            if (_peers.Count == 0) return;
            var p = GameBoot.Instance != null ? GameBoot.Instance.Player : null;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            int total = (p != null ? 1 : 0) + _peers.Count;
            bw.Write(total);
            if (p != null)
            {
                Vector3 pos = p.transform.position;
                bw.Write(0);
                bw.Write(MyName);
                bw.Write(pos.x); bw.Write(pos.y); bw.Write(pos.z);
                bw.Write(p.Yaw);
                bw.Write(p.Pitch);
                bw.Write(BuildLocalFlags());
            }
            for (int i = 0; i < _peers.Count; i++)
            {
                var c = _peers[i];
                bw.Write(c.slot);
                bw.Write(c.name);
                bw.Write(c.pos.x); bw.Write(c.pos.y); bw.Write(c.pos.z);
                bw.Write(c.yaw);
                bw.Write(c.pitch);
                bw.Write(c.flags);
            }
            BroadcastToClients(_unreliable, PWorldPlayers, ms.ToArray());
        }

        private void BroadcastEntities()
        {
            if (_peers.Count == 0 || EntityDirector.Instance == null) return;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            EntityDirector.Instance.WriteHostSnapshots(bw);
            BroadcastToClients(_unreliable, PEntities, ms.ToArray());
        }

        public void BroadcastRunState(int seed, int levelIndex, GameBoot.Mode mode, string killer = "")
        {
            if (!IsHost) return;
            // Reset peer downed/dead flags when starting a new run or advancing a floor
            if (mode == GameBoot.Mode.Playing || mode == GameBoot.Mode.Transition)
            {
                for (int i = 0; i < _peers.Count; i++) _peers[i].flags &= 0x0F;
                foreach (var kv in Avatars) if (kv.Value != null) { kv.Value.Downed = false; kv.Value.Dead = false; }
            }
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(seed);
            bw.Write(levelIndex);
            bw.Write((byte)mode);
            bw.Write(killer ?? "");
            BroadcastToClients(_reliable, PRunState, ms.ToArray());
        }

        public void BroadcastEntityCue(int netId, byte cueKind, int stolenClipId = 0)
        {
            if (!IsHost || _peers.Count == 0) return;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(netId);
            bw.Write(cueKind);
            bw.Write(stolenClipId);
            BroadcastToClients(_reliable, PEntityCue, ms.ToArray());
        }

        public void SendLiveVoice(byte[] mulaw)
        {
            if (!IsOnline || mulaw == null || mulaw.Length == 0) return;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(MySlot);
            bw.Write(mulaw.Length);
            bw.Write(mulaw);
            byte[] packet = ms.ToArray();
            if (Role == CoopRole.Host) BroadcastToClients(_unreliable, PVoiceLive, packet);
            else Send(_toHost, _unreliable, PVoiceLive, packet);
        }

        public void BroadcastStolenVoice(int clipId, int speakerSlot, string speakerName, byte[] mulaw)
        {
            if (!IsOnline || mulaw == null) return;
            byte[] packet = PackStolenVoice(clipId, speakerSlot, speakerName, mulaw);
            if (Role == CoopRole.Host) BroadcastToClients(_reliable, PVoiceStolen, packet);
            else Send(_toHost, _reliable, PVoiceStolen, packet);
        }

        private static byte[] PackStolenVoice(int clipId, int slot, string name, byte[] mulaw)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(clipId);
            bw.Write(slot);
            bw.Write(name ?? "EXPLORER");
            bw.Write(mulaw.Length);
            bw.Write(mulaw);
            return ms.ToArray();
        }

        private static void UnpackStolenVoice(BinaryReader br, out int clipId, out int slot, out string name, out byte[] mulaw)
        {
            clipId = br.ReadInt32();
            slot = br.ReadInt32();
            name = br.ReadString();
            int len = Mathf.Clamp(br.ReadInt32(), 0, 30000);
            mulaw = br.ReadBytes(len);
        }

        public void NotifyPickupTaken(Vector2Int cell)
        {
            if (!IsOnline) return;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(cell.x);
            bw.Write(cell.y);
            if (Role == CoopRole.Host) BroadcastToClients(_reliable, PPickupTaken, ms.ToArray());
            else Send(_toHost, _reliable, PPickupTaken, ms.ToArray());
        }

        public void NotifyExitUsed()
        {
            if (Role == CoopRole.Client && Ready)
                Send(_toHost, _reliable, PExitUsed, Array.Empty<byte>());
            else
                GameBoot.Instance?.ExitReached();
        }

        /// <summary>Called on Host (or Offline) when an SCP catches player `slot`.</summary>
        public void OnMonsterCaughtPlayer(int slot, Transform killerTr, string killerName)
        {
            var boot = GameBoot.Instance;
            if (boot == null) return;

            if (!IsOnline || TotalPlayerCount <= 1)
            {
                boot.Player?.Kill(killerTr, killerName);
                return;
            }

            if (!IsHost) return;

            // Mark target as Downed if another teammate is still standing; otherwise squad wipe!
            if (slot == 0 && boot.Player != null)
            {
                if (boot.Player.Downed || boot.Player.Dead) return;
                boot.Player.SetDowned(true, killerName);
            }
            else
            {
                var peer = _peers.Find(x => x.slot == slot);
                if (peer == null || (peer.flags & 0x30) != 0) return;
                peer.flags |= 16; // downed
                if (Avatars.TryGetValue(slot, out var av) && av != null) av.Downed = true;
                SendPlayerHit(slot, true, killerName);
                GameUI.Instance?.Toast($"{peer.name} WAS DOWNED BY {killerName}! REVIVE THEM!", 4f);
            }

            CheckSquadWipeOnHost(killerName);
        }

        private void CheckSquadWipeOnHost(string killerName)
        {
            if (!IsHost || GameBoot.Instance == null || GameBoot.Instance.State != GameBoot.Mode.Playing) return;
            var hostP = GameBoot.Instance.Player;
            bool anyStanding = hostP != null && !hostP.Dead && !hostP.Downed;
            for (int i = 0; i < _peers.Count; i++)
                if ((_peers[i].flags & 0x30) == 0) anyStanding = true;

            if (!anyStanding)
            {
                // Squad wiped!
                for (int i = 0; i < _peers.Count; i++)
                {
                    _peers[i].flags = (byte)((_peers[i].flags & ~16) | 32);
                    SendPlayerHit(_peers[i].slot, false, killerName);
                }
                if (hostP != null) hostP.Kill(null, killerName);
                BroadcastRunState(GameBoot.Instance.RunSeed, GameBoot.Instance.LevelIndex, GameBoot.Mode.Dead, killerName);
            }
        }

        private void SendPlayerHit(int slot, bool downedOnly, string killer)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(slot);
            bw.Write(downedOnly);
            bw.Write(killer ?? "ANOMALY");
            BroadcastToClients(_reliable, PPlayerHit, ms.ToArray());
        }

        public void RequestRevive(int targetSlot)
        {
            if (!IsOnline) return;
            if (IsHost) ApplyReviveAuthoritative(targetSlot);
            else
            {
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                bw.Write(targetSlot);
                Send(_toHost, _reliable, PRevive, ms.ToArray());
            }
        }

        private void ApplyReviveAuthoritative(int targetSlot)
        {
            if (targetSlot == 0 && GameBoot.Instance?.Player != null)
            {
                GameBoot.Instance.Player.SetDowned(false, "");
            }
            else
            {
                var peer = _peers.Find(x => x.slot == targetSlot);
                if (peer != null) peer.flags = (byte)(peer.flags & ~0x30);
                if (Avatars.TryGetValue(targetSlot, out var av) && av != null) { av.Downed = false; av.Dead = false; }
            }
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(targetSlot);
            BroadcastToClients(_reliable, PRevive, ms.ToArray());
        }

        /// <summary>Fills `targets` with all standing (non-downed, non-dead) players in the maze so SCPs hunt everyone.</summary>
        public static void GatherTargets(PlayerController local, List<PlayerTarget> targets)
        {
            targets.Clear();
            if (local != null && !local.Dead && !local.Downed)
            {
                targets.Add(new PlayerTarget
                {
                    slot = Instance != null ? Instance.MySlot : 0,
                    pos = local.transform.position,
                    head = local.Cam != null ? local.Cam.transform.position : local.transform.position + Vector3.up * 1.6f,
                    lookDir = local.Cam != null ? local.Cam.transform.forward : local.transform.forward,
                    flashlightOn = local.Flashlight != null && local.Flashlight.enabled,
                    flashlightAngle = local.Flashlight != null ? local.Flashlight.spotAngle : 52f,
                    flashlightRange = local.Flashlight != null ? local.Flashlight.range : 22f,
                    crouching = local.Crouching,
                    exposure = local.Exposure,
                    sanity01 = local.Sanity / Mathf.Max(1f, GameConfig.Current.player.sanityMax),
                    downed = local.Downed,
                    transform = local.transform
                });
            }

            if (Instance == null || !IsOnline) return;
            foreach (var kv in Instance.Avatars)
            {
                var av = kv.Value;
                if (av == null || av.Dead || av.Downed) continue;
                targets.Add(new PlayerTarget
                {
                    slot = av.Slot,
                    pos = av.transform.position,
                    head = av.EyePos,
                    lookDir = av.LookForward,
                    flashlightOn = av.FlashlightOn,
                    flashlightAngle = av.Flashlight != null ? av.Flashlight.spotAngle : 52f,
                    flashlightRange = av.Flashlight != null ? av.Flashlight.range : 22f,
                    crouching = av.Crouching,
                    exposure = av.FlashlightOn ? 0.8f : 0.35f,
                    sanity01 = 0.65f,
                    downed = av.Downed,
                    transform = av.transform
                });
            }
        }
    }
}
