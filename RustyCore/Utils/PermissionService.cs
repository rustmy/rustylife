using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace RustyCore.Utils
{
    public static class PermissionService
    {
        public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

        public static bool HasPermission(ulong uid, string permissionName)
        {
            return !string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(uid.ToString(), permissionName);
        }

        public static void RegisterPermissions(Plugin owner, List<string> permissions)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            if (permissions == null) throw new ArgumentNullException("commands");

            foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
            {
                permission.RegisterPermission(permissionName, owner);
            }
        }
    }
}
