# Wii Controller

.NET MAUI app that turns your Android phone into a Wii Remote for the Dolphin emulator.

The app reads your phone's accelerometer and gyroscope, exposes on-screen Wiimote buttons, and streams everything over UDP using the [DSU/cemuhook protocol](https://v1993.github.io/cemuhook-protocol/) — so Dolphin sees it as a real motion controller.

## How it works

```
┌──────────────┐       UDP 26760       ┌──────────────┐
│   📱 Phone   │ ────────────────────▸ │  🐬 Dolphin  │
│  (MAUI App)  │  DSU protocol binary  │   (on PC)    │
│              │ ◂──────────────────── │              │
│ accel + gyro │   data requests       │ Wiimote slot │
│ + buttons    │                       │              │
└──────────────┘                       └──────────────┘
```

1. The app runs on your phone and listens on UDP port **26760**
2. In Dolphin, go to **Controllers → Alternate Input Sources** and point to your phone's IP
3. Dolphin requests data via the DSU protocol; the app responds with sensor + button state at ~60 Hz

## 📥 Download

Download the latest pre-compiled APK from the [Releases](https://github.com/blasestevez/dsucontroller/releases/latest) page and install it on your Android device.

## Features

- **Full motion controls** — accelerometer and gyroscope mapped to DSU spec units (g's, °/s)
- **On-screen Wiimote buttons** — D-pad, A, B trigger, 1, 2, +, -, Home, and recenter
- **Multi-client UDP support** — handles multiple emulators or concurrent data/discovery subscriptions without dropping packets
- **Haptic feedback** on button press
- **Keep screen on** while the controller server is active

## Requirements

- Android phone with accelerometer and gyroscope
- [.NET 10 SDK](https://dotnet.microsoft.com/download) with MAUI workload
- [Dolphin Emulator](https://dolphin-emu.org/) on PC (same Wi-Fi network)

## Build & Run

```bash
# Clone the repo
git clone https://github.com/blasestevez/dsucontroller.git
cd dsucontroller/wiimotedsu

# Restore and build release APK
dotnet publish wiimotedsu.app -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

The signed APK will be at `wiimotedsu.app/bin/Release/net10.0-android/publish/com.blasestevez.wiicontroller-Signed.apk`.

Or open `wiimotedsu.slnx` in Visual Studio 2022+ and deploy to your Android device.

## Dolphin Setup

1. Open Dolphin → **Controllers**
2. Under **Alternate Input Sources**, check **DSU Client**
3. Click **Configure** and add your phone's IP with port `26760`
4. Map the Wiimote controller to the DSU device

## Credits

Inspired by [WiiMoteDSU](https://github.com/marcowindt/WiiMoteDSU) (Flutter/Dart) by Marco Windt.

## License

MIT
