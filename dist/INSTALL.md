# RoonBroadcastRelay - 3-Site Mesh Build

This package is a **separate, parallel install** of a modified
`RoonBroadcastRelay` that supports three or more sites. It installs to
`/opt/roonrelay-mesh/` and runs as `roonrelay-mesh.service`. **Your existing
`roonrelay` install in `/opt/roonrelay/` is not touched.** You can run the new
build, stop it, and the original keeps working unchanged.

## What changed vs. upstream

- New `RemoteRelayIps` array field in `appsettings.json` — list every other
  relay (full mesh) instead of just one.
- Legacy single-string `RemoteRelayIp` field still accepted (merged at
  startup), so old configs keep parsing.
- A relay silently skips any peer address that matches one of its own
  interfaces, and de-duplicates the merged list.
- License unchanged: GPL-3.0 (see `LICENSE.md`). All upstream attribution
  preserved.

## Topology this package ships configs for

| Site | Relay LAN IP | LAN subnet | Tunnel peers |
|------|--------------|------------|--------------|
| Bangkok | 10.0.3.20 | 10.0.3.0/24 | 192.168.1.30, 10.0.2.14 |
| HQ | 192.168.1.30 | 192.168.1.0/24 | 10.0.3.20, 10.0.2.14 |
| Denver | 10.0.2.14 | 10.0.2.0/24 | 10.0.3.20, 192.168.1.30 |

All three relays speak to each other over UDP `9004`.

## Prerequisites — do these once, before testing

1. **WireGuard / routing**: each site must be able to reach the other two
   LAN subnets. In particular, Denver↔Bangkok and Denver↔HQ are new paths
   if they did not exist before. Confirm with `ping` from each relay to the
   other two relay IPs.
2. **Firewall**: UDP `9004` must be permitted in both directions between
   every pair of relays. Bangkok↔HQ already works for you; add
   Denver↔Bangkok and Denver↔HQ.
3. **WireGuard AllowedIPs** on each side must include the other LAN subnets
   so traffic actually routes across the tunnels.
4. Subnets must not overlap (yours don't: 10.0.3.0/24, 192.168.1.0/24,
   10.0.2.0/24 — good).

## Install — run on each of the three relay VMs

These steps are identical on Bangkok, HQ, and Denver except the config and
binary choice.

```bash
# 1. Create the parallel install directory (does not touch /opt/roonrelay)
sudo mkdir -p /opt/roonrelay-mesh

# 2. Copy the correct binary. Pick ONE of these lines based on the VM's CPU:
#    Most x86_64 Linux VMs:
sudo cp bin/RoonBroadcastRelay-linux-x64 /opt/roonrelay-mesh/RoonBroadcastRelay
#    Raspberry Pi / arm64 Linux VMs:
sudo cp bin/RoonBroadcastRelay-linux-arm64 /opt/roonrelay-mesh/RoonBroadcastRelay
sudo chmod +x /opt/roonrelay-mesh/RoonBroadcastRelay

# 3. Copy the config matching THIS site. Pick ONE:
sudo cp configs/appsettings.Bangkok.json /opt/roonrelay-mesh/appsettings.json
# or
sudo cp configs/appsettings.HQ.json      /opt/roonrelay-mesh/appsettings.json
# or
sudo cp configs/appsettings.Denver.json  /opt/roonrelay-mesh/appsettings.json

# 4. Install the systemd unit (does not enable it yet)
sudo cp systemd/roonrelay-mesh.service /etc/systemd/system/roonrelay-mesh.service
sudo systemctl daemon-reload
```

At this point nothing is running yet. Both services exist; only the
original `roonrelay.service` is active.

## Test (one site at a time, then all three)

Both relays cannot run at the same time on the same machine — they would
fight over the Roon discovery ports (9003 etc.). So testing means stopping
the old one briefly and starting the new one. On each site:

```bash
# Stop the existing relay (no permanent change — still enabled)
sudo systemctl stop roonrelay

# Start the new mesh build (no permanent change — not enabled)
sudo systemctl start roonrelay-mesh

# Watch the logs
journalctl -u roonrelay-mesh -f
```

A healthy startup log shows:

- Two `Remote relay:` lines (one per peer).
- After a Roon discovery announcement: `TUNNEL ->` going to both peers.
- Incoming `TUNNEL <-` lines as the other two relays send to you.

Open Roon at each site and verify that endpoints from the other two sites
appear in the device list.

## Commit to the new build (only after all three sites work)

```bash
# Make the mesh build the one that starts on boot, retire the old one
sudo systemctl disable roonrelay
sudo systemctl enable roonrelay-mesh
```

The original `/opt/roonrelay/` directory still sits there, untouched, as a
fallback you can revert to at any time.

## Rollback — fastest path back to the working 2-site setup

This is the whole reason for the parallel install. On any (or all) sites:

```bash
sudo systemctl stop roonrelay-mesh
sudo systemctl start roonrelay
```

That's it — you're back on the original binary and config in
`/opt/roonrelay/`. If you also did the `enable`/`disable` step above and
want to revert that:

```bash
sudo systemctl disable roonrelay-mesh
sudo systemctl enable roonrelay
```

Full removal (only if you decide not to keep the mesh build at all):

```bash
sudo systemctl stop roonrelay-mesh
sudo systemctl disable roonrelay-mesh
sudo rm /etc/systemd/system/roonrelay-mesh.service
sudo systemctl daemon-reload
sudo rm -rf /opt/roonrelay-mesh
```

## Troubleshooting

- **Log shows only one `Remote relay:` line** — the config still has the
  legacy single `RemoteRelayIp`. Replace with `RemoteRelayIps` array.
- **`TUNNEL ->` is logged but no `TUNNEL <-` from one peer** — firewall or
  WireGuard between this site and that peer is blocking UDP 9004.
- **`WARNING: Cannot bind ... port may be in use`** — `roonrelay.service`
  is still running on this machine. Stop it first.
- **`WARNING: ignoring remote relay X (matches a local interface)`** —
  expected, just means the config listed this relay's own IP. Harmless.

## Building from source (optional)

The complete modified source is in `source/`. To rebuild a binary:

```bash
cd source
dotnet publish RoonBroadcastRelay.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

Replace `linux-x64` with `linux-arm64` for ARM. Requires .NET 8 SDK.

## License

GPL-3.0. See `LICENSE.md`. This is a modified version of
`simonefil/RoonBroadcastRelay` (https://github.com/simonefil/RoonBroadcastRelay).
