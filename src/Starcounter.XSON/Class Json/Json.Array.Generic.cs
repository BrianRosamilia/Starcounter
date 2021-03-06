﻿

using Starcounter.Advanced;
using Starcounter.Internal.XSON;
using Starcounter.Templates;
using System;
using System.Collections;
using System.Collections.Generic;
namespace Starcounter {
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Arr<T> : Json where T : Json, new() {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static implicit operator Arr<T>(Rows res) {
            return new Arr<T>(res);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        protected Arr(IEnumerable result)
            : base(result) {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="templ"></param>
        public Arr(Json parent, TObjArr templ)
            : base(parent, templ) {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public new T Add() {
            /*
            TObjArr template = (TObjArr)Template;
            Template typed = template.ElementType;
            T app;
            if (typed != null) {
                app = (T)typed.CreateInstance(this);
            }
            else {
                throw new NotImplementedException();
//                app = new T();
//                app.Parent = this;
            }
            Add(app);
            return app;
             */

            TObjArr template = (TObjArr)Template;
            T app = new T();
            //app.Parent = this;

            Template typed = template.ElementType;
            if (typed != null) {
                app.Template = (TObject)typed;
            }
            else {
                app.CreateDynamicTemplate();
//                app.Template = new Schema();
//                CreateGe
            }
            Add(app);
            return app;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item) {
            base.Add(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public new T Add(object data) {
			T app;
            TObjArr template = (TObjArr)Template;

			if (template.ElementType == null) {
				app = new T();
				app.CreateDynamicTemplate();
			} else {
				app = (T)template.ElementType.CreateInstance(this);
			}
            app.Data = data;
            Add(app);
            return app;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
		public new T this[int index] {
			get {
				return (T)base[index];
			}
			set {
				base[index] = value;
			}
		}
    }
}
