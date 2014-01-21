
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Starcounter;

[assembly: InternalsVisibleTo("Starcounter.Bootstrap, PublicKey=0024000004800000940000000602000000240000525341310004000001000100e758955f5e1537c52891c61cd689a8dd1643807340bd32cc12aee50d2add85eeeaac0b44a796cefb6055fac91836a8a72b5dbf3b44138f508bc2d92798a618ad5791ff0db51b8662d7c936c16b5720b075b2a966bb36844d375c1481c2c7dc4bb54f6d72dbe9d33712aacb6fa0ad84f04bfa6c951f7b9432fe820884c81d67db")]


namespace Starcounter.Advanced {

    /// <summary>
    /// This class does not define any instance properties or behaviour. It just makes
    /// available the global static functions SQL, Transaction and communication handler functions
    /// GET,POST, etc. in various Starcounter classes for convenience.
    /// </summary>
    public class StarcounterBase : Handle {

        /// <summary>
        /// Inject database function provider here
        /// </summary>
        public static IDb _DB;

        /// <summary>
        /// 
        /// </summary>
        public static IRestServer Fileserver;

        /// <summary>
        /// Runs code as an ACID database transaction in the embedding database.
        /// </summary>
        /// <param name="action"></param>
        public static void TransactionRun(Action action) {
            _DB.Transaction(action);
        }

        /// <summary>
        /// Executes a query on the embedding database.
        /// </summary>
        /// <param name="query">The SQL query string excluding parameters. Parameters are supplied as ? marks.</param>
        /// <param name="args">The parameters corresponding to the ? marks in the query string.</param>
        /// <returns></returns>
        public static Rows<dynamic> SQL(string query, params object[] args) {
            return _DB.SQL(query, args);
        }

        /// <summary>
        /// Executes a query on the embedding database.
        /// </summary>
        /// <typeparam name="T">The class of each entity in the result."/></typeparam>
        /// <param name="query">The SQL query string excluding parameters. Parameters are supplied as ? marks.</param>
        /// <param name="args">The parameters corresponding to the ? marks in the query string.</param>
        /// <returns></returns>
        public static Rows<T> SQL<T>(string query, params object[] args) {
            return _DB.SQL<T>(query, args);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="pars"></param>
        /// <returns></returns>
        public static Rows<dynamic> SlowSQL(string str, params object[] pars) {
            return _DB.SlowSQL(str, pars);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <param name="pars"></param>
        /// <returns></returns>
        public static Rows<T> SlowSQL<T>(string str, params object[] pars) {
            return _DB.SlowSQL<T>(str, pars);
        }

        /// <summary>
        /// 
        /// </summary>
        public static IREST REST {
            get {
                return _REST;
            }
        }


        /// <summary>
        /// Gets the specified URI.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uri">The URI.</param>
        /// <returns>``0.</returns>
        public static T Get<T>(string uri) {
            return (T)Get(uri);
        }


        /// <summary>
        /// Gets the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="pars">The pars.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public static object Get(string uri, params object[] pars) {
            throw new NotImplementedException();
        }

    }
}
