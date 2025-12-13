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
- Install the [Settings Api Mod](https://www.raftmodding.com/mods/extra-settings-api)

> Optionally you can install the [Mod Updater](https://www.raftmodding.com/mods/modupdater) to make sure you always have the latest versions

## Configuration

Once you have all of the above installed, start the game through the mod loader, make sure all the mods are loaded.

Then head into the main settings of the game, on the last tab you'll have the MOD settings.

### Authorize the bot account

1. Click **Authorize Twitch (opens browser)** first. Your default browser opens Twitch’s OAuth consent page so you can use password managers and view the URL directly.
2. Approve the request. When the browser shows “Authorization complete”, return to Raft; the mod automatically saves the credentials and fills in the Twitch username for you—no manual copy/paste required.
   - If Windows Defender blocks the browser callback, allow inbound connections to `http://localhost:37081/twitch-shark/oauth` (for example via `netsh advfirewall firewall add rule name="TwitchShark OAuth" dir=in action=allow protocol=TCP localport=37081`).
   - Access tokens eventually expire (this is a Twitch restriction), so repeat the authorization when the mod tells you to reconnect.
3. After the browser finishes, the settings tab hides the authorize button and replaces it with your Twitch username plus a **Disconnect Twitch** button. Use Disconnect whenever you want to clear the saved credentials or swap accounts.
4. Set **Twitch Channel** to the chat you want to track and adjust the other options as needed.

> The legacy token from [twitchapps.com/tmi](https://twitchapps.com/tmi) is no longer sufficient for authentication.

## Usage

Your moderators are allowed to use the following commands **in the twitch chat**

`!noshark username` - blacklists a given username

`!allowshark username` - removes a given username from the blacklist

## MULTIPLAYER

The mod WORKS in multiplayer.

- Every person in the session needs to have this mod
- Only the HOST **needs** to configure their twitch
- The other players can connect to their own tiwtch too, the chatters will be combined in the shark name pool.
- The blacklisting is local to the clients however the host's blacklist is global. Meaning that if a name is on the host's blacklist, 
they will never be added. But if the name is on a client's blacklist, they can still be added from another participant's chat.
- The entries can only be cleared from the host and it will clear it out for everyone.
- It doesn't matter where the viewer chats from, their last chat time will be updated regardless when dealing with the timeouts
- The timeout setting is controlled by the host

## Troubleshooting

Sometimes the "Connecting to twitch" notification stays up for 30 seconds. That probably means that the connection failed and twitch's api didn't respond properly.
When that happens, reconnect from the settings!

If something doesn't work, make sure to pay attention to the bottom right hand corner of the screen. An error message might pop up.

The main reasons the mod could fail is due to misconfiguration. Make sure to double check your settings and your token in particular.

If that fails, try restarting the game.

If that fails too, press F10 to see the console and please get in touch on [github](https://github.com/meza/TwitchShark/issues).
