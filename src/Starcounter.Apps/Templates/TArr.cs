﻿// ***********************************************************************
// <copyright file="TArr.cs" company="Starcounter AB">
//     Copyright (c) Starcounter AB.  All rights reserved.
// </copyright>
// ***********************************************************************

using System;
using System.Collections;
using System.Collections.Generic;
using Starcounter.Templates.DataBinding;
using Starcounter.Templates.Interfaces;
#if CLIENT
namespace Starcounter.Client.Template {
#else
namespace Starcounter.Templates {
#endif

//    public class SetProperty<AppType, SchemaType> : TObjArr<AppType> where AppType : App, new() where SchemaType : TApp {
//    }

    /// <summary>
    /// Class TArr
    /// </summary>
    /// <typeparam name="OT">The type of the app type.</typeparam>
    /// <typeparam name="OTT">The type of the app template type.</typeparam>
    public class TArr<OT,OTT> : TObjArr
        where OT : Obj, new()
        where OTT : TObj
    {
        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <returns>System.Object.</returns>
        public override object CreateInstance(Container parent) {
            return new Listing<OT>((Obj)parent, this);
        }

        /// <summary>
        /// The .NET type of the instance represented by this template.
        /// </summary>
        /// <value>The type of the instance.</value>
        public override System.Type InstanceType {
            get { return typeof(Listing<OT>); }
        }

        internal override void ProcessInput(Obj obj, byte[] rawValue) {
            throw new System.NotImplementedException();
        }


        /// <summary>
        /// Gets or sets the app.
        /// </summary>
        /// <value>The app.</value>
        public override TObj App {
            get {
                if (_Single.Length == 0)
                    return null;
                return (TObj)_Single[0];
            }
            set {
                _Single = new OTT[1];
                _Single[0] = (OTT)value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        internal OTT[] _Single = new OTT[0];


        /// <summary>
        /// Gets the children.
        /// </summary>
        /// <value>The children.</value>
        public override IEnumerable<Template> Children {
            get {
                return (IEnumerable<Template>)_Single;
            }
        }


    }

    /// <summary>
    /// Class TArr
    /// </summary>
    public abstract class TObjArr : TContainer
#if IAPP
//        , ITObjArr
#endif
    {

        private DataBinding<SqlResult> dataBinding;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataGetter"></param>
        public void AddDataBinding(Func<Obj, SqlResult> dataGetter) {
            dataBinding = new DataBinding<SqlResult>(dataGetter);
            Bound = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public SqlResult GetBoundValue(Obj app) {
            return dataBinding.GetValue(app);
        }
        
        /// <summary>
        /// Gets or sets the type (the template) that should be the template for all elements
        /// in this array.
        /// </summary>
        /// <value>The obj template adhering to each element in this array</value>
        public abstract TObj App { get; set; }

        /// <summary>
        /// Contains the default value for the property represented by this
        /// Template for each new App object.
        /// </summary>
        /// <value>The default value as object.</value>
        /// <exception cref="System.NotImplementedException"></exception>
        public override object DefaultValueAsObject {
            get {
                throw new System.NotImplementedException();
            }
            set {
                throw new System.NotImplementedException();
            }
        }

    }

}
