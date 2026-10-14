using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

// Inert native boundary; both runtime and authority execute the actual production source.
internal sealed class Socket
{
    internal string Host;
    internal bool Connected = true;
    public bool IsConnected() => Connected;
    public string GetHostName() => Host;
}
internal sealed class ZPackage
{
    private byte[] bytes;
    internal int ArrayReads;
    public ZPackage() { bytes = Array.Empty<byte>(); }
    public ZPackage(byte[] data) { bytes = (byte[])data.Clone(); }
    public int Size() => bytes.Length;
    public byte[] GetArray() { ++ArrayReads; return (byte[])bytes.Clone(); }
    public void Write(byte[] data) { bytes = BitConverter.GetBytes(data.Length).Concat(data).ToArray(); }
}
internal sealed class ZRpc
{
    internal Socket Socket;
    internal bool Connected = true;
    internal readonly Dictionary<string, Delegate> Handlers = new Dictionary<string, Delegate>();
    internal Action<string, ZPackage> Sending;
    public bool IsConnected() => Connected;
    public Socket GetSocket() => Socket;
    public void Register<T>(string name, Action<ZRpc, T> handler) => Handlers[name] = handler;
    public void Unregister(string name) => Handlers.Remove(name);
    public void Invoke(string name, params object[] args) => Sending?.Invoke(name, (ZPackage)args.Single());
    internal Action<ZRpc, ZPackage> Handler(string name) => (Action<ZRpc, ZPackage>)Handlers[name];
}
internal sealed class ZNetPeer
{
    public ZRpc m_rpc;
    public Socket m_socket;
    public long m_uid;
    public bool Ready = true;
    public bool IsReady() => Ready;
}
internal sealed class Player
{
    internal static Player m_localPlayer;
    public static implicit operator bool(Player player) => player != null;
}
internal sealed class ZNet
{
    public static ZNet instance;
    internal bool Server = true, Loading;
    internal long World = 1;
    internal ZNetPeer ServerPeer;
    internal List<ZNetPeer> Peers = new List<ZNetPeer>();
    internal HashSet<string> Admins = new HashSet<string>();
    public bool IsServer() => Server;
    public long GetWorldUID() => Loading ? throw new NullReferenceException() : World;
    public ZNetPeer GetServerPeer() => ServerPeer;
    public ZRpc GetServerRPC() => ServerPeer?.m_rpc;
    public List<ZNetPeer> GetPeers() => Peers;
    public bool IsAdmin(string principal) => Admins.Contains(principal);
    public bool LocalPlayerIsAdminOrHost() => Server;
    public static implicit operator bool(ZNet net) => net != null;
    public static bool operator !(ZNet net) => net == null;
}

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ServerRuntimeChecks
    {
        internal static void Run(string folder, byte[] original)
        {
            double now = 1;
            var errors = new List<Exception>();
            var host = ZNet.instance = new ZNet { World = 701 };
            var administrator = Peer("admin", 1); var player = Peer("player", 1); // Duplicate routed UID confers no privilege.
            host.Peers.Add(administrator); host.Peers.Add(player); host.Admins.Add("admin");
            using (var runtime = new BlueprintServerRuntime(folder, errors.Add))
            using (var client = new BlueprintServerClient(host.World,
                bytes => administrator.m_rpc.Handler(BlueprintServerRuntime.RequestRpc)(administrator.m_rpc, new ZPackage(bytes))))
            {
                administrator.m_rpc.Sending = (name, package) =>
                { Require(name == BlueprintServerRuntime.ReplyRpc, "Server used routed/unexpected response"); client.Receive(package.GetArray(), now); };
                runtime.Tick(host, now);
                Require(runtime.Client == null && administrator.m_rpc.Handlers.ContainsKey(BlueprintServerRuntime.RequestRpc), "Dedicated host failed registration without a player");
                WaitList(client, runtime, host);
                var publish = client.Publish(original, 0);
                Pump(() => publish.IsCompleted, () => { client.Tick(now); runtime.Tick(host, now); });
                var item = publish.GetAwaiter().GetResult().Reply.Item;
                var read = client.Read(item);
                Pump(() => read.IsCompleted, () => { client.Tick(now); runtime.Tick(host, now); });
                Require(read.GetAwaiter().GetResult().Document.SequenceEqual(original), "Native package adapter changed protocol bytes");
                Require(Directory.GetFiles(Path.Combine(folder, "BuildWorks", "server", "world_701"), "*.bwserver.zip").Length == 1,
                    "Server publication not scoped to world UID");
                host.Admins.Clear();
                var denied = client.Delete(item);
                Require(denied.IsFaulted && Failure(denied) == P.Failure.Denied, "Native admin revocation was cached");
                host.Admins.Add("admin");
                int replies = 0; player.m_rpc.Sending = (_, __) => ++replies;
                administrator.m_rpc.Handler(BlueprintServerRuntime.RequestRpc)(new ZRpc { Socket = administrator.m_socket }, new ZPackage(new byte[50]));
                Require(replies == 0, "Unknown callback connection accepted");

                Player.m_localPlayer = new Player(); runtime.Tick(host, now);
                Require(runtime.Client != null, "Local host has no client");
                var localList = WaitList(runtime.Client, runtime, host);
                Require(localList.Reply.Administrator && localList.Reply.Items.Single().Id == item.Id, "Local host did not use same server library");
                host.World = 702; runtime.Tick(host, now);
                Pump(() => runtime.Client != null, () => runtime.Tick(host, now));
                Require(WaitList(runtime.Client, runtime, host).Reply.Total == 0, "World switch exposed old library");
                runtime.Suspend();
                Require(runtime.Client == null && administrator.m_rpc.Handlers.Count == 0, "Disable retained callbacks or client");
                Pump(() => runtime.Client != null, () => runtime.Tick(host, now));
                Require(WaitList(runtime.Client, runtime, host).Reply.Total == 0, "Resume changed the world library");
                host.Loading = true; runtime.Tick(host, now);
                Require(runtime.Client == null && administrator.m_rpc.Handlers.Count == 0, "Loading/teardown retained callbacks");
                host.Loading = false;
            }
            Require(administrator.m_rpc.Handlers.Count == 0 && errors.Count == 0, "Server teardown failed or logged unexpected error");

            Player.m_localPlayer = null;
            var serverPeer = Peer("server", 8);
            var remote = ZNet.instance = new ZNet { Server = false, World = 800, ServerPeer = serverPeer };
            using (var runtime = new BlueprintServerRuntime(folder, errors.Add))
            {
                var sent = new List<P.Message>();
                serverPeer.m_rpc.Sending = (name, package) =>
                { Require(name == BlueprintServerRuntime.RequestRpc, "Client used unexpected RPC"); sent.Add(P.Decode(package.GetArray())); };
                runtime.Tick(remote, now);
                var client = runtime.Client; var list = client.List(); var request = sent.Last();
                var callback = serverPeer.m_rpc.Handler(BlueprintServerRuntime.ReplyRpc);
                var reply = P.Encode(new P.Message
                {
                    Kind = P.Command.ListResult,
                    World = remote.World,
                    Request = request.Request,
                    Session = Guid.NewGuid(),
                    Total = 0,
                    Items = Array.Empty<P.Item>()
                });
                callback(new ZRpc { Socket = serverPeer.m_socket }, new ZPackage(reply));
                Require(!list.IsCompleted, "Spoofed server connection delivered reply");
                callback(serverPeer.m_rpc, new ZPackage(reply));
                Require(list.Result.Reply.Total == 0 && client.Ready && !client.IsAdministrator, "Remote client bootstrap failed");
                var oversizedTask = client.List(); var oversized = new ZPackage(new byte[P.MaximumFrameBytes + 1]);
                callback(serverPeer.m_rpc, oversized);
                Require(oversized.ArrayReads == 0 && Failure(oversizedTask) == P.Failure.Invalid, "Oversized package copied or accepted");
                var pending = client.List(); serverPeer.m_rpc.Connected = false; runtime.Tick(remote, now);
                Require(runtime.Client == null && Failure(pending) == P.Failure.Unavailable && serverPeer.m_rpc.Handlers.Count == 0,
                    "Disconnect retained client/callback/task");
                serverPeer.m_rpc.Connected = true;
                remote.ServerPeer = Peer("replacement", 9); runtime.Tick(remote, now);
                Require(runtime.Client != null && !ReferenceEquals(runtime.Client, client), "Reconnection reused old client state");
                callback(serverPeer.m_rpc, new ZPackage(reply));
                Require(!runtime.Client.Ready, "Late old callback completed new connection");
            }
            Require(errors.Count == 0 && remote.ServerPeer.m_rpc.Handlers.Count == 0, "Client teardown failed");
            ZNet.instance = null;
            Console.WriteLine("PASS actual native runtime source on inert host: direct typed registration/package bytes, dedicated and local host, world UID separation, current native admin, forged callback denial, bounded package before copy, disconnect/reconnect/teardown. No live socket/game evidence.");

            BlueprintServerClient.Result WaitList(BlueprintServerClient client, BlueprintServerRuntime runtime, ZNet net)
            {
                while (true)
                {
                    now += .5; var task = client.List();
                    Pump(() => task.IsCompleted, () => runtime.Tick(net, now));
                    if (task.Status == TaskStatus.RanToCompletion) return task.Result;
                    Require(Failure(task) == P.Failure.Busy, "Unexpected bootstrap error");
                    Thread.Sleep(1);
                }
            }
            void Pump(Func<bool> done, Action tick)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (!done()) { Require(DateTime.UtcNow < deadline, "Native runtime did not settle"); now += .001; tick(); Thread.Sleep(1); }
            }
        }
        private static P.Failure Failure(Task<BlueprintServerClient.Result> task)
        { try { task.GetAwaiter().GetResult(); throw new Exception("Expected client failure"); } catch (BlueprintServerClient.Failure failure) { return failure.Code; } }
        private static ZNetPeer Peer(string principal, long uid)
        { var socket = new Socket { Host = principal }; return new ZNetPeer { m_uid = uid, m_socket = socket, m_rpc = new ZRpc { Socket = socket } }; }
        private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
    }
}
