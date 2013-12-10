using System;
using Starcounter.Internal.XSON;
using Starcounter.Templates;

namespace Starcounter {
	partial class Json {
		/// <summary>
		/// 
		/// </summary>
		internal void Dirtyfy() {
			_Dirty = true;
			if (Parent != null)
				Parent.Dirtyfy();
		}

		/// <summary>
		/// 
		/// </summary>
		internal void CheckpointChangeLog() {
			if (this.IsArray) {
				this.ArrayAddsAndDeletes = null;
				if (Template != null) {
					var tjson = (TObjArr)Template;
					tjson.Checkpoint(this.Parent);
				}
			} else {
				if (Template != null) {
					var tjson = (TObject)Template;

					for (int i = 0; i < tjson.Properties.ExposedProperties.Count; i++) {
						var property = tjson.Properties.ExposedProperties[i] as TValue;
						if (property != null) {
							property.Checkpoint(this);
						}
					}
				}
			}
			_Dirty = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="prop"></param>
		/// <returns></returns>
		public bool IsDirty(Template prop) {
#if DEBUG
			this.Template.VerifyProperty(prop);
#endif
			return (WasReplacedAt(prop.TemplateIndex));
		}

		/// <summary>
		/// Logs all property changes made to this object or its bound data object
		/// </summary>
		/// <param name="session">The session (for faster access)</param>
		internal void LogValueChangesWithDatabase(Session session) {
			if (this.IsArray) {
				LogArrayChangesWithDatabase(session);
			} else {
				LogObjectValueChangesWithDatabase(session);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="session"></param>
		private void LogArrayChangesWithDatabase(Session session) {
			if (ArrayAddsAndDeletes != null) {
				Session._Changes.AddRange(ArrayAddsAndDeletes);
				ArrayAddsAndDeletes.Clear();

				for (int i = 0; i < list.Count; i++) {
					CheckpointAt(i);
				}
			}
			//            foreach (var e in _Values) {
			//                (e as Json).LogValueChangesWithDatabase(session);
			//            }
			//           if (_Dirty) {


			var property = Template as TValue;
			for (int t = 0; t < _list.Count; t++) {
				if (WasReplacedAt(t)) {
					session._Changes.Add(Change.Update(this.Parent as Json, property, t));
				}
				(_list[t] as Json).LogValueChangesWithDatabase(session);
			}
			//            }
		}

		/// <summary>
		/// Used to generate change logs for all pending property changes in this object and
		/// and its children and grandchidren (recursivly) excluding changes to bound data
		/// objects. This method is much faster than the corresponding method checking
		/// th database.
		/// </summary>
		/// <param name="session">The session (for faster access)</param>
		internal void LogValueChangesWithoutDatabase(Session s) {
			throw new NotImplementedException();
		}


		/// <summary>
		/// Dirty checks each value of the object and reports any changes
		/// to the session changelog.
		/// </summary>
		/// <param name="session">The session to report to</param>
		private void LogObjectValueChangesWithDatabase(Session session) {
			var template = (TObject)Template;
			var exposed = template.Properties.ExposedProperties;

			ResumeTransaction(false);

			if (_Dirty) {
				for (int t = 0; t < exposed.Count; t++) {
					if (WasReplacedAt(exposed[t].TemplateIndex)) {
						var s = Session;
						if (s != null) {
							if (IsArray) {
								throw new NotImplementedException();
							} else {
								var childTemplate = (TValue)exposed[t];
								Session.UpdateValue(this, childTemplate);

								// TODO:
								// Added this code to make current implementation work.
								// Probably not the correct place to do it though, both
								// for readability and speed.
								Json childJson = null;
								if (childTemplate is TObjArr) 
									childJson = this.Get((TObjArr)childTemplate);
								else if (childTemplate is TObject)
									childJson = this.Get((TObject)childTemplate);									

								if (childJson != null) {
									childJson.SetBoundValuesInTuple();
									childJson.CheckpointChangeLog();
								}
							}
						}
						CheckpointAt(exposed[t].TemplateIndex);
					} else {
						var p = exposed[t];
						if (p is TContainer) {
							var c = ((TContainer)p).GetValue(this);
							if (c != null)
								c.LogValueChangesWithDatabase(session);
						} else {
							if (IsArray)
								throw new NotImplementedException();
							else
								((TValue)p).CheckAndSetBoundValue(this, true);
						}
					}
				}
				_Dirty = false;
			} else if (template.HasAtLeastOneBoundProperty) {
				for (int t = 0; t < exposed.Count; t++) {
					if (exposed[t] is TContainer) {
						((TContainer)exposed[t]).GetValue(this).LogValueChangesWithDatabase(session);
					} else {
						if (IsArray) {
							throw new NotImplementedException();
						} else {
							var p = exposed[t] as TValue;
							p.CheckAndSetBoundValue(this, true);
						}
					}
				}
			} else {
				foreach (var e in list) {
					if (e is Json) {
						((Json)e).LogValueChangesWithDatabase(session);
					}
				}
			}
		}

		internal void SetBoundValuesInTuple() {
			if (IsArray) {
				foreach (Json item in _list) {
					item.SetBoundValuesInTuple();
				}
			} else {
				ResumeTransaction(false);
				TObject tobj = (TObject)Template;
				if (tobj != null) {
					for (int i = 0; i < tobj.Properties.Count; i++) {
						var t = tobj.Properties[i];

						if (t is TContainer) {
							var childJson = ((TContainer)t).GetValue(this);
							childJson.SetBoundValuesInTuple();
						} else {
							var vt = t as TValue;
							if (vt != null)
								vt.CheckAndSetBoundValue(this, false);
						}
					}
				}
			}
		}
	}
}
