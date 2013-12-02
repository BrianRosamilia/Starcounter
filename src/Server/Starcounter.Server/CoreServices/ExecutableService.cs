using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Starcounter.Server {
    /// <summary>
    /// Provides services for executables to the server implementation.
    /// </summary>
    internal sealed class ExecutableService {
        readonly ServerEngine engine;
        HashAlgorithm hashAlgorithm;

        /// <summary>
        /// Initializes a <see cref="ExecutableService"/>.
        /// </summary>
        /// <param name="engine">The <see cref="ServerEngine"/> under which
        /// this service will run.</param>
        internal ExecutableService(ServerEngine engine) {
            this.engine = engine;
        }

        /// <summary>
        /// Executes setup of <see cref="ExecutableService"/>.
        /// </summary>
        internal void Setup() {
            this.hashAlgorithm = SHA1.Create();
        }

        /// <summary>
        /// Generates a unique key for the given executable, based on it's path.
        /// </summary>
        /// <param name="executablePath">Full path to the executable whose key
        /// should be generated.</param>
        /// <returns>A unique key from <paramref name="executablePath"/>.</returns>
        public string CreateKey(string executablePath) {
            string hash;
            string key;

            executablePath = executablePath.ToLowerInvariant();
            var keyBytes = Encoding.UTF8.GetBytes(executablePath);
            var hashBytes = hashAlgorithm.ComputeHash(keyBytes);
            hash = BitConverter.ToString(hashBytes).Replace("-", "");

            key = hash;
            return key;
        }
    }
}
