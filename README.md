# Rust Alerts
Rust Alerts is an Oxide Extension and plugin for Rust, enabling mobile notifications for users when events happen in-game.

Once Rust Alerts is installed on a server, users will be able to link their account via the Rust Alerts app (Available on [Google Play](https://play.google.com/store/apps/details?id=com.atlas.rustalerts) and the [App Store](https://itunes.apple.com/us/app/rust-alerts/id1456273466?ls=1&mt=8). After successfully linking the account, they will be able to receive push notifications when:


- Any building block (Above twig) that they own is destroyed.
- Any wall, gate, or fence that they own is destroyed.
- A tool cupboard they own is destroyed.
- A base that they own is decaying.
- The player dies while sleeping.
- An auto turret that the player owns is destroyed.
- An auto turret that the player owns runs out of ammo.
- A trap set by the player kills another player.

Users can configure and customize which alerts they receive from within the app. 


# Installation
To install Rust Alerts to your Oxide server, take the following steps:
- Turn off your server.
- Copy both .dlls from the **RustAlerts** folder and put them in your server's **RustDedicated_Data/Managed** folder.
- Restart your server.

Note: If you previously had a RustAlerts.cs file in your oxide/plugins folder from an older version, you will need to delete it as it is no longer required.

### Recommendations:
If you plan on using this for your server, adding |RustAlerts| somewhere in your server's title or description will help app users find your server.


# Configuration:
```
  "adminsCanSendAlerts": true,
  "alertDispatchDelaySeconds": 0,
  "alertDispatchDelaySecondsPriority": 0,
  "sendUnregisteredReminders": true,
  "reminderRate": 7200
```

**adminsCanSendAlerts** (true/false) -- Whether or not all server admins have permission to send alerts to all users in the server. If set to false, all users, even admins, need the rustalerts.sendalerts permission to send alerts via the app.

**alertDispatchDelaySeconds** -- How long (by default) a user will need to wait before an alert is dispatched. If set to 0, the server will send out alerts as soon as they happen.

**alertDispatchDelaySecondsPriority** -- How long a priority user will have to wait before an alert is dispatched. Priority users are determined by users with the permission `rustalerts.priority`. This delay will bypass the default delay.

**sendUnregisteredReminders** (true/false) -- Whether or not the server will send messages to unregistered players reminding them about RustAlerts periodically.

**reminderRate** -- How often (in seconds) the server will send out reminders for people who haven't registered with the RustAlerts app. If set to anything less than 360, reminders will not be sent.

# Permissions:
**rustalerts.priority** -- Users with this permission bypass the delay set in **alertDispatchDelaySeconds** and instead use the delay set by **alertDispatchDelaySecondsPriority**. 

**rustalerts.sendalerts** -- Users with this permission will be able to send custom alerts via the app to all users in the server.


# Chat Commands
-**/rustalerts setup** -- Users can type this to learn about how to setup Rust Alerts.

-**/rustalerts link** -- Users can type this to obtain an auth key -- this is how they link their Steam account to the app.

-**/rustalerts test** -- Users can use this command to send a test alert to their mobile device.

-**/rustalerts sync** -- Updates the server's database cache. This should be used if you make changes to permissions regarding Rust Alerts, but also happens automatically every so often. (Admin only command).


### Integrations
[Clans by k1lly0u](https://umod.org/plugins/clans) (Optional) -- If this plugin is installed, clanmates that share a base will all receive alerts if that base is raided, begins decaying, or has its tool cupboard destroyed.


# Support
If you need help with Rust Alerts or encounter an issue, feel free to shoot an email to: **calebchalmers@gmail.com**.

Optionally, you can also join the associated Discord, [Atlas Incawporated](https://discordapp.com/invite/battlesquares), to give feedback or resolve issues.


# License
You are free to download, use, and modify Rust Alerts for your own use. Reuploading or distributing copies of the plugin require express consent.


