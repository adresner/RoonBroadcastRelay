# 3-Site Roon Mesh — Deployment Plan

This is a complete, in-order checklist to stand up the new 3-site mesh relay
on three brand-new LXCs **without touching the running production relays**.
Each new LXC gets a new IP, builds the modified binary from your GitHub fork,
and stays idle until you choose to cut over.

## Target layout

| Site | New LXC IP | Existing relay (untouched) | LAN subnet |
|------|------------|----------------------------|------------|
| Site 1 | **10.0.3.22** | 10.0.3.20 | 10.0.3.0/24 |
| Site 2 | **192.168.1.37** | 192.168.1.30 | 192.168.1.0/24 |
| Site 3 | **10.0.2.17** | 10.0.2.14 | 10.0.2.0/24 |

Roon Server stays where it is at 10.0.3.15.

---

## Phase 1 — Create three fresh LXCs (Proxmox, ~5 min each)

At each Proxmox host, create one container with these settings.

**Critical:** the container must be **privileged**. Untick "Unprivileged
container" on the General tab. Raw sockets (the IP-spoofing trick the relay
uses) are blocked in unprivileged LXCs, and the binary will fail at startup
with "Permission denied" on socket creation. Your existing relay LXCs must
already be privileged or they wouldn't be working.

General tab:
- CT ID: any free ID
- Hostname: `roonrelay-mesh` (or `roonrelay-bkk`, `-hq`, `-den` if you like)
- Untick "Unprivileged container"

