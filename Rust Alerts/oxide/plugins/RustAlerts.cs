using ConVar;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;



namespace Oxide.Plugins
{
	[Info( "RustAlerts", "Atlas Incawporated", 1.4 )]
	[Description( "A plugin for RustAlerts to allow offline Rust alerts and push notifications." )]
	public class RustAlerts : RustPlugin
	{
		[PluginReference]
		Plugin RustAlertsPlugin;

		[PluginReference]
		Plugin Clans;
		bool clansEnabled = false;

		public enum AlertType { BaseDecay, RaidAlert, CupboardDestroyed, Custom, TestAlert, AutoTurretNoAmmo, AutoTurretDestroy, TrapTriggered, PlayerKilledSleeping, ServerWipe, Service, AdminRequested, ClanBaseAttack, ClanBaseDecay, Unassigned3 }

		class RustAlertsConfig
		{
			public int alertDispatchDelaySeconds = 0;//Delay (In seconds) before a user will receive an alert once its dispatched.
			public int alertDispatchDelaySecondsPriority = 0;//Delay (In seconds) before a high priority user (Typically a VIP) will receive an alert once its dispatched.
			public bool sendUnregisteredReminders = true;//Whether or not we will send reminders to those not registered to RustAlerts.
			public int reminderRate = 7200;//How often to remind people that aren't signed up with RustAlerts to sign up.
			public bool adminsCanSendAlerts = true;//Whether or not admins can automatically send alerts.
			
		}

		//const int REMINDER_RATE = 
		const string rustAlertsPriorityPermission = "rustalerts.priority";
		const string rustAlertsAdminPermission = "rustalerts.sendalerts";
		//References to the timers, if needed.
		static Timer reminderTimer;
		static RustAlertsConfig config;
		static RustAlerts instance;

		List<BasePlayer> unregisteredPlayers = new List<BasePlayer>();


		const string setupString = "To setup RustAlerts:\n" + "- Download the RustAlerts app at rustalerts.com" + "\n" + "- After opening the app, link your Steam account by following the instructions on-screen." + "\nWhen prompted, enter the following code: *AUTH_KEY*";

		#region ChatCommands
		/// <summary>
		/// Handles all /rustalerts commands.
		/// </summary>
		/// <param name="player"></param>
		/// <param name="command"></param>
		/// <param name="args"></param>
		[ChatCommand( "rustalerts" )]
		void RustAlertRegister( BasePlayer player, string command, params string[] args ) {
			if ( args == null || args.Length == 0 ) {
				SendMessageToPlayer( player, "Unknown RustAlerts command." );
				return;
			}
			string authKey = "";
			switch ( args[0].ToUpper() ) {
				case ("SETUP"):
					authKey = RustAlertsPlugin.Call<string>( "GenerateAuthKey", player.userID );
					SendMessageToPlayer( player, setupString.Replace( "*AUTH_KEY*", TextWithColor( authKey, Color.cyan ) ) );
					break;

				case ("LINK"):
					authKey = RustAlertsPlugin.Call<string>( "GenerateAuthKey", player.userID );
					SendMessageToPlayer( player, "Here is your auth key: " + authKey + "\n" + "Type this code into your RustAlerts app to link your Steam Account!" );
					if ( unregisteredPlayers.Contains( player ) ) {
						unregisteredPlayers.Remove( player );
						//Remove the player from the unregistered list. This doesnt necessarily mean they are registered but this stops them from receiving reminders to register.
					}
					break;

				case ("CLANTEST"):
					DispatchAlert( player.userID, AlertType.ClanBaseAttack, 0 );
					DispatchAlert( player.userID, AlertType.ClanBaseDecay, 0 );
					break;

				case ("TEST"):
					if ( unregisteredPlayers.Contains( player ) ) {
						Action trueCallback = () => {
							//Debug.Log( "Got test alert command (On familiar user).. preparing to send test alert." );
							//RustAlertsPlugin.Call( "RegisterPlayer", player.userID );
							if ( unregisteredPlayers.Contains( player ) ) {
								unregisteredPlayers.Remove( player );
							}

							SendMessageToPlayer( player, "Sending a test alert now!" );
							DispatchAlert( player.userID, AlertType.TestAlert, 0 );
						};
						Action falseCallback = () => {
							//Debug.Log( "Unfamiliar player requested test:" + player.userID );
							SendMessageToPlayer( player, TextWithColor( "Could not send a test alert because you don't have RustAlerts configured!", Color.red ) + TextWithColor( "\n" + "Type /rustalerts setup for instructions on how to configure Rust Alerts.", Color.white ) );
							if ( !unregisteredPlayers.Contains( player ) ) {
								unregisteredPlayers.Add( player );
							}


						};
						RustAlertsPlugin.Call( "IsPlayerFamiliar", player.userID, trueCallback, falseCallback );
					} else {
						SendMessageToPlayer( player, "Sending a test alert now!" );
						DispatchAlert( player.userID, AlertType.TestAlert, 0 );
					}


					break;

				case ("SYNC"):
					if ( player.IsAdmin == false ) return;
					SendMessageToPlayer( player, "The database has been re-synced." );
					RustAlertsSync();
					break;

				default:
					SendMessageToPlayer( player, "Unknown RustAlerts command." );
					break;
			}


		}
		#endregion
		#region ConsoleCommands
		[ConsoleCommand( "rustalerts sync" )]
		private void RustAlertsSync() {
			Debug.Log( "Force-Syncing local database!" );
			RustAlertsPlugin.Call( "LoadPlayersFromDB", JsonConvert.SerializeObject( permission.GetPermissionUsers( rustAlertsAdminPermission ) ) );
		}
		#endregion
		#region RustHooks
		void OnPlayerInit( BasePlayer player ) {
			Action trueCallback = () => {
				SendMessageToPlayer( player, TextWithColor( "Rust Alerts v" + this.Version, Color.cyan ) + "\nRust Alerts is configured and ready to go!\nNote: You can further customize Rust Alerts via the app. Happy Rusting!" );
				if ( PlayerHasSendAlertPermission( player.userID ) ) {
					SendMessageToPlayer( player, TextWithColor( "You're currently permitted to send alerts to the server via the app.", Color.green ) );
				}
			};
			Action falseCallback = () => {
				SendMessageToPlayer( player, TextWithColor( "Rust Alerts v" + this.Version, Color.cyan ) + "\nYou don't seem to have RustAlerts configured!\n\nType " + TextWithColor( "/rustalerts setup", Color.cyan ) + " to configure RustAlerts and get notifications on your phone even when you're offline." );
				unregisteredPlayers.Add( player );

			};
			RustAlertsPlugin.Call( "IsPlayerFamiliar", player.userID, trueCallback, falseCallback );
		}

