# Rust Alerts
Rust Alerts is an Oxide Extension for Rust, enabling mobile notifications for users through an opt-in process.

Once Rust Alerts is installed on a server, users will be able to link their account via the Rust Alerts app on the [Google Play Store](https://play.google.com/store/apps/details?id=com.atlas.rustalerts). After successfully linking the account, they will be able to receive push notifications when:


- Any building block (Above twig) that they own is destroyed.
- Any wall, gate, or fence that they own is destroyed
- A tool cupboard they own is destroyed
- A base that they own is decaying.
- The player dies while sleeping
- An auto turret that the player owns is destroyed
- An auto turret that the player owns runs out of ammo
- A trap set by the player kills another player

Users can configure these alerts from within the app. 


# Installation
To install Rust Alerts to your Oxide server, take the following steps:
- Turn off your server.
- Copy all .dlls from Managed and put them in your server's RustDedicated_Data/Managed folder.
- Copy the RustAlerts.cs file from the Oxide folder and put it in your server's oxide/plugins folder.
- Restart your server.


### Updating Rust Alerts:
If you have a previous version of Rust Alerts installed, you will only need to copy the RustAlerts.cs file into oxide/plugins, unless otherwise noted.

### Recommendations:
If you plan on using this for your server, adding |RustAlerts| somewhere in your server's title or description will help app users find your server.


