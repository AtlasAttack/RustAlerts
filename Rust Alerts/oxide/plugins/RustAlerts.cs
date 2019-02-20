using ConVar;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;



namespace Oxide.Plugins
{
	[Info( "RustAlerts", "Atlas Incawporated", 1.1 )]
	[Description( "A plugin for RustAlerts to allow offline Rust alerts and push notifications." )]
	public class RustAlerts : RustPlugin
	{
		public enum AlertType { BaseDecay, RaidAlert, CupboardDestroyed, Custom, TestAlert, AutoTurretNoAmmo, AutoTurretDestroy, TrapTriggered, PlayerKilledSleeping, ServerWipe, Service, AdminRequested, Unassigned1, Unassigned2, Unassigned3 }


		[PluginReference]
		Plugin RustAlertsPlugin;
		const int SERVER_REPORTRATE = 5;//How often the plugin reports back to the server (In Seconds). Lower report rates means more responsive alerts, but potentially reduced performance.
		const int SERVER_SYNCRATE = 1800;//How often the plugin syncs its cache to the server (In Seconds). This is a slow operation, but doesnt need to be done very often. Default 30 mins / 1800 seconds.

		const int REMINDER_RATE = 7200;//How often to remind people that aren't signed up with RustAlerts to sign up.

		//References to the timers, if needed.
		static Timer serverTimer;
		static Timer serverDBSyncTimer;
		static Timer reminderTimer;

		static RustAlerts instance;

		List<BasePlayer> unregisteredPlayers = new List<BasePlayer>();

		public static void SendServerMessage( string message, params object[] args ) {

			instance.PrintToChat( message, args );
		}

		public static void SendMessageToPlayer( BasePlayer player, string message, params object[] args ) {
			instance.PrintToChat( player, message, args );
			
		}
		const string setupString = "To setup RustAlerts:\n" + "- Download the RustAlerts app at rustalerts.com" + "\n" + "- After opening the app, link your Steam account by following the instructions on-screen." + "\nWhen prompted, enter the following code: *AUTH_KEY*";
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

				case ("TEST"):
					if ( unregisteredPlayers.Contains( player ) ) {
						Action trueCallback = () => {
							Debug.Log( "Got test alert command (On familiar user).. preparing to send test alert." );
							//RustAlertsPlugin.Call( "RegisterPlayer", player.userID );
							if ( unregisteredPlayers.Contains( player ) ) {
								unregisteredPlayers.Remove( player );
							}

							SendMessageToPlayer( player, "Sending a test alert now!" );
							RustAlertsPlugin.Call( "SendAlert", player.userID, AlertType.TestAlert );
						};
						Action falseCallback = () => {
							Debug.Log( "Unfamiliar player requested test:" + player.userID );
							SendMessageToPlayer( player, TextWithColor( "Could not send a test alert because you don't have RustAlerts configured!", Color.red ) + TextWithColor( "\n" + "Type /rustalerts setup for instructions on how to configure Rust Alerts.", Color.white ) );
							if ( !unregisteredPlayers.Contains( player ) ) {
								unregisteredPlayers.Add( player );
							}


						};
						RustAlertsPlugin.Call( "IsPlayerFamiliar", player.userID, trueCallback, falseCallback );
					} else {
						SendMessageToPlayer( player, "Sending a test alert now!" );
						RustAlertsPlugin.Call( "SendAlert", player.userID, AlertType.TestAlert );
					}


					break;

				case ("SYNC"):
					if ( player.IsAdmin == false ) return;
					SendMessageToPlayer( player, "Updating the Server's cache.. this could take a bit." );
					RustAlertsPlugin.Call( "LoadPlayersFromDB" );
					break;

