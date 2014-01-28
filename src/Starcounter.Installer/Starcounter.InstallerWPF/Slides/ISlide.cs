using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Starcounter.InstallerWPF.Slides
{
    interface ISlide
    {
        string HeaderText { get; }
        bool AutoClose { get; }
    }
}
