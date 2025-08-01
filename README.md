# Minimap

Adds a minimap to Len's Island. Actively under development, so expect the odd bug or incomplete feature.

## Preview

|Perspective|Orthographic|Orthographic + Flatten|
|-----------|------------|----------------------|
|<img width="290" height="290" alt="8084e6c4-9f92-4579-80bb-ea682cde1f29" src="https://github.com/user-attachments/assets/03eabbe3-bf25-4bbf-86a0-5047252856f2" />|<img width="311" height="311" alt="4d58830d-c7a8-4447-9b56-6f726ace690a" src="https://github.com/user-attachments/assets/4611c790-82c0-4670-bf87-78a9ee74414f" />|<img width="289" height="289" alt="3c8a1c46-766b-4923-9d92-a418f6502614" src="https://github.com/user-attachments/assets/7f5be57d-77d0-4acf-9df2-0adc3cf58404" />|

See [Rendering Style](#rendering-style) for more information about each mode.


## Configuration

Several options are provided for customising the minimap to behave however you see fit.

### Replace Compass

If you are using the default overlay, you may wish to disable the vanilla compass as the default overlay includes cardinals.

### Rotate with Player

You can toggle rotation, or have the minimap in fixed orientation. Whatever floats your boat.

### Custom Overlay

If you're artsy or simply hate the bland default, a [Krita](https://krita.org) template is provided in the [Overlays](Overlays) directory for 
you to customise the border of the minimap to your heart's content.

If you are creating a custom overlay to distribute via Thunderstore, your package should have the following structure:

```
.
├── BepInEx/
│   └── plugins/
│       └── smrkn-Minimap/
│           └── Overlays/
│               └── YourOverlay.png
├── icon.png
├── manifest.json
└── README.md
```

### Camera Height

Typically, you won't need to touch this by hand. This is just how the plugin remembers your previous zoom level.

_**Zoom is currently controlled with 9 and 0, keybind support will be available in future updates.**_

### Rendering Style

There are currently two different styles provided, aiming to cover different preferences for how a minimap should look/feel.

#### Orthographic

Orthographic more commonly resembles what many people are familiar with when they think of minimaps.
Very two dimensional with limited to no-depth.

> [!NOTE]
> The game's water shader does not work with orthographic mode out of the box.
> 
> I've had to improvise a little with making something appear in place of water.

If orthographic mode still isn't quite what you're after, there's an optional "Flatten" mode.

#### Perspective

Perspective rendering behaves just like the main game camera except top down.
This may be renamed to something like "3D Mode" for simplicity in future.

This is the default setting.

