using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starcounter.XSON {
    internal struct BindingInfo {
        internal static BindingInfo Null = new BindingInfo();

        internal MemberInfo Member;
        internal bool IsBoundToParent;
        internal Type BoundToType;
        internal List<MemberInfo> Path;
    }
}
