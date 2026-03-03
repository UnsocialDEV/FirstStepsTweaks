using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public class DebugCommands
    {
        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands
                .Create("fsdebug")
                .WithDescription("Debug command for First Steps dev")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => fsDebug(api, args));
        }

        private static TextCommandResult fsDebug(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Player only");

            BlockPos pos = player.Entity.ServerPos.AsBlockPos.Copy();
            pos.Up();

            AssetLocation entityCode = new AssetLocation("game:chicken-hen");
            EntityProperties type = api.World.GetEntityType(entityCode);

            if (type == null)
                return TextCommandResult.Error("Entity type not found");

            Entity entity = api.World.ClassRegistry.CreateEntity(type);

            entity.ServerPos.SetPos(
                pos.X + 0.5,
                pos.Y + 1.5,
                pos.Z + 0.5
            );

            entity.Pos.SetFrom(entity.ServerPos);

            api.World.SpawnEntity(entity);

            // Set custom name
            entity.WatchedAttributes.SetString("name", "☠ FLOATING TEXT TEST ☠");
            entity.WatchedAttributes.MarkPathDirty("name");

            return TextCommandResult.Success("Spawned test entity");
        }
    }
}