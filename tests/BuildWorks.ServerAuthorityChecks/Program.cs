using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks;

// Inert host boundary; authority decisions execute the actual production source.
internal sealed class Socket
{
    internal string Host;
    internal bool Connected = true;
    public bool IsConnected() => Connected;
    public string GetHostName() => Host;
}
internal sealed class ZRpc
{
    internal Socket Socket;
    internal bool Connected = true;
    public bool IsConnected() => Connected;
    public Socket GetSocket() => Socket;
}
internal sealed class ZNetPeer
{
    public ZRpc m_rpc;
    public Socket m_socket;
    public long m_uid;
    public bool Ready = true;
    public bool IsReady() => Ready;
}
internal sealed class ZNet
{
    public bool Server = true;
    public List<ZNetPeer> Peers = new List<ZNetPeer>();
    public HashSet<string> Admins = new HashSet<string>(StringComparer.Ordinal);
    public bool IsServer() => Server;
    public List<ZNetPeer> GetPeers() => Peers;
    public bool IsAdmin(string principal) => Admins.Contains(principal);
    public static implicit operator bool(ZNet net) => net != null;
    public static bool operator !(ZNet net) => net == null;
}
internal static class Program
{
    private static int Main()
    {
        var host = new ZNet();
        var adminSocket = new Socket { Host = "Steam_admin" };
        var playerSocket = new Socket { Host = "Steam_player" };
        var admin = new ZNetPeer { m_uid = 1, m_socket = adminSocket, m_rpc = new ZRpc { Socket = adminSocket } };
        var player = new ZNetPeer { m_uid = 2, m_socket = playerSocket, m_rpc = new ZRpc { Socket = playerSocket } };
        host.Peers.Add(admin); host.Peers.Add(player); host.Admins.Add(adminSocket.Host);
        Require(BlueprintServerAuthority.TryResolve(host, player.m_rpc, out string principal) && principal == playerSocket.Host,
            "Ready peer identity did not come from its socket");
        Require(!BlueprintServerAuthority.IsAdministrator(host, player.m_rpc, adminSocket.Host), "Claimed admin principal bypassed connection binding");
        player.m_uid = admin.m_uid;
        Require(!BlueprintServerAuthority.IsAdministrator(host, player.m_rpc, playerSocket.Host), "Forged routed UID granted administrator rights");
        Require(BlueprintServerAuthority.IsAdministrator(host, admin.m_rpc, adminSocket.Host), "Real administrator refused");
        host.Admins.Clear();
        Require(!BlueprintServerAuthority.IsAdministrator(host, admin.m_rpc, adminSocket.Host), "Admin revocation was cached");
        host.Admins.Add(adminSocket.Host);
        player.Ready = false; Refused(host, player.m_rpc); player.Ready = true;
        player.m_rpc.Connected = false; Refused(host, player.m_rpc); player.m_rpc.Connected = true;
        playerSocket.Connected = false; Refused(host, player.m_rpc); playerSocket.Connected = true;
        var other = new ZRpc { Socket = adminSocket }; Refused(host, other);
        host.Peers.Add(player); Refused(host, player.m_rpc); host.Peers.RemoveAt(host.Peers.Count - 1);
        player.m_socket = adminSocket; Refused(host, player.m_rpc); player.m_socket = playerSocket;
        host.Peers.Remove(player); Refused(host, player.m_rpc); host.Peers.Add(player);
        playerSocket.Host = "\nadmin"; Refused(host, player.m_rpc); playerSocket.Host = "Steam_player";
        player.m_uid = 0; Refused(host, player.m_rpc); player.m_uid = 2;
        host.Server = false; Refused(host, admin.m_rpc); host.Server = true;
        Refused(null, admin.m_rpc); Refused(host, null);
        Console.WriteLine("PASS actual server authority source: direct peer/RPC/socket binding, forged UID/principal denial, native admin revocation, not-ready/disconnected/replaced/client/null refusal. Inert host only; no real network or RPC registration tested.");
        return 0;
    }
    private static void Refused(ZNet host, ZRpc rpc) => Require(!BlueprintServerAuthority.TryResolve(host, rpc, out string principal) && principal == null,
        "Invalid connection retained authority");
    private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
}
