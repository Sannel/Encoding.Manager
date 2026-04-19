# File Share Configuration

Encoding Manager accesses disc folders (Blu-ray, DVD) through the paths configured in `Filesystem:Roots`.
When those paths are served over a network file share (SMB/Samba), a specific Samba setting is required to
ensure HandBrakeCLI assigns consistent title numbers across all platforms.

## Why Title Numbers Must Match

HandBrakeCLI scans a Blu-ray disc by enumerating the `.MPLS` playlist files inside `BDMV/PLAYLIST/`.
It assigns each title a sequential `Index` (1, 2, 3, …) based on the **order the operating system returns
those files**.

- **Linux** clients reading a Samba share receive directory entries in the order Samba sends them, which
  reflects the underlying filesystem order (e.g. XFS hash order).
- **Windows** clients re-sort SMB directory entries **alphabetically** before presenting them to
  applications.

This means HandBrakeCLI on Windows and HandBrakeCLI on Linux enumerate `00002.MPLS`, `00022.MPLS`, etc.
in different orders and assign them different `Index` values. The same physical title ends up as "Title 2"
on one platform and "Title 6" on another.

Since Encoding Manager stores the title number when you add a disc to the queue (scanned on the web
server) and then re-uses that number when encoding (on the Runner), a mismatch causes the wrong title to
be encoded.

## Required Samba Setting: `dirsort`

> **This section applies only when the file server is running Samba on Linux.**
> If your disc folders are shared from a **Windows** file server (Windows Server or Windows 10/11),
> no additional configuration is needed — Windows SMB always returns directory entries in alphabetical
> order to all clients, so title numbers are already consistent across platforms.

Add the `dirsort` VFS module to every share that contains disc folders. This makes Samba sort directory
entries alphabetically **before** sending them to any client, so both Linux and Windows receive entries in
the same order and HandBrakeCLI assigns the same `Index` values on all platforms.

```ini
[disks]
   path = /srv/disks
   vfs objects = dirsort
   # ... rest of your share options
```

After editing `smb.conf`, reload Samba:

```bash
sudo systemctl reload smbd
```

> **Note:** The `dirsort` module is included in all standard Samba packages — no additional installation
> is required.

## Clearing the Scan Cache

After applying `dirsort`, any discs already scanned and cached by the web server will have the old
(pre-`dirsort`) title numbers stored. Use the **Rescan Disc** button on the scan page to force a fresh
scan and update the cache with the correct, consistent title numbers.
