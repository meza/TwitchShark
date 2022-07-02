# Twitch Shark

> WARNING: the mod hasn't been tested for Multiplayer yet. It _should_ work but if anything comes up, please open an issue on [github](https://github.com/meza/TwitchShark/issues).

Name the shark after your active Twitch chat.

Twitch Shark is fully compatible with the [Crowd Control mod](https://www.raftmodding.com/mods/crowd-control-support). Sharks spawned via Crowd Control will take on the names that come from Crowd Control.

> This mod was inspired by [Dinnerbone's Name Your Shark mod](https://github.com/Dinnerbone/name-your-shark).

Also **BIG SHOUTOUT** to [FranzFischer78](https://www.raftmodding.com/user/FranzFischer78) for bringing me up to speed with Raft mod development.

> If you need help with raft modding, go check out [The Raft Modding docs](https://api.raftmodding.com/)

## Installing

- Install the [Raft Mod Loader](https://www.raftmodding.com/download)
- Install the mod from [The Raft Mod Website](https://www.raftmodding.com/mods)
- Install the [Settings Api Mod](https://www.raftmodding.com/mods/extra-settings-api)

> Optionally you can install the [Mod Updater](https://www.raftmodding.com/mods/modupdater) to make sure you always have the latest versions

## Configuration

Once you have all of the above installed, start the game through the mod loader, make sure all the mods are loaded.

Then head into the main settings of the game, on the last tab you'll have the MOD settings.

Head to [https://twitchapps.com/tmi](https://twitchapps.com/tmi) to get yourself an oauth key for your tiwtch bot user (can be your own username too)

Use that in the settings.

## Usage

Your moderators are allowed to use the following commands **in the twitch chat**

`**!noshark** username` - blacklists a given username

`**!allowshark** username` - removes a given username from the blacklist

## Troubleshooting

If something doesn't work, make sure to pay attention to the bottom right hand corner of the screen. An error message might pop up.

The main reasons the mod could fail is due to misconfiguration. Make sure to double check your settings and your token in particular.

If that fails, try restarting the game.

If that fails too, please let me know on [github](https://github.com/meza/TwitchShark/issues).