Template tab:
- Ubuntu 26.04 LTS template (or 24.04 if 26.04 isn't downloaded yet — either works)

Disks: 4 GB is plenty.
CPU: 1 core.
Memory: 512 MB.

Network tab:
- Bridge: your LAN bridge (the same one the existing relay uses at that site)
- IPv4: **Static**, with the IP from the table above and a /24 netmask
- Gateway: the LAN gateway you already use
- IPv6: None

Start the container after creation. SSH into it once from your laptop and
set a root password (or push your public key) so the Claude Code prompts in
Phase 4 can get in.

---

## Phase 2 — Push the modified source to your GitHub fork (one-time, ~10 min)

Easiest path for a first-time GitHub user:

1. **Fork the upstream repo.** In a browser go to
   <https://github.com/simonefil/RoonBroadcastRelay>, click **Fork** (top right),
   accept the defaults. You now own
   `https://github.com/<your-github-username>/RoonBroadcastRelay`.

2. **Install GitHub Desktop** on your laptop from <https://desktop.github.com>
   and sign in with your GitHub account. (Skip this if you already use the
   `git` CLI.)

3. **Clone your fork.** In GitHub Desktop: File → Clone repository → pick your
   fork. It clones to e.g. `~/Documents/GitHub/RoonBroadcastRelay`.

4. **Copy your modifications over the clone.** Open the cloned folder and
   the `RoonBroadCast` folder side by side. Copy these files from
   `RoonBroadCast` into the clone, overwriting where they exist and creating
   folders where needed:
   - `RoonBroadcastRelay/RelayConfig.cs`
   - `RoonBroadcastRelay/RoonBroadcastRelay.cs`
   - `RoonBroadcastRelay/Program.cs`
   - `EXAMPLES.md`
   - `README.md`
   - new folder `examples-3site/` with all three `appsettings.*.json` files
   - new folder `dist/systemd/` with `roonrelay-mesh.service`
   - new file `dist/INSTALL.md`
   - new file `DEPLOYMENT.md` (this file)

5. **Commit and push.** Back in GitHub Desktop, you'll see all your changes
   listed. Summary: `Add 3-site full-mesh support (RemoteRelayIps)`. Click
   **Commit to main**, then **Push origin**.

6. **Sanity check.** Reload your fork's page on GitHub. You should see
   `examples-3site/` and `dist/` folders and the modified .cs files.

The full URL you'll plug into the prompts in Phase 4 is:
`https://github.com/<your-github-username>/RoonBroadcastRelay.git`

---

## Phase 3 — Confirm SSH from your laptop to each LXC (one-time, ~2 min)

From your laptop, all three of these must succeed before you start Phase 4:

```bash
ssh root@10.0.3.22    # Site 1 new LXC
ssh root@192.168.1.37 # Site 2 new LXC
ssh root@10.0.2.17    # Site 3 new LXC
```

If you don't want to type passwords every step, run
`ssh-copy-id root@<each-ip>` once per LXC.

---

## Phase 4 — Deploy to each LXC

Install Claude Code on your laptop (if not already), open it in any
directory, and paste **one prompt at a time** into it. Each prompt finishes
without starting the service, so a half-deployed state is harmless.

Before running them, set the GitHub URL once at the top of your head: replace
every `<YOUR-FORK-URL>` below with
`https://github.com/<your-github-username>/RoonBroadcastRelay.git`.

### Prompt 1 — Site 1 (new LXC at 10.0.3.22)

```
Deploy the 3-site Roon mesh relay to a fresh Ubuntu LXC over SSH.

Target host:
- Hostname/role: Site 1 (new mesh relay, parallel to existing 10.0.3.20)
- SSH target: root@10.0.3.22
- GitHub fork: <YOUR-FORK-URL>

Site identity this LXC should run as:
- SiteName: "Site 1"
- LocalIp: 10.0.3.22
- BroadcastAddress: 10.0.3.255
- SubnetMask: 255.255.255.0
- RemoteRelayIps (tunnel peers): ["192.168.1.37", "10.0.2.17"]
- TunnelPort: 9004
- All four protocols enabled (Raat, AirPlay, Ssdp, Squeezebox)

Do the install over SSH, in this order. Stop and report any step that fails
before moving on.

1. SSH in and confirm identity:
   - Run `ip -4 addr show` and confirm 10.0.3.22 is present on an interface.
   - Run `hostname` and `uname -m`. Confirm x86_64.

2. Install build tools and runtime deps:
   - apt-get update
   - apt-get install -y git wget curl ca-certificates dotnet-sdk-8.0
   - If dotnet-sdk-8.0 is unavailable on this Ubuntu release, install via the
     Microsoft package feed (https://learn.microsoft.com/dotnet/core/install/linux-ubuntu).

3. Clone the fork to /tmp:
   - cd /tmp
   - git clone <YOUR-FORK-URL> RoonBroadcastRelay
   - cd RoonBroadcastRelay

4. Build a self-contained single-file binary:
   - dotnet publish RoonBroadcastRelay/RoonBroadcastRelay.csproj \
       -c Release -r linux-x64 --self-contained true \
       -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
       -p:EnableCompressionInSingleFile=true -o ./publish
   - Confirm the file ./publish/RoonBroadcastRelay exists and is ~30+ MB.

5. Install into /opt/roonrelay-mesh (parallel to any existing /opt/roonrelay):
   - mkdir -p /opt/roonrelay-mesh
   - cp ./publish/RoonBroadcastRelay /opt/roonrelay-mesh/RoonBroadcastRelay
   - chmod +x /opt/roonrelay-mesh/RoonBroadcastRelay
   - cp examples-3site/appsettings.Site1.json /opt/roonrelay-mesh/appsettings.json
   - cp dist/systemd/roonrelay-mesh.service /etc/systemd/system/
   - systemctl daemon-reload

6. Verify the deployed config without starting the service:
   - cat /opt/roonrelay-mesh/appsettings.json
   - Confirm SiteName="Site 1", LocalIp="10.0.3.22",
     RemoteRelayIps contains exactly "192.168.1.37" and "10.0.2.17".

7. Confirm the service is registered but NOT enabled or running:
   - systemctl status roonrelay-mesh
   - It should show "loaded; disabled; inactive (dead)".

8. Report back:
   - Hostname, IP, kernel arch.
   - SHA256 of /opt/roonrelay-mesh/RoonBroadcastRelay.
   - The full content of /opt/roonrelay-mesh/appsettings.json.
   - The final systemctl status line.

Do NOT start or enable the service. Do not touch /opt/roonrelay or
roonrelay.service on this host (there shouldn't be one here, but verify).
```

### Prompt 2 — Site 2 (new LXC at 192.168.1.37)

Same as Prompt 1, with these substitutions:

```
- Hostname/role: Site 2 (new mesh relay, parallel to existing 192.168.1.30)
- SSH target: root@192.168.1.37
- SiteName: "Site 2"
- LocalIp: 192.168.1.37
- BroadcastAddress: 192.168.1.255
- SubnetMask: 255.255.255.0
- RemoteRelayIps: ["10.0.3.22", "10.0.2.17"]
- Config file to copy: examples-3site/appsettings.Site2.json
```

### Prompt 3 — Site 3 (new LXC at 10.0.2.17)

Same as Prompt 1, with these substitutions:

```
- Hostname/role: Site 3 (new mesh relay, parallel to existing 10.0.2.14)
- SSH target: root@10.0.2.17
- SiteName: "Site 3"
- LocalIp: 10.0.2.17
- BroadcastAddress: 10.0.2.255
- SubnetMask: 255.255.255.0
- RemoteRelayIps: ["10.0.3.22", "192.168.1.37"]
- Config file to copy: examples-3site/appsettings.Site3.json
```

At the end of Phase 4, all three LXCs have everything installed, the right
config, the systemd unit ready — and **nothing running**. Production is
unaffected.

---

## Phase 5 — Cutover test (the moment of truth)

Important: do **not** run two relays at the same site simultaneously. The
new LXC and the old LXC at one site would both try to bind UDP 9003 and
both join the multicast group, which causes duplicate forwarding and
generally weird behavior. So the test is: pause the old relays, start the
new mesh, observe, then either keep it or roll back.

On each existing relay (the 10.0.3.20 / 192.168.1.30 / 10.0.2.14 LXCs):

```bash
sudo systemctl stop roonrelay
```

On each **new** LXC (10.0.3.22 / 192.168.1.37 / 10.0.2.17):

```bash
sudo systemctl start roonrelay-mesh
journalctl -u roonrelay-mesh -f
```

A healthy log on each shows:

- One line per protocol: `Raat socket bound to 0.0.0.0:9003`, etc.
- `Raw socket created for IP spoofing` (if you see "Permission denied" here
  the LXC is not privileged — go back to Phase 1).
- **Two** `Remote relay:` lines listing the other two peers.
- Once Roon at any site discovers something, `TUNNEL ->` lines going to
  both peers and `TUNNEL <-` lines coming back from both.

Open Roon at each site. Endpoints from the other two sites should appear
in the device list within a minute.

---

## Phase 6 — Commit or roll back

### If everything works

Make it the permanent setup by enabling the new service and disabling the
old one on each pair:

On each **new** LXC:
```bash
sudo systemctl enable roonrelay-mesh
```

On each existing relay LXC:
```bash
sudo systemctl disable roonrelay
```

The original LXCs remain idle but intact. Leave them powered on for a week
or two as instant fallback. After you're confident, you can stop and delete
them whenever you like.

### If it doesn't work

The fastest path back, on each existing relay LXC:
```bash
sudo systemctl start roonrelay
```

And on each new LXC:
```bash
sudo systemctl stop roonrelay-mesh
```

You're back on the production setup in under a minute. The new LXCs stay
there for diagnosis — nothing has been changed on them since Phase 4.

---

## Reference: what's in your GitHub fork

```
RoonBroadcastRelay/                     # source the LXCs build from
├── RelayConfig.cs                      # modified: adds RemoteRelayIps list
├── RoonBroadcastRelay.cs               # modified: multi-peer tunnel send
├── Program.cs                          # modified: example uses RemoteRelayIps
├── LanInterface.cs                     # unchanged
├── InterfaceConfig.cs                  # unchanged
├── ProtocolConfig.cs                   # unchanged
├── ProtocolDefinitions.cs              # unchanged
├── ProtocolSettings.cs                 # unchanged
└── RoonBroadcastRelay.csproj           # unchanged
examples-3site/
├── appsettings.Site1.json            # site-specific config
├── appsettings.Site2.json
└── appsettings.Site3.json
dist/
├── systemd/roonrelay-mesh.service      # systemd unit, parallel install
└── INSTALL.md                          # alternate manual install guide
EXAMPLES.md                             # updated upstream docs (Example 4 added)
README.md                               # updated upstream README
LICENSE.md                              # GPL-3.0, preserved
```
