using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerPrivilegeMutator : IPlayerPrivilegeMutator
    {
        private readonly ICoreServerAPI api;

        public PlayerPrivilegeMutator(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void Grant(IServerPlayer player, string privilege)
        {
            InvokePrivilegeMutation("GrantPrivilege", player, privilege);
        }

        public void Revoke(IServerPlayer player, string privilege)
        {
            InvokePrivilegeMutation("RevokePrivilege", player, privilege);
        }

        private void InvokePrivilegeMutation(string methodName, IServerPlayer player, string privilege)
        {
            if (player == null || string.IsNullOrWhiteSpace(privilege))
            {
                return;
            }

            object permissionManager = api.Permissions;
            MethodInfo[] candidateMethods = permissionManager
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                .ToArray();

            foreach (MethodInfo candidateMethod in candidateMethods)
            {
                if (!TryBuildArguments(candidateMethod, player, privilege, out object[] arguments))
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

            throw new InvalidOperationException($"Unable to locate a supported permissions API method for {methodName}.");
        }

        private static bool TryBuildArguments(MethodInfo method, IServerPlayer player, string privilege, out object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            arguments = new object[parameters.Length];

            bool assignedPrivilege = false;
            bool assignedPlayer = false;

            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo parameter = parameters[index];
                Type parameterType = parameter.ParameterType;

                if (parameterType == typeof(string))
                {
                    if (IsPrivilegeParameter(parameter.Name, assignedPrivilege, assignedPlayer))
                    {
                        arguments[index] = privilege;
                        assignedPrivilege = true;
                    }
                    else
                    {
                        arguments[index] = player.PlayerUID;
                        assignedPlayer = true;
                    }

                    continue;
                }

                if (parameterType == typeof(bool))
                {
                    arguments[index] = true;
                    continue;
                }

                if (parameterType == typeof(int))
                {
                    arguments[index] = 0;
                    continue;
                }

                if (parameterType == typeof(EnumPlayerGroupMemberShip))
                {
                    arguments[index] = EnumPlayerGroupMemberShip.Member;
                    continue;
                }

                if (parameterType.IsInstanceOfType(player)
                    || parameterType.IsAssignableFrom(player.GetType()))
                {
                    arguments[index] = player;
                    assignedPlayer = true;
                    continue;
                }

                if (typeof(IPlayer).IsAssignableFrom(parameterType))
                {
                    arguments[index] = player;
                    assignedPlayer = true;
                    continue;
                }

                arguments = Array.Empty<object>();
                return false;
            }

            return assignedPrivilege && assignedPlayer;
        }

        private static bool IsPrivilegeParameter(string parameterName, bool assignedPrivilege, bool assignedPlayer)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return !assignedPrivilege;
            }

            if (parameterName.IndexOf("priv", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (parameterName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("uid", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("plr", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (!assignedPrivilege)
            {
                return true;
            }

            return assignedPlayer;
        }
    }
}