		void OnPlayerDisconnected( BasePlayer player, string reason ) {
			Action trueCallback = () => { RustAlertsPlugin.Call( "RegisterPlayer", player.userID ); };
			Action falseCallback = () => { };
			RustAlertsPlugin.Call( "IsPlayerFamiliar", player.userID, trueCallback, falseCallback );

		}

		private void OnEntityTakeDamage( BaseCombatEntity entity, HitInfo hitInfo ) {
			if ( hitInfo.damageTypes.Has( DamageType.Decay ) ) {
				//Decay damage taken, notify owner.
				BuildingPrivlidge bp = entity.GetBuildingPrivilege();
				if ( bp != null ) {
					DispatchAlert( bp.OwnerID, AlertType.BaseDecay, 0 );
				}

			}

		}

		void OnEntityDeath( BaseCombatEntity entity, HitInfo hitInfo ) {
			if ( EntityIsTC( entity ) ) {
				OnToolCupboardDestroyed( entity, hitInfo );
				return;
			}

			if ( EntityIsAutoTurret( entity ) ) {
				OnAutoTurretDestroyed( entity, hitInfo );
				return;
			}

			if ( hitInfo == null || hitInfo.Initiator == null || hitInfo.Initiator.transform == null )
				return;
			if ( !IsRaidDamage( hitInfo.damageTypes ) )
				return;

			if ( !EntityIsUpgradedBuildingBlock( entity ) && !EntityIsWallOrDoor( entity ) ) {
				return;
			} else {
				OnStructureDestroyed( entity, hitInfo.Initiator );
			}


			//StructureAttack( entity, hitInfo.Initiator, hitInfo?.WeaponPrefab?.ShortPrefabName, hitInfo.HitPositionWorld );
		}

		void OnToolCupboardDestroyed( BaseCombatEntity damagedStructure, HitInfo hitInfo ) {
			ulong ownerID = damagedStructure.OwnerID;
			ulong attacker = 0;
			if ( hitInfo != null && hitInfo.InitiatorPlayer != null ) {
				attacker = hitInfo.InitiatorPlayer.userID;
			}
			BasePlayer owner = Oxide.Game.Rust.RustCore.FindPlayerById( ownerID );
			DispatchAlert( ownerID, AlertType.CupboardDestroyed, attacker );
			//Debug.Log( "Detected TC destruction." );
		}

		void OnStructureDestroyed( BaseCombatEntity damagedStructure, BaseEntity attackerEntity ) {

			ulong ownerID = damagedStructure.OwnerID;
			if ( ownerID > 0 ) {
				//Debug.Log( "Detected structure killed by raid damage, owner not null." );
				BasePlayer owner = Oxide.Game.Rust.RustCore.FindPlayerById( ownerID );
				BasePlayer attacker = attackerEntity.ToPlayer();
				BuildingPrivlidge privs = damagedStructure.GetBuildingPrivilege();
				if ( owner != null && attacker != null && privs != null ) {
					//SendServerMessage( attacker.displayName + " destroyed a structure owned by: " + owner.displayName );
					DispatchAlert( owner.userID, AlertType.RaidAlert, attacker.userID );
				}
			}
		}

