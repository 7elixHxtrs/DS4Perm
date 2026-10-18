# DS4Perm

Convierte tu mando de **PS4 (DualShock 4)** en un mando de **Xbox 360** para Windows, de forma permanente y con un monitor en vivo.

El programa se queda en la bandeja del sistema, detecta el mando cada vez que lo conectas (USB o Bluetooth) y crea un mando virtual de Xbox 360 sin que tengas que abrir nada ni reinstalar drivers.

![Captura](docs/screenshot.png)

## Características

- Mando virtual Xbox 360 (vía ViGEmBus) compatible con casi cualquier juego.
- Conexión por **USB y Bluetooth**; se reconecta solo.
- Gatillos L2/R2 con resolución completa (0–255) y sticks de 16 bits.
- Vibración del juego enviada al mando (motores grande y pequeño) y botón de prueba.
- Monitor visual del mando: botones, D-pad, touchpad, sticks, gatillos con relleno progresivo y batería.
- Modo claro y oscuro.
- Opción "Iniciar con Windows (minimizado)".

## Mapeo de botones

| DualShock 4 | Xbox 360 |
|---|---|
| ✕ / ○ / □ / △ | A / B / X / Y |
| L1 / R1 | LB / RB |
| L2 / R2 | LT / RT (analógicos) |
| L3 / R3 | Clic de stick izq. / der. |
| Share / Touchpad (clic) | Back |
| Options | Start |
| PS | Guide |
| D-pad, sticks | D-pad, sticks |

## Requisitos

- Windows 10 u 11 (64 bits).
- [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases). Se instala una sola vez y queda permanente. El proyecto fue archivado por su autor en 2023, pero sigue funcionando.
- Para compilar: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Uso

1. Instala ViGEmBus y reinicia.
2. Descarga `DS4Perm.exe` desde la sección **Releases** y ábrelo.
3. Marca "Iniciar con Windows (minimizado)" para que arranque solo.
4. Conecta el mando por USB, o emparéjalo por Bluetooth (mantén **PS + Share** hasta que parpadee) y luego usa solo el botón PS.

Windows SmartScreen puede avisar que el programa no está firmado. Es normal en programas sin certificado; puedes compilarlo tú mismo para verificar el código.

## Compilar

```
build.bat
```

Genera `out\DS4Perm.exe` (un solo archivo, sin instalar nada más). También se compila automáticamente con GitHub Actions en cada push.

## Solución de problemas

**"Windows no ve ningún dispositivo HID de Sony" (por USB).** Casi siempre es un driver `libusbK` que dejó ScpToolkit o DS4Windows y que Windows reinstala cada vez que enchufas el mando. Con PowerShell como administrador:

```
Get-WindowsDriver -Online -All | Where-Object { $_.ProviderName -match 'libusbK|Scarlet' -or $_.OriginalFileName -match 'scpvbus' } | Format-Table Driver,ProviderName,ClassName
```

Revisa la lista y, si son restos de SCP, bórralos:

```
Get-WindowsDriver -Online -All | Where-Object { $_.ProviderName -match 'libusbK|Scarlet' -or $_.OriginalFileName -match 'scpvbus' } | ForEach-Object { pnputil /delete-driver $_.Driver /uninstall /force }
```

Después desenchufa y vuelve a enchufar el mando: debe aparecer como "Controlador de juego compatible con HID".

**Un juego ve el mando dos veces.** Ve el DS4 original y el virtual. Desactiva el soporte de mandos PlayStation en Steam o instala [HidHide](https://github.com/nefarius/HidHide) para ocultar el original.

**Otro programa tiene el mando abierto.** Cierra Steam, DS4Windows o ScpToolkit mientras usas DS4Perm.

## Limitaciones

- Solo DualShock 4 (V1, V2 y adaptador inalámbrico de Sony). No incluye DualSense.
- No tiene giroscopio, touchpad como ratón ni barra de luz configurable.

## Cómo funciona

- `HidSharp` lee los reportes HID del mando (USB: reporte `0x01`; Bluetooth: reporte `0x11`).
- `Nefarius.ViGEm.Client` crea el mando virtual de Xbox 360 y recibe la vibración del juego.
- La vibración se devuelve al mando con el reporte de salida `0x05` (USB) o `0x11` con CRC32 (Bluetooth).
- Los rellenos de la interfaz se generan a partir de las regiones cerradas del dibujo del mando (`masks/`).

## Licencia

[MIT](LICENSE). Usa HidSharp (Apache 2.0) y Nefarius.ViGEm.Client (MIT).
