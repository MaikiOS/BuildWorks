using System;
using System.Linq;

namespace OstrixMods.BuildWorks
{
    // Direct ZRpc callbacks only. Routed sender IDs and request role/name fields are not identities.
    internal static class BlueprintServerAuthority
    {
        internal static bool TryResolve(ZNet server, ZRpc connection, out string principal)
        {
            principal = null;
            if (!server || !server.IsServer() || connection == null || !connection.IsConnected()) return false;
            var peers = server.GetPeers();
            ZNetPeer peer = null;
            if (peers == null) return false;
            foreach (var candidate in peers)
                if (candidate != null && ReferenceEquals(candidate.m_rpc, connection))
                {
                    if (peer != null) return false;
                    peer = candidate;
                }
            if (peer == null || !peer.IsReady() || peer.m_uid == 0 || peer.m_socket == null ||
                !ReferenceEquals(peer.m_socket, connection.GetSocket()) || !peer.m_socket.IsConnected()) return false;
            string host = peer.m_socket.GetHostName();
            if (string.IsNullOrWhiteSpace(host) || host.Length > 256 || host.Any(char.IsControl)) return false;
            principal = host;
            return true;
        }

        internal static bool IsAdministrator(ZNet server, ZRpc connection, string authenticatedPrincipal)
        {
            return TryResolve(server, connection, out string current) &&
                string.Equals(current, authenticatedPrincipal, StringComparison.Ordinal) && server.IsAdmin(current);
        }
    }
}
