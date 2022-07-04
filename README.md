# Twitch Shark
Name the shark after your active Twitch chat.
Once the name has been chosen for the shark, the person has to say something in chat again to get back in the pool. This rewards activity in your chat.

I recommend setting up a channel point redemption to get chat to make you kill the shark for added engagement.

If you experience any issues that you can't resolve by restarting the game, please ping `Meza#0001` on the [Raft Modding Discord](https://www.raftmodding.com/discord)

Twitch Shark is fully compatible with the [Crowd Control mod](https://www.raftmodding.com/mods/crowd-control-support). Sharks spawned via Crowd Control will take on the names that come from Crowd Control.

> This mod was inspired by [Dinnerbone's Name Your Shark mod](https://github.com/Dinnerbone/name-your-shark).

Also **BIG SHOUTOUT** to [FranzFischer78](https://www.raftmodding.com/user/FranzFischer78) for bringing me up to speed with Raft mod development.

> If you need help with raft modding, go check out [The Raft Modding docs](https://api.raftmodding.com/)

## Installing

- Install the [Raft Mod Loader](https://www.raftmodding.com/download)
- Install this mod from [The Raft Mod Website](https://www.raftmodding.com/mods)
- Install the [Settings Api Mod](https://www.raftmodding.com/mods/extra-settings-api) (only needed for the host)

> Optionally you can install the [Mod Updater](https://www.raftmodding.com/mods/modupdater) to make sure you always have the latest versions

## Configuration

Once you have all of the above installed, start the game through the mod loader, make sure all the mods are loaded.

Then head into the main settings of the game, on the last tab you'll have the MOD settings.

Head to [https://twitchapps.com/tmi](https://twitchapps.com/tmi) to get yourself an oauth key for your tiwtch bot user (can be your own username too)

Use that in the settings.

## Usage

Your moderators are allowed to use the following commands **in the twitch chat**

`!noshark username` - blacklists a given username

`!allowshark username` - removes a given username from the blacklist

## MULTIPLAYER

The mod WORKS in multiplayer however the experience isn't fully optimised yet.

- Every person in the session needs to have this mod
- Only the HOST needs to configure their twitch
- All the other players can ignore the warnings the mod shows in the main menu

I will be adding a proper client experience soon.

## Troubleshooting

If something doesn't work, make sure to pay attention to the bottom right hand corner of the screen. An error message might pop up.

The main reasons the mod could fail is due to misconfiguration. Make sure to double check your settings and your token in particular.

If that fails, try restarting the game.

If that fails too, press F10 to see the console and please get in touch on [github](https://github.com/meza/TwitchShark/issues).