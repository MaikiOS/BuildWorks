using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using P = OstrixMods.BuildWorks.BlueprintServerProtocol;

namespace OstrixMods.BuildWorks
{
    // Owns direct peer registrations. A routed RPC sender UID is never an authority credential.
    internal sealed class BlueprintServerRuntime : IDisposable
    {
        internal const string RequestRpc = "BW_ServerLibrary_v1_Request";
        internal const string ReplyRpc = "BW_ServerLibrary_v1_Reply";
        private readonly string root;
        private readonly Action<Exception> log;
        private readonly object local = new object();
        private readonly HashSet<ZRpc> registered = new HashSet<ZRpc>();
        private readonly List<BlueprintServerEndpoint> retiring = new List<BlueprintServerEndpoint>();
        private ZNet network;
        private long world;
        private bool server, disposed, failed;
        private double now;
        private ZRpc remote;
        private BlueprintServerEndpoint endpoint;
        internal BlueprintServerClient Client { get; private set; }

        internal BlueprintServerRuntime(string configPath, Action<Exception> log)
        { root = Path.Combine(configPath ?? throw new ArgumentNullException(nameof(configPath)), "BuildWorks", "server"); this.log = log; }

        internal void Tick(ZNet current, double time)
        {
            if (disposed) return;
            now = time;
            for (int i = retiring.Count - 1; i >= 0; --i)
            {
                retiring[i].Tick(time);
                if (!retiring[i].HasPendingWork) retiring.RemoveAt(i);
            }
            long uid = World(current);
            bool isServer = current && current.IsServer();
            if (!ReferenceEquals(network, current) || world != uid || server != isServer)
            { Reset(); network = current; world = uid; server = isServer; failed = false; }
            if (!current || uid == 0 || failed || retiring.Count != 0) return;
            try
            {
                if (server)
                {
                    if (endpoint == null)
                    {
                        BlueprintServerEndpoint created = null;
                        var library = new ServerBlueprintLibrary(Path.Combine(root, "world_" + world.ToString(CultureInfo.InvariantCulture)),
                            principal => created != null && created.IsAdministrator(principal));
                        created = new BlueprintServerEndpoint(library, world, Resolve, SendServer, log);
                        endpoint = created;
                    }
                    var ready = new HashSet<ZRpc>();
                    foreach (var peer in current.GetPeers())
                        if (peer != null && BlueprintServerAuthority.TryResolve(current, peer.m_rpc, out _)) ready.Add(peer.m_rpc);
                    foreach (var rpc in registered.Where(rpc => !ready.Contains(rpc)).ToArray())
                    { rpc.Unregister(RequestRpc); registered.Remove(rpc); endpoint.Disconnect(rpc); }
                    foreach (var rpc in ready)
                        if (registered.Add(rpc)) rpc.Register<ZPackage>(RequestRpc, ReceiveRequest);
                    if (Player.m_localPlayer)
                    {
                        if (Client == null) Client = new BlueprintServerClient(world, bytes =>
                        { if (!Context()) throw new IOException("Server context changed."); endpoint.Receive(local, bytes, now); }, "local-host");
                    }
                    else if (Client != null) { Client.Dispose(); Client = null; endpoint.Disconnect(local); }
                    Client?.Tick(time);
                    endpoint.Tick(time);
                }
                else
                {
                    ZRpc connection = CurrentServerConnection();
                    if (!ReferenceEquals(remote, connection))
                    {
                        Client?.Dispose(); Client = null;
                        remote?.Unregister(ReplyRpc); remote = connection;
                        if (connection != null)
                        {
                            connection.Register<ZPackage>(ReplyRpc, ReceiveReply);
                            Client = new BlueprintServerClient(world, bytes =>
                            {
                                if (!Context() || !ReferenceEquals(connection, CurrentServerConnection())) throw new IOException("Server connection changed.");
                                Send(connection, RequestRpc, bytes);
                            }, connection.GetSocket().GetHostName());
                        }
                    }
                    Client?.Tick(time);
                }
            }
            catch (Exception exception) { log?.Invoke(exception); Reset(); failed = true; }
        }

        private (string Principal, bool Administrator) Resolve(object connection)
        {
            if (!Context() || !server) return (null, false);
            if (ReferenceEquals(connection, local))
                return Player.m_localPlayer && network.LocalPlayerIsAdminOrHost() ? ("local-host", true) : (null, false);
            var rpc = connection as ZRpc;
            return BlueprintServerAuthority.TryResolve(network, rpc, out string principal)
                ? (principal, BlueprintServerAuthority.IsAdministrator(network, rpc, principal)) : (null, false);
        }
        private void ReceiveRequest(ZRpc connection, ZPackage package)
        {
            if (!Context() || !server || endpoint == null || !registered.Contains(connection)) return;
            try { endpoint.Receive(connection, Frame(package), now); }
            catch (Exception exception) { endpoint.Disconnect(connection); log?.Invoke(exception); }
        }
        private void ReceiveReply(ZRpc connection, ZPackage package)
        {
            if (!Context() || server || !ReferenceEquals(connection, remote) || !ReferenceEquals(connection, CurrentServerConnection())) return;
            Client?.Receive(Frame(package), now);
        }
        private void SendServer(object connection, byte[] frame)
        {
            if (!Context()) throw new IOException("Server context changed.");
            if (ReferenceEquals(connection, local)) Client?.Receive(frame, now);
            else
            {
                var rpc = connection as ZRpc;
                if (!BlueprintServerAuthority.TryResolve(network, rpc, out _)) throw new IOException("Peer disconnected.");
                Send(rpc, ReplyRpc, frame);
            }
        }
        private static byte[] Frame(ZPackage package) => package == null || package.Size() < 42 || package.Size() > P.MaximumFrameBytes ? null : package.GetArray();
        private static void Send(ZRpc connection, string method, byte[] frame)
        {
            if (frame == null || frame.Length > P.MaximumFrameBytes) throw new InvalidDataException();
            var package = new ZPackage(frame); // Write(byte[]) would add an unwanted length prefix.
            connection.Invoke(method, package);
        }
        private ZRpc CurrentServerConnection()
        {
            var peer = network.GetServerPeer(); var rpc = network.GetServerRPC();
            return peer != null && peer.IsReady() && peer.m_uid != 0 && rpc != null && rpc.IsConnected() &&
                ReferenceEquals(peer.m_rpc, rpc) && ReferenceEquals(peer.m_socket, rpc.GetSocket()) && peer.m_socket != null && peer.m_socket.IsConnected()
                ? rpc : null;
        }
        private bool Context() => !disposed && network && ReferenceEquals(network, ZNet.instance) && world != 0 && World(network) == world && network.IsServer() == server;
        private static long World(ZNet net)
        {
            if (!net) return 0;
            try { return net.GetWorldUID(); }
            catch (NullReferenceException) { return 0; } // Native m_world is absent during login/teardown.
        }
        private void Reset()
        {
            Client?.Dispose(); Client = null;
            remote?.Unregister(ReplyRpc); remote = null;
            foreach (var rpc in registered) rpc.Unregister(RequestRpc);
            registered.Clear();
            if (endpoint != null) { endpoint.Dispose(); retiring.Add(endpoint); endpoint = null; }
        }
        internal void Suspend()
        {
            if (disposed) return;
            Reset(); network = null; world = 0; failed = false;
        }
        public void Dispose()
        { if (disposed) return; Reset(); disposed = true; network = null; }
    }
}
