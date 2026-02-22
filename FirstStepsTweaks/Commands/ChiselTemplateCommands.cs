using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
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
                    .WithDescription("Capture the chiseled block you are looking at to a named template")
                    .WithArgs(api.ChatCommands.Parsers.Word("templateName"))
                    .HandleWith(args => CaptureTemplate(api, args))
                .EndSubCommand()
                .BeginSubCommand("load")
                    .WithDescription("Load a saved chiseled block template and place it where you are standing")
                    .WithArgs(api.ChatCommands.Parsers.Word("templateName"))
                    .HandleWith(args => LoadTemplate(api, args))
                .EndSubCommand();
        }

        private static TextCommandResult CaptureTemplate(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;

            string templateName = (string)args[0];

            string sanitizedName = SanitizeTemplateName(templateName);
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Invalid template name.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            BlockSelection selection = caller.CurrentBlockSelection;
            if (selection == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Look at a block before using this command.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            BlockPos pos = selection.Position.Copy();
            Block block = api.World.BlockAccessor.GetBlock(pos);
            if (block == null || block.Id == 0)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"No block found at {pos.X} {pos.Y} {pos.Z}.", EnumChatType.CommandError);
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

                string blockCode = block.Code?.ToShortString() ?? "game:chiseledblock";
                string encodedState = EncodeTreeAttribute(stateTree);

                string templatesDir = api.GetOrCreateDataPath("ChiselTemplates");
                Directory.CreateDirectory(templatesDir);

                string filePath = Path.Combine(templatesDir, sanitizedName + ".template");
                File.WriteAllLines(filePath, new[] { blockCode, encodedState }, Encoding.UTF8);

                string successMessage = $"Captured chiseled template '{sanitizedName}' from {pos.X} {pos.Y} {pos.Z}.";
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, successMessage, EnumChatType.CommandSuccess);
                api.Logger.Notification($"[FirstStepsTweaks] {successMessage} Saved to: {filePath}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to capture chisel template '{sanitizedName}' at {pos.X} {pos.Y} {pos.Z}.";
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, errorMessage, EnumChatType.CommandError);
                api.Logger.Error($"[FirstStepsTweaks] {errorMessage} Error: {ex}");
            }

            return TextCommandResult.Success();
        }

        private static TextCommandResult LoadTemplate(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;

            string templateName = (string)args[0];
            string sanitizedName = SanitizeTemplateName(templateName);

            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Invalid template name.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            string templatesDir = api.GetOrCreateDataPath("ChiselTemplates");
            string filePath = Path.Combine(templatesDir, sanitizedName + ".template");

            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(templatesDir, sanitizedName + ".json");
            }

            if (!File.Exists(filePath))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Template '{sanitizedName}' not found.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2)
                {
                    caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Template format is invalid.", EnumChatType.CommandError);
                    return TextCommandResult.Success();
                }

                string blockCode = lines[0];
                if (string.IsNullOrWhiteSpace(blockCode))
                {
                    blockCode = "game:chiseledblock";
                }

                Block blockToPlace = api.World.GetBlock(new AssetLocation(blockCode));
                if (blockToPlace == null || blockToPlace.Id == 0)
                {
                    caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Unable to resolve block '{blockCode}' in template.", EnumChatType.CommandError);
                    return TextCommandResult.Success();
                }

                BlockPos placePos = caller.Entity.Pos.AsBlockPos;
                api.World.BlockAccessor.SetBlock(blockToPlace.Id, placePos);

                BlockEntity placedEntity = api.World.BlockAccessor.GetBlockEntity(placePos);
                if (placedEntity == null)
                {
                    caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Placed block does not have a BlockEntity; template cannot be applied.", EnumChatType.CommandError);
                    return TextCommandResult.Success();
                }

                TreeAttribute stateTree = DecodeTreeAttribute(lines[1]);

                placedEntity.FromTreeAttributes(stateTree, api.World);
                placedEntity.MarkDirty(true);

                string successMessage = $"Loaded chiseled template '{sanitizedName}' at {placePos.X} {placePos.Y} {placePos.Z}.";
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, successMessage, EnumChatType.CommandSuccess);
                api.Logger.Notification($"[FirstStepsTweaks] {successMessage} Source: {filePath}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to load chisel template '{sanitizedName}'.";
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, errorMessage, EnumChatType.CommandError);
                api.Logger.Error($"[FirstStepsTweaks] {errorMessage} Error: {ex}");
            }

            return TextCommandResult.Success();
        }


        private static string EncodeTreeAttribute(TreeAttribute stateTree)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream, Encoding.UTF8, true))
            {
                stateTree.ToBytes(writer);
                writer.Flush();
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        private static TreeAttribute DecodeTreeAttribute(string encodedState)
        {
            byte[] stateBytes = Convert.FromBase64String(encodedState);

            using (MemoryStream memoryStream = new MemoryStream(stateBytes))
            using (BinaryReader reader = new BinaryReader(memoryStream, Encoding.UTF8, true))
            {
                TreeAttribute stateTree = new TreeAttribute();
                stateTree.FromBytes(reader);
                return stateTree;
            }
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
