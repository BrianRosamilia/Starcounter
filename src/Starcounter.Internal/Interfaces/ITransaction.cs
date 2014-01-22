using System;
using System.Collections.Generic;

namespace Starcounter.Advanced {
    public interface ITransaction : IDisposable {
        void Commit();
        void Rollback();
        void Add(Action action);
        void EndScope();
        void BeginScope();
        Boolean IsDirty { get; }
    }
}
