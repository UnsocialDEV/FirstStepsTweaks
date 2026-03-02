using System;
using System.Collections.Generic;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.ChiselTransfer
{
    public static class ChiselCommandHandlers
    {
        private static readonly Dictionary<string, ChiselSelection> PlayerSelections = new Dictionary<string, ChiselSelection>(StringComparer.OrdinalIgnoreCase);

        private static ChiselTransferConfig transferConfig = new ChiselTransferConfig();
        private static ChiselSerializationService serializer = null!;
        private static ChiselFileStore fileStore = null!;
        private static ChiselDataExtractor extractor = null!;

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            transferConfig = config?.ChiselTransfer ?? new ChiselTransferConfig();
            serializer = new ChiselSerializationService();
            fileStore = new ChiselFileStore(api, transferConfig);
            extractor = new ChiselDataExtractor(api);

            api.ChatCommands
                .Create("chiselpos1")
                .WithDescription("Sets position 1 for chisel export/import selection.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => SetSelectionPos(args, true));

            api.ChatCommands
                .Create("chiselpos2")
                .WithDescription("Sets position 2 for chisel export/import selection.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => SetSelectionPos(args, false));

            api.ChatCommands
                .Create("exportchisel")
                .WithDescription("Export chisel data from selected region to file.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("filename"))
                .HandleWith(args => ExportSelected(api, args));

            api.ChatCommands
                .Create("exportchiselbox")
                .WithDescription("Export chisel data from explicit region coordinates.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(
                    api.ChatCommands.Parsers.Int("x1"),
                    api.ChatCommands.Parsers.Int("y1"),
                    api.ChatCommands.Parsers.Int("z1"),
                    api.ChatCommands.Parsers.Int("x2"),
                    api.ChatCommands.Parsers.Int("y2"),
                    api.ChatCommands.Parsers.Int("z2"),
                    api.ChatCommands.Parsers.Word("filename")
                )
                .HandleWith(args => ExportByCoords(api, args));

            api.ChatCommands
                .Create("importchisel")
                .WithDescription("Import chiseled blocks from a file at your current location.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("filename"))
                .HandleWith(args => ImportAtPlayer(api, args));
        }

        private static TextCommandResult SetSelectionPos(TextCommandCallingArgs args, bool first)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            var pos = player.Entity.BlockPos;

            if (!PlayerSelections.TryGetValue(player.PlayerUID, out ChiselSelection selection))
            {
                selection = new ChiselSelection();
                PlayerSelections[player.PlayerUID] = selection;
            }

            if (first)
            {
                selection.X1 = pos.X;
                selection.Y1 = pos.Y;
                selection.Z1 = pos.Z;
            }
            else
            {
                selection.X2 = pos.X;
                selection.Y2 = pos.Y;
                selection.Z2 = pos.Z;
            }

            player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Chisel selection {(first ? "pos1" : "pos2")} set to {pos}.", EnumChatType.CommandSuccess);
            return TextCommandResult.Success();
        }

        private static TextCommandResult ExportSelected(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string filename = (string)args[0];

            if (!PlayerSelections.TryGetValue(player.PlayerUID, out ChiselSelection selection))
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "Selection not set. Use /chiselpos1 and /chiselpos2 first.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            return ExportInternal(api, player, selection, filename);
        }

        private static TextCommandResult ExportByCoords(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            var selection = new ChiselSelection
            {
                X1 = (int)args[0],
                Y1 = (int)args[1],
                Z1 = (int)args[2],
                X2 = (int)args[3],
                Y2 = (int)args[4],
                Z2 = (int)args[5]
            };

            string filename = (string)args[6];
            return ExportInternal(api, player, selection, filename);
        }

        private static TextCommandResult ExportInternal(ICoreServerAPI api, IServerPlayer player, ChiselSelection selection, string filename)
        {
            if (selection.BlockCount <= 0 || selection.BlockCount > transferConfig.MaxBlocksPerExport)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Selection size invalid. Max allowed is {transferConfig.MaxBlocksPerExport} blocks.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            var exportFile = new ChiselExportFile();
            IBlockAccessor blockAccessor = api.World.BlockAccessor;
            var pos = new BlockPos();

            for (int x = selection.MinX; x <= selection.MaxX; x++)
            {
                for (int y = selection.MinY; y <= selection.MaxY; y++)
                {
                    for (int z = selection.MinZ; z <= selection.MaxZ; z++)
                    {
                        pos.Set(x, y, z);
                        Block block = blockAccessor.GetBlock(pos);
                        object? blockEntity = extractor.GetBlockEntity(blockAccessor, pos);

                        if (!extractor.IsLikelyChiseledBlock(block, blockEntity))
                        {
                            continue;
                        }

                        var shapeData = extractor.TryCollectShapeData(blockEntity);
                        if (shapeData.Count == 0)
                        {
                            continue;
                        }

                        exportFile.Blocks.Add(new ChiselBlockRecord
                        {
                            RelX = x - selection.MinX,
                            RelY = y - selection.MinY,
                            RelZ = z - selection.MinZ,
                            BaseBlockCode = block?.Code?.ToString() ?? "",
                            ShapeData = shapeData
                        });
                    }
                }
            }

            string json = serializer.Serialize(exportFile);
            fileStore.Save(filename, json);

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Export complete. Saved {exportFile.Blocks.Count} chiseled blocks from {selection.BlockCount} scanned blocks.",
                EnumChatType.CommandSuccess
            );
            return TextCommandResult.Success();
        }

        private static TextCommandResult ImportAtPlayer(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            string filename = (string)args[0];
            var targetOrigin = player.Entity.BlockPos.Copy();

            string json;
            try
            {
                json = fileStore.Load(filename);
            }
            catch (Exception ex)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Import failed: {ex.Message}", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            ChiselExportFile? file = serializer.Deserialize(json);
            if (file?.Blocks == null || file.Blocks.Count == 0)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "Import failed: File has no blocks.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            IBlockAccessor blockAccessor = api.World.BlockAccessor;
            int importedCount = 0;

            foreach (ChiselBlockRecord record in file.Blocks)
            {
                var targetPos = new BlockPos(targetOrigin.X + record.RelX, targetOrigin.Y + record.RelY, targetOrigin.Z + record.RelZ);
                Block existing = blockAccessor.GetBlock(targetPos);

                if (!transferConfig.AllowOverwriteOnImportDefault && existing != null && existing.Id != 0)
                {
                    continue;
                }

                Block baseBlock = api.World.GetBlock(new AssetLocation(record.BaseBlockCode));
                if (baseBlock == null)
                {
                    continue;
                }

                blockAccessor.SetBlock(baseBlock.Id, targetPos);

                object? blockEntity = extractor.GetBlockEntity(blockAccessor, targetPos);
                extractor.TryApplyShapeData(blockEntity, record.ShapeData);
                importedCount++;
            }

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Import complete. Placed {importedCount} blocks from file '{filename}'.",
                EnumChatType.CommandSuccess
            );

            return TextCommandResult.Success();
        }
    }
}
