using System.Collections.Generic;

namespace RoonBroadcastRelay
{
    /// <summary>
    /// Configuration settings for the Roon Relay service.
    /// </summary>
    public class RelayConfig
    {
        /// <summary>
        /// Site identifier used in logs. Example: "SiteA", "Office".
        /// </summary>
        public string SiteName { get; set; }

        /// <summary>
        /// UDP port for tunnel communication between remote relays.
        /// </summary>
        public int TunnelPort { get; set; }

        /// <summary>
        /// IP address of a single remote relay for tunnel connection. Can be null.
        /// Legacy field, kept for backward compatibility with two-site configs.
        /// For three or more sites use <see cref="RemoteRelayIps"/> instead.
        /// Any value set here is merged with <see cref="RemoteRelayIps"/> at startup.
        /// </summary>
        public string RemoteRelayIp { get; set; }

        /// <summary>
        /// IP addresses of all remote relays for tunnel connections. Can be null or empty.
        /// Use this to build a full mesh across three or more sites: list every other
        /// relay here. Merged with the legacy <see cref="RemoteRelayIp"/> field at startup.
        /// </summary>
        public List<string> RemoteRelayIps { get; set; }

        /// <summary>
        /// Local network interfaces for listening and forwarding Roon packets.
        /// </summary>
        public List<InterfaceConfig> LocalInterfaces { get; set; }

        /// <summary>
        /// Optional list of unicast targets to forward packets to.
        /// </summary>
        public List<string> UnicastTargets { get; set; }

        /// <summary>
        /// Protocol enable/disable settings. If null, only RAAT is enabled.
        /// </summary>
        public ProtocolSettings Protocols { get; set; }
    }
}