using System.Collections.Generic;

namespace Backend.Modules.ActiveDirectory {
    public interface IActiveDirectoryProxy {
        HashSet<string> ListNestedGroups(string memberName);
        bool ExistsInActiveDirectory(string login);
    }
}
