using System;

namespace VerdantsAscent.Terrain.Lifecycle
{
    // Shared admin / sender guard for every Galaxies RPC handler.
    //
    // 0.1.60 security fix: the old version looked up ServerSync.AdminSyncing.IsAdmin
    // by reflection, never found a public one, and then returned TRUE for every peer.
    // It now FAILS CLOSED:
    //   * Server: the sender's peer (ZNet.GetPeer) must be on ZNet's admin list
    //     (FiresCore.Sync.AdminSyncing.IsAdmin). No peer = not admin.
    //   * Local call (sender 0 or our own uid): the host/server counts as admin;
    //     a client counts only when it is a local admin.
    //   * Client receiving from someone else: only the server is trusted
    //     (the server relays admin-approved ops).
    public static class AdminCheck
    {
        public static bool IsLocalSender(long sender)
        {
            if (sender == 0L) return true;
            try { return sender == ZNet.GetUID(); } catch { return false; }
        }

        public static long ServerPeerId()
        {
            var znet = ZNet.instance;
            if (znet == null) return 0L;
            if (znet.IsServer()) return ZNet.GetUID();
            var sp = znet.GetServerPeer();
            return sp != null ? sp.m_uid : 0L;
        }

        // True only when the sender is the server (or, on the server, our own loopback).
        public static bool IsFromServer(long sender)
        {
            var znet = ZNet.instance;
            if (znet == null) return false;
            if (znet.IsServer()) return IsLocalSender(sender);
            var sp = znet.GetServerPeer();
            return sp != null && sp.m_uid != 0L && sender == sp.m_uid;
        }

        public static bool IsAdmin(long sender)
        {
            var znet = ZNet.instance;
            if (znet == null) return false;
            try
            {
                if (IsLocalSender(sender))
                    return znet.IsServer() || FiresCore.Sync.AdminSyncing.IsLocalAdmin();
                if (znet.IsServer())
                    return FiresCore.Sync.AdminSyncing.IsAdmin(sender);
                return IsFromServer(sender);
            }
            catch (Exception ex)
            {
                FiresLogger.LogWarning($"[AdminCheck] admin check threw for sender {sender}: {ex.Message} - DENY.");
                return false;
            }
        }

        public static bool IsSaneRadius(float r, float max = 256f)
        {
            return !float.IsNaN(r) && !float.IsInfinity(r) && r >= 0f && r <= max;
        }

        public static bool IsSaneVector(UnityEngine.Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        // 0.1.60 security: a world name sent by a server must never leave the cache folder.
        // Drops separators, drive colons and invalid file-name chars (legit names keep their old file),
        // then checks the full path stays inside the folder. A refused name falls back to
        // "invalid_world" inside the same folder, so callers never get null.
        public static string SafeCacheFile(string folder, string worldName, string suffix)
        {
            string root = System.IO.Path.GetFullPath(folder).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            string fallback = System.IO.Path.Combine(root, "invalid_world" + suffix);
            if (string.IsNullOrEmpty(worldName) || worldName.Length > 128) { FiresLogger.LogWarning("[AdminCheck] cache world name empty or too long - using invalid_world."); return fallback; }
            var bad = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(worldName.Length);
            foreach (char ch in worldName)
                sb.Append(ch == '/' || ch == '\\' || ch == ':' || ch < 32 || System.Array.IndexOf(bad, ch) >= 0 ? '_' : ch);
            string safe = sb.ToString().Trim().Trim('.');
            if (safe.Length == 0) { FiresLogger.LogWarning("[AdminCheck] cache world name had no usable characters - using invalid_world."); return fallback; }
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, safe + suffix));
            if (!full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase) || full.IndexOf(System.IO.Path.DirectorySeparatorChar, root.Length) >= 0)
            {
                FiresLogger.LogWarning($"[AdminCheck] cache path for world '{worldName}' escapes {root} - using invalid_world.");
                return fallback;
            }
            if (safe != worldName) FiresLogger.LogWarning($"[AdminCheck] world name '{worldName}' sanitised to '{safe}' for the cache file.");
            return full;
        }

        // Kept for API compatibility (was a reflection-cache reset).
        public static void Reset() { }
    }
}
