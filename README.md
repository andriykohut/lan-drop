# LAN Drop

Sends a web link or a file from one PC to every other PC on the local network, where it opens
by itself. Runs on Windows 7 and later with nothing to install: it uses .NET Framework 3.5,
which is part of Windows 7, or 4.x on newer Windows.

## Build

```
dotnet build -c Release
dotnet test -c Release
```

The app is `LanDrop.exe` and `LanDrop.exe.config` in `src/LanDrop/bin/Release/net35/`.

## Setting up

1. Copy `LanDrop.exe` and `LanDrop.exe.config` into a folder on each PC. Not a USB stick: the
   PC starts the app from that folder at every sign-in, and saves its settings beside it.
2. On the PC that sends: run it, type a code, choose "Цей ПК надсилає". When Windows asks about
   the firewall, allow it (this needs an administrator), or the other PCs cannot connect.
3. On every other PC: run it, type the same code, choose "Цей ПК отримує від ПК:" and type the
   sending PC's name (Control Panel, System, Computer name). From then on it runs in the
   background and starts with Windows for that user.

Links open in the default browser. Files are saved to a `LAN Drop` folder on the desktop and
opened with the program Windows uses for them.

To change the settings, delete `lan-drop.ini` beside the exe and run it again. To stop it
starting with Windows, remove "LAN Drop" from startup: msconfig, Startup on Windows 7; Task
Manager, Startup on Windows 8 and later.

## Limits

- The code stops casual misuse only. Anyone who can read `lan-drop.ini` on a receiving PC can
  send to every PC.
- Nothing is encrypted on the network.
- Starting with Windows is per Windows user: on a PC with several accounts, run it once under
  each.
- It uses TCP port 17850.
