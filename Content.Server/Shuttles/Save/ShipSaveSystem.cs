using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Configuration;
using Content.Shared.Shuttles.Save;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.CCVar;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Robust.Shared.Log;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Save
{
    public sealed class ShipSaveSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;

        // Static caches for admin ship save interactions
        private static readonly Dictionary<string, Action<string>> PendingAdminRequests = new();
        private static readonly Dictionary<string, List<(string filename, string shipName, DateTime timestamp)>> PlayerShipCache = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<RequestSaveShipServerMessage>(OnRequestSaveShipServer);
            SubscribeNetworkEvent<RequestLoadShipMessage>(OnRequestLoadShip);
            SubscribeNetworkEvent<RequestAvailableShipsMessage>(OnRequestAvailableShips);
            SubscribeNetworkEvent<AdminSendPlayerShipsMessage>(OnAdminSendPlayerShips);
            SubscribeNetworkEvent<AdminSendShipDataMessage>(OnAdminSendShipData);
        }

        private void OnRequestSaveShipServer(RequestSaveShipServerMessage msg, EntitySessionEventArgs args)
        {
            var playerSession = args.SenderSession;
            if (playerSession == null)
                return;

            var deedUid = new EntityUid((int)msg.DeedUid);
            // Only save the grid referenced by the shuttle deed. Do NOT fall back to the player's current grid / station.
            if (!_entityManager.TryGetComponent<ShuttleDeedComponent>(deedUid, out var deed) || deed.ShuttleUid == null ||
                !_entityManager.TryGetEntity(deed.ShuttleUid.Value, out var shuttleNetUid))
            {
                Logger.Warning($"Player {playerSession.Name} attempted ship save without a valid shuttle deed / shuttle reference on ID {deedUid}");
                return;
            }

            var gridToSave = shuttleNetUid.Value;
            if (!_entityManager.HasComponent<MapGridComponent>(gridToSave))
            {
                Logger.Warning($"Player {playerSession.Name} deed shuttle {gridToSave} is not a grid");
                return;
            }

            var shipName = deed.ShuttleName ?? $"SavedShip_{DateTime.Now:yyyyMMdd_HHmmss}";

            var shipyardGridSaveSystem = _entitySystemManager.GetEntitySystem<Content.Server._NF.Shipyard.Systems.ShipyardGridSaveSystem>();
            Logger.Info($"Player {playerSession.Name} is saving deed-referenced ship {shipName} (grid {gridToSave})");

            // Generate security hash to prevent file tampering
            var securityHash = GenerateShipSecurityHash(playerSession.Name);
            Logger.Debug($"Generated security hash for player {playerSession.Name}: {securityHash}");

            var success = shipyardGridSaveSystem.TrySaveGridAsShip(gridToSave, shipName, playerSession.UserId.ToString(), playerSession, securityHash);
            if (success)
            {
                Logger.Info($"Successfully saved ship {shipName}");
                // Mirror ShipyardGridSaveSystem deed/grid cleanup to avoid stale ownership
                if (_entityManager.TryGetComponent<ShuttleDeedComponent>(deedUid, out var deedComp))
                {
                    _entityManager.RemoveComponent<ShuttleDeedComponent>(deedUid);
                }

                // Remove any other deeds that referenced this shuttle
                var toRemove = new List<EntityUid>();
                var query = _entityManager.EntityQueryEnumerator<ShuttleDeedComponent>();
                while (query.MoveNext(out var ent, out var deedRef))
                {
                    if (deedRef.ShuttleUid != null && _entityManager.TryGetEntity(deedRef.ShuttleUid.Value, out var entUid) && entUid == gridToSave)
                        toRemove.Add(ent);
                }
                foreach (var uidToClear in toRemove)
                {
                    _entityManager.RemoveComponent<ShuttleDeedComponent>(uidToClear);
                }

                // Delete the live grid after save to reset ownership chain
                _entityManager.QueueDeleteEntity(gridToSave);
            }
            else
            {
                Logger.Error($"Failed to save ship {shipName}");
            }
        }
        public void RequestSaveShip(EntityUid deedUid, ICommonSession? playerSession)
        {
            if (playerSession == null)
            {
                Logger.Warning($"Attempted to save ship for deed {deedUid} without a valid player session.");
                return;
            }

            if (!_entityManager.TryGetComponent<ShuttleDeedComponent>(deedUid, out var deedComponent))
            {
                Logger.Warning($"Player {playerSession.Name} tried to save ship with invalid deed UID: {deedUid}");
                return;
            }

            if (deedComponent.ShuttleUid == null || !_entityManager.TryGetEntity(deedComponent.ShuttleUid.Value, out var shuttleUid) || !_entityManager.TryGetComponent<MapGridComponent>(shuttleUid.Value, out var grid))
            {
                Logger.Warning($"Player {playerSession.Name} tried to save ship with deed {deedUid} but no valid shuttle UID found.");
                return;
            }

            // Integrate with ShipyardGridSaveSystem for ship saving functionality
            var shipName = deedComponent.ShuttleName ?? "SavedShip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Get the ShipyardGridSaveSystem and use it to save the ship
            var shipyardGridSaveSystem = _entitySystemManager.GetEntitySystem<Content.Server._NF.Shipyard.Systems.ShipyardGridSaveSystem>();

            Logger.Info($"Player {playerSession.Name} is saving ship {shipName} via ShipyardGridSaveSystem");

            // Generate security hash to prevent file tampering
            var securityHash = GenerateShipSecurityHash(playerSession.Name);
            Logger.Debug($"Generated security hash for player {playerSession.Name}: {securityHash}");

            // Save the ship using the working grid-based system (synchronously on main thread)
            var success2 = shipyardGridSaveSystem.TrySaveGridAsShip(shuttleUid.Value, shipName, playerSession.UserId.ToString(), playerSession, securityHash);
            if (success2)
            {
                // Clean up the deed after successful save
                _entityManager.RemoveComponent<ShuttleDeedComponent>(deedUid);
                Logger.Info($"Successfully saved and removed ship {shipName}");
            }
            else
            {
                Logger.Error($"Failed to save ship {shipName}");
            }
        }

        private void OnRequestLoadShip(RequestLoadShipMessage msg, EntitySessionEventArgs args)
        {
            var playerSession = args.SenderSession;
            if (playerSession == null)
                return;

            Logger.Info($"Player {playerSession.Name} requested to load ship from YAML data");

            // Validate security hash to prevent file tampering
            var expectedHash = GenerateShipSecurityHash(playerSession.Name);
            if (msg.SecurityHash != expectedHash)
            {
                Logger.Error($"Player {playerSession.Name} attempted to load ship with INVALID security hash!");
                Logger.Error($"  Expected hash: {expectedHash}");
                Logger.Error($"  Provided hash: {msg.SecurityHash}");
                Logger.Error($"  This could indicate a tampered save file or a hash generation mismatch");
                return;
            }

            Logger.Info($"Security hash VALIDATED for player {playerSession.Name} - hash matches server expectations");

            // Implement ship loading from YAML data
            try
            {
                var mapLoader = _entitySystemManager.GetEntitySystem<MapLoaderSystem>();

                // Load the ship from the YAML data
                var shipData = msg.YamlData;
                Logger.Info($"Loading ship from YAML data for player {playerSession.Name}");

                // Create a temporary file to load the YAML data from
                // This is necessary because MapLoaderSystem.TryLoadGeneric expects a file path
                var tempFileName = $"/tmp/ship_load_{Guid.NewGuid()}.yml";

                try
                {
                    // Write YAML data to temporary file
                    var resourceManager = IoCManager.Resolve<IResourceManager>();
                    using (var writer = resourceManager.UserData.OpenWriteText(new ResPath(tempFileName)))
                    {
                        writer.Write(shipData);
                    }

                    // Load from the temporary file
                    var success = mapLoader.TryLoadGeneric(new ResPath(tempFileName), out var maps, out var grids);

                    if (!success || maps == null || maps.Count == 0)
                    {
                        Logger.Error($"Failed to deserialize ship YAML for player {playerSession.Name}");
                        return;
                    }

                    Logger.Info($"Successfully loaded ship with {maps.Count} maps for player {playerSession.Name}");

                    // If we got grids, log how many
                    if (grids != null)
                    {
                        Logger.Info($"Loaded {grids.Count} grids for player {playerSession.Name}");
                    }
                }
                catch (Exception loadEx)
                {
                    Logger.Error($"Exception while loading ship YAML for player {playerSession.Name}: {loadEx.Message}");
                    throw;
                }
                finally
                {
                    // Clean up temporary file
                    try
                    {
                        var resourceManager = IoCManager.Resolve<IResourceManager>();
                        if (resourceManager.UserData.Exists(new ResPath(tempFileName)))
                        {
                            resourceManager.UserData.Delete(new ResPath(tempFileName));
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.Warning($"Failed to delete temporary ship load file: {cleanupEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load ship for player {playerSession.Name}: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        private void OnRequestAvailableShips(RequestAvailableShipsMessage msg, EntitySessionEventArgs args)
        {
            var playerSession = args.SenderSession;
            if (playerSession == null)
                return;

            // Client handles available ships from local user data
            Logger.Info($"Player {playerSession.Name} requested available ships - client handles this locally");
        }

        private void OnAdminSendPlayerShips(AdminSendPlayerShipsMessage msg, EntitySessionEventArgs args)
        {
            var key = $"player_ships_{msg.AdminName}";
            if (PendingAdminRequests.TryGetValue(key, out var callback))
            {
                // Cache the ship data for later commands
                PlayerShipCache[key] = msg.Ships;

                var result = $"=== Ships for player ===\n\n";
                for (int i = 0; i < msg.Ships.Count; i++)
                {
                    var (filename, shipName, timestamp) = msg.Ships[i];
                    result += $"[{i + 1}] {shipName} ({filename})\n";
                    result += $"    Saved: {timestamp:yyyy-MM-dd HH:mm:ss}\n";
                    result += "\n";
                }
                callback(result);
                PendingAdminRequests.Remove(key);
            }
        }

        private void OnAdminSendShipData(AdminSendShipDataMessage msg, EntitySessionEventArgs args)
        {
            var key = $"ship_data_{msg.AdminName}_{msg.ShipFilename}";
            if (PendingAdminRequests.TryGetValue(key, out var callback))
            {
                callback(msg.ShipData);
                PendingAdminRequests.Remove(key);
            }
        }

        public static void RegisterAdminRequest(string key, Action<string> callback)
        {
            PendingAdminRequests[key] = callback;
        }

        public void SendAdminRequestPlayerShips(Guid playerId, string adminName, ICommonSession targetSession)
        {
            RaiseNetworkEvent(new AdminRequestPlayerShipsMessage(playerId, adminName), targetSession);
        }

        public void SendAdminRequestShipData(string filename, string adminName, ICommonSession targetSession)
        {
            RaiseNetworkEvent(new AdminRequestShipDataMessage(filename, adminName), targetSession);
        }

        /// <summary>
        /// Generates a security hash to prevent ship save file tampering.
        /// The hash combines the server's unique hash from the shuttle.unique_server_hash CCVar
        /// with the player's username to create a tamper-detection value.
        /// Uses only the player username and server hash as these never change.
        /// </summary>
        private string GenerateShipSecurityHash(string playerUsername)
        {
            try
            {
                var serverHash = _configurationManager.GetCVar(CCVars.UniqueServerHash);

                // Combine server hash with player's username
                var combinedData = $"{serverHash}:{playerUsername}";

                // Generate SHA256 hash
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
                    var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    return hashHex;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to generate security hash for ship save: {ex.Message}. Using fallback hash.");
                // Fallback: if CCVar is missing, use a simple hash of player username alone
                using (var sha256 = SHA256.Create())
                {
                    var fallbackData = playerUsername;
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallbackData));
                    var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    return hashHex;
                }
            }
        }
    }
}
