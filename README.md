# dogpile-mods

[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://raw.githubusercontent.com/Nauja/raft-mods/master/LICENSE)

My own mods for the [Dogpile](https://store.steampowered.com/app/3839300/Dogpile/) game.

<img src="https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/3839300/b3e3e3a2c712e63b8d40958c9829053dec0f9b59/header.jpg?t=1765918502" width="500px"/>

## Installation

You can find pre-built versions of my mods at https://www.nexusmods.com/ (see links below).

Each mod requires that you first install BepInEx version 5.4+ next to the game executable.

First, find a release for BepInEx version 5.4+ here https://github.com/BepInEx/BepInEx/releases.

Then, download for example the archive `BepInEx_win_x64_5.4.23.4.zip`.

Put all the content of the archive next to the game executable, for example in `steamapps\common\Dogpile`.

Start the game.

If it works, you should see logs in `steamapps\common\Dogpile\BepInEx\LogOutput.log`.

You can close the game and install my mods in `steamapps\common\Dogpile\BepInEx\plugins`.

## Build

Clone the repo with:

```
git clone https://github.com/Nauja/raft-mods.git
```

Copy the folder `steamapps\common\Dogpile\dogpile_Data\Managed` to `dogpile-mods\` as it contains the game assemblies referenced by the mods.

Then you can open the Visual Studio solution `dogpile-mods\DogpileMods.slnx` and build the desired mod.

Once built, simply copy the produced dll to `steamapps\common\Dogpile\BepInEx\plugins`.

For example, copy `dogpile-mods\ShowDebugMenu\bin\Release\net45\ShowDebugMenu.dll` to `steamapps\common\Dogpile\BepInEx\plugins`, then start the game.

## ShowDebugMenu

![Dogpile](https://img.shields.io/badge/Dogpile-1.04+-blue)

Allow to toggle the debug menu used by developers with F7 ingame.

Mod at: wip
