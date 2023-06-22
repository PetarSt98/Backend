using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using Microsoft.Extensions.Logging;

namespace Backend.Modules.ActiveDirectory {
    public class ActiveDirectoryProxy : IActiveDirectoryProxy {
        private readonly ILogger<ActiveDirectoryProxy> _logger;
        private const string UrlBase = "LDAP://cern.ch";

        public ActiveDirectoryProxy(ILogger<ActiveDirectoryProxy> logger) {
            _logger = logger;
        }

        public HashSet<string> ListNestedGroups(string memberName) {
            _logger.LogInformation($"Getting groups for member name '{memberName}'.");
            var userNestedGroups = new HashSet<string>();
            var entry = new DirectoryEntry("LDAP://cern.ch");
            var mySearcher = new DirectorySearcher(entry);
            mySearcher.Filter = "(&(objectClass=*)(|(cn=" + memberName + ")))";
            SearchResult result = mySearcher.FindOne();

            if (result == null)
                return userNestedGroups;

            DirectoryEntry directoryObject = result.GetDirectoryEntry();
            PropertyValueCollection values = directoryObject.Properties["memberOf"];
            IEnumerator en = values.GetEnumerator();
            while (en.MoveNext()) {
                if (en.Current == null || en.Current.ToString().Contains("Exchange")) continue;

                DirectoryEntry obGpEntry = new DirectoryEntry("LDAP://" + en.Current);
                string groupName = obGpEntry.Name[3..];

                if (!userNestedGroups.Contains(groupName))
                    userNestedGroups.Add(groupName);

                HashSet<string> nested = ListNestedGroups(groupName);
                if (nested == null) continue;
                foreach (var nestedGroup in nested)
                    userNestedGroups.Add(nestedGroup);
            }
            _logger.LogInformation($"Found {userNestedGroups.Count} groups for member name '{memberName}'.");
            return userNestedGroups;
        }

        public bool ExistsInActiveDirectory(string login) {
            var isGroup = false;
            var isLogin = false;
            var entry = new DirectoryEntry(UrlBase);
            var mySearcher = new DirectorySearcher(entry) { Filter = $"(&(objectClass=group)(|(cn={login})))" };

            var result = mySearcher.FindOne();
            if (result != null)
                isGroup = true;

            if (isGroup) return true;
            mySearcher.Filter = $"(&(objectClass=user)(|(cn={login})(sAMAccountName={login})))";
            result = mySearcher.FindOne();
            if (result != null)
                isLogin = true;
            return isLogin;
        }
    }
}
