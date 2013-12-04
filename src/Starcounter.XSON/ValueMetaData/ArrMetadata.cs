// ***********************************************************************
// <copyright file="ArrMetadata.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using Starcounter;

namespace Starcounter.Templates {

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="AppType"></typeparam>
    /// <typeparam name="JsonType"></typeparam>
    public class ArrMetadata<AppType,JsonType> : ValueMetadata<JsonType,TArray<AppType>>
                where AppType : Json, new()
                where JsonType : Json
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="prop"></param>
        public ArrMetadata(JsonType app, TArray<AppType> prop) : base(app, prop) { }

    }
}