		void OnItemUse( Item item, int amount ) {
			var entity = item.parent?.entityOwner;
			if ( entity != null ) {
				if ( entity is AutoTurret ) {
					var autoTurret = entity as AutoTurret;
					if ( autoTurret != null ) {
						if ( item.amount <= 1 ) {
							OnAutoTurretAmmoDepleted( autoTurret );
						}

					}
				}
			}
		}

		void OnAutoTurretAmmoDepleted( AutoTurret turret ) {
			DispatchAlert( turret.OwnerID, AlertType.AutoTurretNoAmmo, 0 );
		}

		void OnAutoTurretDestroyed( BaseCombatEntity turret, HitInfo hitInfo ) {
			ulong attacker = 0;
			if ( hitInfo != null && hitInfo.InitiatorPlayer != null ) {
				attacker = hitInfo.InitiatorPlayer.userID;
			}
			DispatchAlert( turret.OwnerID, AlertType.AutoTurretDestroy, attacker );
		}

		void OnTrapTrigger( BaseTrap trap, GameObject go ) {

			//Debug.Log( "**OnTrapTriggerprecalled." );
			DelayedInvoke( () => { OnTrapTriggerLate( trap, go ); }, .25f );


		}

		//Called about .25 seconds after a trap has triggered. This is used to tell if the player dies from the trap or not.
		void OnTrapTriggerLate( BaseTrap trap, GameObject go ) {
			var victim = go.GetComponent<BasePlayer>();
			if ( victim != null ) {
				if ( victim.IsWounded() || victim.IsDead() || victim.IsSleeping() || victim.healthFraction == 0 ) {
					DispatchAlert( trap.OwnerID, AlertType.TrapTriggered, victim.userID );
				}
			}
		}

		void OnPlayerDie( BasePlayer victim, HitInfo hitInfo ) {
			ulong attacker = 0;
			if ( hitInfo != null && hitInfo.InitiatorPlayer != null ) {
				attacker = hitInfo.InitiatorPlayer.userID;
				//Debug.Log( "On player death (Sleeping) got attacker =" + attacker );
			}
			if ( victim.IsSleeping() ) {
				//Debug.Log( "On player death (Sleeping) called for victim =" + victim.userID );
				DispatchAlert( victim.userID, AlertType.PlayerKilledSleeping, attacker );
			}
		}
		#endregion

		#region RustAlertsMethods

		/// <summary>
		/// Lets anyone who is not registered to RustAlerts know about the plugin.
		/// </summary>
		private void SendRustAlertsReminder() {
			if ( config != null && config.sendUnregisteredReminders == false ) return;
			foreach ( var player in unregisteredPlayers ) {
				SendMessageToPlayer( player, "Register for RustAlerts to receive mobile notifications when your base comes under threat!\nTo begin, type " + TextWithColor( "/rustalerts setup", Color.cyan ) + "" );
			}
		}

		public bool PlayerHasSendAlertPermission( ulong playerID ) {
			bool result = permission.UserHasPermission( playerID + "", rustAlertsAdminPermission );
			return result;
		}

		private Action GetDispatchRequest( ulong playerID, object alertType, ulong triggeringPlayerID, string alertText = "" ) {
			Action dispatchAlert = () => {
				RustAlertsPlugin.Call( "SendAlert", playerID, alertType, triggeringPlayerID, alertText );
			};
			return dispatchAlert;
		}

		private void DispatchAlert( ulong playerID, object alertType, ulong triggeringPlayerID, string alertText = "" ) {
			
			float delay = 0;
			if ( config != null && permission.UserHasPermission( playerID + "", rustAlertsPriorityPermission ) ) {
				delay = config.alertDispatchDelaySecondsPriority;
			} else if ( config != null ) {
				delay = config.alertDispatchDelaySeconds;
			}

			Action dispatchAlert = GetDispatchRequest( playerID, alertType, triggeringPlayerID, alertText = "" );
			DelayedInvoke( dispatchAlert, delay );


			//Handle clan forwarding.
			AlertType aType = (AlertType) (alertType);
			if ( clansEnabled && Clans != null && (aType == AlertType.BaseDecay || aType == AlertType.CupboardDestroyed || aType == AlertType.RaidAlert) ) {
				string clan = Clans?.Call<string>( "GetClanTag", playerID );
				if ( clan != null ) {
					//The user is in a clan. Forward this alert to clanmates.
					JObject clanObject = Clans?.Call<JObject>( "GetClan", clan );
					if ( clanObject != null ) {
						JArray memberList = (JArray) clanObject["members"];
						foreach ( var member in memberList ) {
							ulong clanmate = Convert.ToUInt64( member );
							if ( clanmate != playerID ) {
								if ( aType == AlertType.CupboardDestroyed || aType == AlertType.RaidAlert ) {
									DispatchAlert( clanmate, AlertType.ClanBaseAttack, triggeringPlayerID );
								} else if ( aType == AlertType.BaseDecay ) {
									DispatchAlert( clanmate, AlertType.ClanBaseDecay, triggeringPlayerID );
								}

							}
						}
					}
				}
			}
		}

