using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class ChiselTemplateCommands
    {
        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands.Create("chiseltemplate")
                .WithDescription("Admin command for exporting chiseled block templates")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("capture")
                    .WithDescription("Capture a chiseled block at x y z to a named template")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("templateName"),
                        api.ChatCommands.Parsers.Int("x"),
                        api.ChatCommands.Parsers.Int("y"),
                        api.ChatCommands.Parsers.Int("z")
                    )
                    .HandleWith(args => CaptureTemplate(api, args))
                .EndSubCommand();
        }

        private static TextCommandResult CaptureTemplate(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;

            string templateName = (string)args[0];
            int x = (int)args[1];
            int y = (int)args[2];
            int z = (int)args[3];

            string sanitizedName = SanitizeTemplateName(templateName);
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Invalid template name.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            BlockPos pos = new BlockPos(x, y, z);
            Block block = api.World.BlockAccessor.GetBlock(pos);
            if (block == null || block.Id == 0)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"No block found at {x} {y} {z}.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(pos);
            if (blockEntity == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target block does not have a BlockEntity.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            if (!IsLikelyChiseled(block, blockEntity))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target block is not recognized as a chiseled block.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            try
            {
                TreeAttribute stateTree = new TreeAttribute();
                blockEntity.ToTreeAttributes(stateTree);

                string json = stateTree.ToJsonToken()?.ToString() ?? "{}";

                string templatesDir = api.GetOrCreateDataPath("ChiselTemplates");
                Directory.CreateDirectory(templatesDir);

                string filePath = Path.Combine(templatesDir, sanitizedName + ".json");
                File.WriteAllText(filePath, json, Encoding.UTF8);

                string successMessage = $"Captured chiseled template '{sanitizedName}' from {x} {y} {z}.";
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, successMessage, EnumChatType.CommandSuccess);
                api.Logger.Notification($"[FirstStepsTweaks] {successMessage} Saved to: {filePath}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to capture chisel template '{sanitizedName}' at {x} {y} {z}.";
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, errorMessage, EnumChatType.CommandError);
                api.Logger.Error($"[FirstStepsTweaks] {errorMessage} Error: {ex}");
            }

            return TextCommandResult.Success();
        }

        private static bool IsLikelyChiseled(Block block, BlockEntity blockEntity)
        {
            string blockCode = block.Code?.ToShortString()?.ToLowerInvariant() ?? string.Empty;
            string blockPath = block.Code?.Path?.ToLowerInvariant() ?? string.Empty;
            string entityName = blockEntity.GetType().Name.ToLowerInvariant();

            return blockCode.Contains("chisel")
                || blockPath.Contains("chisel")
                || entityName.Contains("chisel");
        }

        private static string SanitizeTemplateName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(templateName.Length);
            foreach (char c in templateName.Trim())
            {
                if (Array.IndexOf(invalid, c) >= 0)
                {
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
