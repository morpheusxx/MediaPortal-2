# MP2-Server-Host

Platform independent host of the MP2 server (`net10.0`, .NET generic host). It runs the same server life cycle as `MP2-Server.exe`
(`MediaPortal.Server.Hosting.ServerRuntime`) without WinForms and without the Windows service wrapper.

```
MP2-Server-Host --data <directory>
```

* **Console:** runs until Ctrl+C / SIGTERM; the server is stopped cleanly.
* **systemd:** `Type=notify`, see `mp2-server.service`.
* **Windows service:** `sc create "MP2 Server Host" binPath= "...\MP2-Server-Host.exe --data ..."`.
* **Docker:** see `Dockerfile` (data volume `/data`).
* **IPC shutdown:** other tools can stop the host through the `ServerHost` pipe (`IpcClient`), like the service monitor does for `MP2-Server.exe`.

Default data directory when `--data` is not given: `%ProgramData%\Team MediaPortal\MP2-Server` on Windows, `~/.local/share/Team MediaPortal/MP2-Server`
elsewhere. Services and containers should always pass `--data`.

## Status / known limitations

* Verified on Windows only (start, database creation, plugin loading, `running` state, graceful shutdown via IPC). **Not run on Linux yet.**
* Windows-only plugins (`BassAudioMetadataExtractor`, `AudioCDResourceProvider`, GDI/WIC thumbnails, `NetworkNeighborhoodResourceProvider`, `MP2Extended`,
  TV integration) declare `Platforms="Windows"` in their `plugin.xml`; the plugin manager does not enable them on other platforms (one info line each in the log, plugins that depend on
  them get a warning). On Windows this host is not meant to run them (no WinForms): use `MP2-Server.exe` there.
* The database is SQLite through Microsoft.Data.Sqlite (SQLitePCLRaw, native builds for Windows/Linux/macOS are shipped in the plugin folder). It keeps the schema,
  the PRAGMAs and the 16 byte GUID storage of the former System.Data.SQLite based version, but opening a database file created by that version is **not verified yet**.
  The culture sensitive NOCASE collation differs between Windows (native) and other platforms (managed), so do not copy database files between operating systems.
* Local file system access: provider paths are `//home/user/Music` style on Linux (`/C:/Music` on Windows); the virtual root lists the single root `//`. Provider code is adapted but untested on Linux.
* Native libraries for MediaInfo, FreeImage, OpenCV, FFmpeg are not packaged for Linux yet.