		public void DelayedInvoke( Action callback, float delay ) {
			RustAlertsPlugin.Call( "DelayedCallback", callback, delay );
		}
		#endregion

		#region Initialization
		void Init() {
			if ( instance == null ) {
				instance = this;
			}
			config = Config.ReadObject<RustAlertsConfig>();
			if ( config == null ) {
				Debug.LogWarning( "Could not find config file for RustAlerts!" );
				config = new RustAlertsConfig();
				Config.WriteObject( config, true );
			}
			permission.RegisterPermission( rustAlertsPriorityPermission, this );
			permission.RegisterPermission( rustAlertsAdminPermission, this );

		}


		protected override void LoadDefaultConfig() {
			Config.WriteObject( GetDefaultConfig(), true );
		}

		private RustAlertsConfig GetDefaultConfig() {
			return new RustAlertsConfig();
		}

		void OnServerInitialized() {
			if ( instance == null ) {
				instance = this;
			}
			clansEnabled = Clans != null;
			if ( clansEnabled ) {
				Debug.Log( "(RustAlerts) -- Clan plugin detected, enabling clan-related alert features." );
			} else {
				Debug.LogWarning( "(RustAlerts) -- Clans plugin not detected. Will not be able to send clan-related alerts." );
			}
			string[] permissionUsers = permission.GetPermissionUsers( rustAlertsAdminPermission );
			RustAlertsPlugin.Call( "LoadPlayersFromDB", JsonConvert.SerializeObject( permissionUsers ) );
			StartServerClock();
		}

		void StartServerClock() {

			if ( config.sendUnregisteredReminders ) {
				reminderTimer = timer.Every( config.reminderRate, () => {
					SendRustAlertsReminder();
				} );
			}

			//tcCheckTimer = timer.Every( config.toolCupboardCheckIntervalSeconds );

			RustAlertsPlugin.Call( "RegisterTimer", timer );

		}
		#endregion

		#region UtilityMethods

		public static void SendServerMessage( string message, params object[] args ) {

			instance.PrintToChat( message, args );
		}

		public static void SendMessageToPlayer( BasePlayer player, string message, params object[] args ) {
			instance.PrintToChat( player, message, args );

		}

		public string TextWithColor( string text, Color c ) {
			return "<color=#" + ColorToHex( c ) + ">" + text + "</color>";
		}

		public string ColorToHex( Color32 color ) {
			string hex = color.r.ToString( "X2" ) + color.g.ToString( "X2" ) + color.b.ToString( "X2" );
			return hex;
		}

		bool EntityIsWallOrDoor( BaseCombatEntity entity ) {
			if ( entity is Door || entity.ShortPrefabName.Contains( "door" ) || entity.ShortPrefabName.Contains( "gate" ) || entity.ShortPrefabName.Contains( "fence" ) || entity.ShortPrefabName.Contains( "wall" ) ) {
				return true;
			}

			return false;

		}

		bool EntityIsTC( BaseCombatEntity entity ) {
			if ( entity.ShortPrefabName == "cupboard.tool.deployed" || entity.ShortPrefabName == "cupboard.tool" ) {
				return true;
			}
			return false;
		}

		bool EntityIsAutoTurret( BaseCombatEntity entity ) {
			if ( entity.ShortPrefabName == "autoturret_deployed" ) {
				return true;
			}
			return false;
		}

		bool EntityIsUpgradedBuildingBlock( BaseCombatEntity entity ) {

			if ( entity is BuildingBlock ) {
				if ( ((BuildingBlock) entity).grade == BuildingGrade.Enum.Twigs ) {
					return false;
				}

				return true;
			}
			return false;
		}

		bool IsRaidDamage( DamageType type ) {
			return (type == DamageType.Blunt || type == DamageType.Bullet || type == DamageType.Stab || type == DamageType.Slash || type == DamageType.Explosion || type == DamageType.Heat);
		}

		bool IsRaidDamage( DamageTypeList dtList ) {
			for ( int index = 0; index < dtList.types.Length; ++index ) {
				if ( dtList.types[index] > 0 && IsRaidDamage( (DamageType) index ) ) {
					return true;
				}
			}

			return false;
		}

		#endregion

	}
}
