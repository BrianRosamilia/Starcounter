﻿using System;
using System.Dynamic;
                // Special handling of properties declared in the baseclass (Obj) and overriden
                // using 'new', like a generic override of Data returning the correct type.
                if (binder.Name == "Data") {
                    return base.BindGetMember(binder);
                }
                            var paris = m.GetParameters();
                                var found = (pari.ParameterType.Name.Equals("TObjArr")) && !m.IsGenericMethod;
                // Special handling of properties declared in the baseclass (Obj) and overriden
                // using 'new', like a generic override of Data returning the correct type.
                if (binder.Name == "Data") {
                    return base.BindSetMember(binder, value);
                }