				default:
					SendMessageToPlayer( player, "Unknown RustAlerts command." );
					break;
			}


		}

		[ChatCommand( "steamid" )]
		void PrintSteamID( BasePlayer player ) {

			SendMessageToPlayer( player, "Your Steam ID=" + player.userID );
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

		float GetHealthPercent( BaseEntity entity, float damage = 0f ) {
			return (entity.Health() - damage) * 100f / entity.MaxHealth();
		}
		#region RustHooks
		void OnEntityDeath( BaseCombatEntity entity, HitInfo hitInfo ) {
			Debug.Log( "Detected death of entity: " + entity.ShortPrefabName );
			if ( EntityIsTC( entity ) ) {
				OnToolCupboardDestroyed( entity );
				return;
			}

			if ( EntityIsAutoTurret( entity ) ) {
				OnAutoTurretDestroyed( entity );
				return;
			}

			if ( hitInfo == null || hitInfo.Initiator == null || hitInfo.Initiator.transform == null )
				return;
			if ( !IsRaidDamage( hitInfo.damageTypes ) )
				return;

			/*if ( GetHealthPercent( entity, hitInfo.damageTypes.Total() ) > DAMAGE_THRESHOLD ) {
				return;
			}*/



			if ( !EntityIsUpgradedBuildingBlock( entity ) && !EntityIsWallOrDoor( entity ) ) {
				return;
			} else {
				OnStructureDestroyed( entity, hitInfo.Initiator );
			}


			//StructureAttack( entity, hitInfo.Initiator, hitInfo?.WeaponPrefab?.ShortPrefabName, hitInfo.HitPositionWorld );
		}

		void OnToolCupboardDestroyed( BaseCombatEntity damagedStructure ) {
			ulong ownerID = damagedStructure.OwnerID;
			BasePlayer owner = Oxide.Game.Rust.RustCore.FindPlayerById( ownerID );
			RustAlertsPlugin.Call( "SendAlert", owner.userID, AlertType.CupboardDestroyed );
			Debug.Log( "Detected TC destruction." );
		}

		void OnStructureDestroyed( BaseCombatEntity damagedStructure, BaseEntity attackerEntity ) {

			ulong ownerID = damagedStructure.OwnerID;
			if ( ownerID > 0 ) {
				//Debug.Log( "Detected structure killed by raid damage, owner not null." );
				BasePlayer owner = Oxide.Game.Rust.RustCore.FindPlayerById( ownerID );
				BasePlayer attacker = attackerEntity.ToPlayer();
				if ( owner != null && attacker != null ) {
					//SendServerMessage( attacker.displayName + " destroyed a structure owned by: " + owner.displayName );
					RustAlertsPlugin.Call( "SendAlert", owner.userID, AlertType.RaidAlert );
				} else if ( attacker == null ) {
					//Debug.Log( "Couldn't get attacker info from structure destroyed." );
				}
			}
		}

		void OnItemUse( Item item, int amount ) {
			var entity = item.parent?.entityOwner;
			if ( entity != null ) {
				if ( entity is AutoTurret ) {
					var autoTurret = entity as AutoTurret;
					if ( autoTurret != null ) {
						if ( item.amount <= 0 ) {
							OnAutoTurretAmmoDepleted( autoTurret );
						}

					}
				}
			}
		}

		void OnAutoTurretAmmoDepleted( AutoTurret turret ) {
			RustAlertsPlugin.Call( "SendAlert", turret.OwnerID, AlertType.AutoTurretNoAmmo );
		}

		void OnAutoTurretDestroyed( BaseCombatEntity turret ) {
			RustAlertsPlugin.Call( "SendAlert", turret.OwnerID, AlertType.AutoTurretDestroy );
		}

		void OnTrapTrigger( BaseTrap trap, GameObject go ) {
			BasePlayer victim = go.GetComponent<BasePlayer>();
			Debug.Log( "OnTrapTrigger called with non-null victim." );
			if ( victim != null ) {
				//Someone triggered our trap.
				if ( victim.IsWounded() || victim.IsDead() ) {
					RustAlertsPlugin.Call( "SendAlert", trap.OwnerID, AlertType.TrapTriggered );
				}
			}

		}

		void OnPlayerDie( BasePlayer victim, HitInfo info ) {
			if ( victim.IsSleeping() ) {
				RustAlertsPlugin.Call( "SendAlert", victim.userID, AlertType.PlayerKilledSleeping );
			}
		}
		#endregion

		void StartServerClock() {
			serverTimer = timer.Every( SERVER_REPORTRATE, () => {
				RustAlertsPlugin.Call( "ReportServerTime", DateTimeOffset.Now.ToUnixTimeSeconds() );
			} );

			serverDBSyncTimer = timer.Every( SERVER_SYNCRATE, () => {
				RustAlertsPlugin.Call( "LoadPlayersFromDB" );
			} );

			reminderTimer = timer.Every( REMINDER_RATE, () => {
				SendRustAlertsReminder();
			} );
		}

		/// <summary>
		/// Lets anyone who is not registered to RustAlerts know about the plugin.
		/// </summary>
		private void SendRustAlertsReminder() {
			foreach ( var player in unregisteredPlayers ) {
				SendMessageToPlayer( player, "Register for RustAlerts to receive mobile notifications when your base comes under threat!\nTo begin, type " + TextWithColor( "/rustalerts setup", Color.cyan ) + "" );
			}
		}

		void OnPlayerInit( BasePlayer player ) {
			//ServerHandler.RegisterPlayer( player.userID ); 
			Action trueCallback = () => {
				RustAlertsPlugin.Call( "RegisterPlayer", player.userID );
				SendMessageToPlayer( player, TextWithColor( "Rust Alerts v" + this.Version, Color.cyan ) + "\nRust Alerts is configured and ready to go!\nNote: You can further customize Rust Alerts via the app. Happy Rusting!" );
			};
			Action falseCallback = () => { SendMessageToPlayer( player, TextWithColor( "Rust Alerts v" + this.Version, Color.cyan ) + "\nYou don't seem to have RustAlerts configured!\n\nType " + TextWithColor( "/rustalerts setup", Color.cyan ) + " to configure RustAlerts and get notifications on your phone even when you're offline." ); unregisteredPlayers.Add( player ); };
			RustAlertsPlugin.Call( "IsPlayerFamiliar", player.userID, trueCallback, falseCallback );
		}

		void OnPlayerDisconnected( BasePlayer player, string reason ) {
			Action trueCallback = () => { RustAlertsPlugin.Call( "RegisterPlayer", player.userID ); };
			Action falseCallback = () => { };
			RustAlertsPlugin.Call( "IsPlayerFamiliar", player.userID, trueCallback, falseCallback );
		}

		void Loaded() {
			Debug.Log( "Loaded extern. Rust plugin!" );
		}

		void OnServerInitialized() {
			if ( instance == null ) {
				instance = this;
			}
			//SendServerMessage( "**** RustAlerts loaded successfully! ****");
			RustAlertsPlugin.Call( "LoadPlayersFromDB" );
			StartServerClock();
		}

		public string TextWithColor( string text, Color c ) {
			return "<color=#" + ColorToHex( c ) + ">" + text + "</color>";
		}

		public string ColorToHex( Color32 color ) {
			string hex = color.r.ToString( "X2" ) + color.g.ToString( "X2" ) + color.b.ToString( "X2" );
			return hex;
		}

	}
}
