using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerRoleAssigner : IPlayerRoleAssigner
    {
        private readonly ICoreServerAPI api;

        public PlayerRoleAssigner(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void Assign(IServerPlayer player, string roleCode)
        {
            if (player == null || string.IsNullOrWhiteSpace(roleCode))
            {
                return;
            }

            object permissionManager = api.Permissions;
            MethodInfo[] candidateMethods = permissionManager
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "SetRole", StringComparison.Ordinal))
                .ToArray();

            foreach (MethodInfo candidateMethod in candidateMethods)
            {
                if (!TryBuildArguments(candidateMethod, player, roleCode, out object[] arguments))
                {
                    continue;
                }

                try
                {
                    candidateMethod.Invoke(permissionManager, arguments);
                    return;
                }
                catch (ArgumentException)
                {
                }
                catch (TargetParameterCountException)
                {
                }
            }

            throw new InvalidOperationException("Unable to locate a supported permissions API method for SetRole.");
        }

        private static bool TryBuildArguments(MethodInfo method, IServerPlayer player, string roleCode, out object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            arguments = new object[parameters.Length];

            bool assignedRole = false;
            bool assignedPlayer = false;

            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo parameter = parameters[index];
                Type parameterType = parameter.ParameterType;

                if (parameterType == typeof(string))
                {
                    if (IsRoleParameter(parameter.Name, assignedRole, assignedPlayer))
                    {
                        arguments[index] = roleCode;
                        assignedRole = true;
                    }
                    else
                    {
                        arguments[index] = player.PlayerUID;
                        assignedPlayer = true;
                    }

                    continue;
                }

                if (parameterType.IsInstanceOfType(player)
                    || parameterType.IsAssignableFrom(player.GetType()))
                {
                    arguments[index] = player;
                    assignedPlayer = true;
                    continue;
                }

                arguments = Array.Empty<object>();
                return false;
            }

            return assignedRole && assignedPlayer;
        }

        private static bool IsRoleParameter(string parameterName, bool assignedRole, bool assignedPlayer)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return !assignedRole;
            }

            if (parameterName.IndexOf("role", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (parameterName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("uid", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("plr", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (!assignedRole)
            {
                return true;
            }

            return assignedPlayer;
        }
    }
}
