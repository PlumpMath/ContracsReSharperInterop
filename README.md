﻿# CodeContracs R# Interop ![Badge](https://tom-englert.visualstudio.com/_apis/public/build/definitions/75bf84d2-d359-404a-a712-07c9f693f635/9/badge)

Download the latest binaries from the [Visual Studio Gallery](https://visualstudiogallery.msdn.microsoft.com/8005d228-9f16-4a78-906e-366e919af8e5)

Code fixes to automate adding R#'s `[NotNull]` attributes for the corrseponding `Contract.Requires`, `Contract.Ensures` or `Contract.Invariant`.

This can be useful to have both R# and CodeContract generate (mostly) the same warnings, or to migrate from CodeContracts to R# at all.

![screenshot](https://github.com/tom-englert/ContracsReSharperInterop/blob/master/Assets/Screenshot.png)

e.g.

```
using System.Diagnostics.Contracts;

namespace Test
{
    class Class
    {
        void Method(object arg)
        {
            Contract.Requires(arg != null);
        }
    }
}
```

gets

```
using System.Diagnostics.Contracts;
using JetBrains.Annotations;

namespace Test
{
    class Class
    {
        void Method([NotNull] object arg)
        {
            Contract.Requires(arg != null);
        }
    }
}
```

It handles the cases `arg != null` as well as `!string.IsNullOrEmpty(arg)` 