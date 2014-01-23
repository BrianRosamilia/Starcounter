
using Starcounter.Internal.JsonPatch;
using Starcounter.Internal.XSON;
using Starcounter.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Starcounter.Internal;
namespace Starcounter {
    partial class Session : IEnumerable<Change> {

        private bool _BrandNew = true;

        /// <summary>
        /// The log of Json tree changes pertaining to the session data
        /// </summary>
        internal List<Change> _Changes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Session" /> class.
        /// </summary>
        public Session()
        {
            _Changes = new List<Change>();

            UInt32 errCode = 0;

            if (_Request != null)
            {
                errCode = _Request.GenerateNewSession(this);
            }
            else
            {
                // Simply generating new session.
                ScSessionStruct sss = new ScSessionStruct(true);

                errCode = GlobalSessions.AllGlobalSessions.CreateNewSession(ref sss, this);
            }

            if (errCode != 0)
                throw ErrorCode.ToException(errCode);
        }

        /// <summary>
        /// Adds an value update change.
        /// </summary>
        /// <param name="obj">The Obj.</param>
        /// <param name="property">The property.</param>
        internal void UpdateValue(Json obj, TValue property) {
                _Changes.Add(Change.Update(obj, property));
        }

        /// <summary>
        /// Clears all changes.
        /// </summary>
        public void Clear() {
            _Changes.Clear();
        }

        /// <summary>
        /// Returns a typed enumerator of all changes.
        /// </summary>
        /// <returns>IEnumerator{Change}.</returns>
        public IEnumerator<Change> GetEnumerator() {
            return _Changes.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator of all changes
        /// </summary>
        /// <returns><see cref="T:System.Collections.IEnumerator" /></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return _Changes.GetEnumerator();
        }

        /// <summary>
        /// Returns the number of changes in the log.
        /// </summary>
        /// <value></value>
        public Int32 Count { get { return _Changes.Count; } }

        /// <summary>
        /// Generates a JSON-Patch array for all changes made to the session data
        /// </summary>
        /// <param name="flushLog">If true, the change log will be reset</param>
        /// <returns>The JSON-Patch string (see RFC6902)</returns>
        public string CreateJsonPatch( bool flushLog ) {

			//if (_BrandNew) {
			//	// Just return the whole thing as a change to the root
			//	//GenerateChangeLog(); // Needed to update bound dirty check values.
			//	this.CheckpointChangeLog();
			//	_BrandNew = false;
			//	return "[{\"op\":\"add\",\"path\":\"/\",\"value\":"+_Data.ToJson()+"}]";
			//}

			//this.GenerateChangeLog();

			//var buffer = new List<byte>();
			//HttpPatchBuilder.CreateContentFromChangeLog(this, buffer);
			//var ret = Encoding.UTF8.GetString(buffer.ToArray());

			//if (flushLog) {
			//	this._Changes = new List<Change>();
			//}
			//return ret;

			return Encoding.UTF8.GetString(CreateJsonPatchBytes(flushLog));
        }

        /// <summary>
        /// Generates a JSON-Patch array for all changes made to the session data
        /// </summary>
        /// <param name="flushLog">If true, the change log will be reset</param>
        /// <returns>The JSON-Patch string (see RFC6902)</returns>
        public byte[] CreateJsonPatchBytes(bool flushLog) {
			if (_BrandNew) {
				// TODO: 
				// might be array.
				_Changes.Add(Change.Add(_Data));
				this.CheckpointChangeLog();
				_BrandNew = false;
			} else {
				this.GenerateChangeLog();
			}

            var buffer = new List<byte>();
            HttpPatchBuilder.CreateContentFromChangeLog(this, buffer);

            if (flushLog) {
                this._Changes = new List<Change>();
            }

            return buffer.ToArray();
        }


        /// <summary>
        /// Logs all changes since the last JSON-Patch update. This method generates the log
        /// for the dirty flags and the added/removed logs of the JSON tree in the session data.
        /// </summary>
        private void GenerateChangeLog() {
            if (DatabaseHasBeenUpdatedInCurrentTask) {
                this._Data.LogValueChangesWithDatabase(this);
            }
            else {
                this._Data.LogValueChangesWithoutDatabase(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool DatabaseHasBeenUpdatedInCurrentTask {
            get {
                return true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool HaveAddedOrRemovedObjects { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public void CheckpointChangeLog() {
            this._Data.CheckpointChangeLog();
			_BrandNew = false;
        }

        public bool BrandNew {
            get {
                return _BrandNew;
            }
        }
    }

}
