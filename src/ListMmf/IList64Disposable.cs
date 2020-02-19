﻿using System;
using System.Collections.Generic;

namespace BruSoftware.ListMmf
{
    public interface IList64Disposable<T> : IList64<T>, IDisposable where T : struct
    {
    }
}