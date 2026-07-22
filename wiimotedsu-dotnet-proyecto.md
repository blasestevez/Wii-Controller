# WiimoteDSU en .NET MAUI

## Objetivo

App .NET MAUI que replica WiimoteDSU (original: marcowindt, Flutter/Dart). Usa acelerómetro/giroscopio del celular como control virtual, expuesto por protocolo DSU (cemuhook) para que Dolphin lo detecte como Wiimote emulado.

Flujo:
1. App corre en el celular, muestra IP local, escucha UDP 26760.
2. Dolphin (PC) apunta a esa IP:26760 (Alternate Input Sources).
3. Dolphin pide datos por UDP; la app responde con botones on-screen + accel/gyro, formato binario DSU.

## Protocolo DSU/cemuhook

UDP, puerto 26760. Header de 20 bytes en todo paquete:

| Offset | Bytes | Campo |
|---|---|---|
| 0 | 4 | Magic: "DSUS" (server) / "DSUC" (client) |
| 4 | 2 | Versión protocolo (1001) |
| 6 | 2 | Longitud payload |
| 8 | 4 | CRC32 (campo en 0 al calcular, luego se pisa) |
| 12 | 4 | ID server/cliente |
| 16 | 4 | Tipo de mensaje |

Little-endian.

Tipos de mensaje:
- 0x100000: versión soportada
- 0x100001: info de slots (hasta 4): conectado, modelo, tipo conexión, MAC, batería
- 0x100002: pedido de datos → registrar cliente, mandar reporte (100 bytes: botones, sticks, accel, gyro) en loop hasta timeout

Spec completa: https://v1993.github.io/cemuhook-protocol/

## Arquitectura (4 piezas independientes)

1. Transporte UDP: socket en 26760, distingue los 3 tipos de mensaje
2. Protocolo DSU: serialización binaria (header + payload), testeable sin MAUI ni sensores
3. Fuente de datos: Accelerometer/Gyroscope (MAUI Essentials) + botones on-screen → estado normalizado
4. Loop de reporte: timer de alta frecuencia, manda estado a clientes registrados, maneja timeout

## Cómo quiero trabajar

- No implementar el proyecto completo. Rol: profesor/pair programmer, no implementador.
- Antes de código nuevo: explicar el concepto (CRC32, bind/connect UDP, ciclo de vida de sensores en MAUI, etc), confirmar que lo entendí.
- Prioridad: yo escribo el código, guiado con pistas y revisión. Generar código completo solo si lo pido explícitamente, y solo para boilerplate mecánico.
- Código generado: explicar bloques no triviales, señalar qué puede fallar.
- Cada feature termina en un checkpoint verificable (unit test, herramienta externa, log) antes de avanzar de fase.
- Si detectás que pido resolver algo que no entiendo conceptualmente: explicar primero, no implementar.
- Uso válido del agente para tareas acotadas/mecánicas (esqueleto de UdpClient async, casos de test, debug de CRC32). Diseño y "por qué" los entiendo yo.

## Roadmap

**Fase 0 — Setup**
`dotnet new maui`, correr en Android. Entender ciclo de vida MAUI (background service, UI thread vs workers).

**Fase 1 — UDP puro**
Console app separado: UdpClient, escucha, echo. Conceptos: sockets, async/await en red, IPEndPoint, bind vs connect. Checkpoint: probar con `nc -u` o script Python.

**Fase 2 — Protocolo DSU binario**
Clase que arma header (20 bytes) + los 3 tipos de respuesta (versión / info puertos 11 bytes / datos 100 bytes). Control fantasma con valores fijos, sin sensores. Conceptos: endianness, Span<byte>/BinaryPrimitives, CRC32 en .NET. Checkpoint: unit tests byte a byte contra la spec.

**Fase 3 — Server real, datos fake**
Unir Fase 1+2: parsear type (offset 16-20), responder, registrar clientes. Checkpoint: DSU Pad Test (Windows) o Dolphin directo, confirmar detección del control fantasma.

**Fase 4 — MAUI + sensores reales**
Migrar server a la app MAUI. Accelerometer/Gyroscope mapeados a unidades de la spec (g's, deg/s). UI: IP local (NetworkInterface.GetAllNetworkInterfaces, filtrar WiFi), botones on-screen → bitmask. Investigar permisos background (Android: foreground service; iOS: restrictivo, probablemente requiere foreground). Checkpoint: Dolphin detecta control real, movimiento reflejado.

**Fase 5 — Pulido**
Multi-slot, reconexión, timeout, calibración, persistencia de config.

## Referencias

- Spec: https://v1993.github.io/cemuhook-protocol/
- Original (comparar después de escribir la propia lógica, no antes): https://github.com/marcowindt/WiiMoteDSU
- Wireshark: comparar tráfico real Dolphin↔WiimoteDSU contra output propio
- Docs: Microsoft.Maui.Devices.Sensors

## Estado actual

Por arrancar Fase 1. Sin código escrito.
