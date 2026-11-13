// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2026 AirVPN (support@airvpn.org) / https://airvpn.org )
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>

using System;
//using System.Drawing;
using System.Reflection;
using Foundation;
using AppKit;
using ObjCRuntime;
using Eddie.Core;

namespace Eddie.UI.Cocoa.Osx
{
	class MainClass
	{
        static void Main(string[] args)
		{
			Core.Platform.Instance = new Eddie.Platform.MacOS.Platform();

#if EDDIE_DOTNET
			// .NET has managed POSIX signal handling; NativeMethods.Signal is excluded from the
			// EDDIE_DOTNET build of Lib.Platform.MacOS. Only HUP/INT/TERM: the legacy SIGUSR1/2
			// registrations used Linux numbers (10/12 = SIGBUS/SIGSYS on macOS), so they never fired,
			// and under .NET a real SIGUSR1 (30) arrives at startup and would shut the engine down.
			foreach (System.Runtime.InteropServices.PosixSignal signal in new[] {
				System.Runtime.InteropServices.PosixSignal.SIGHUP,
				System.Runtime.InteropServices.PosixSignal.SIGINT,
				System.Runtime.InteropServices.PosixSignal.SIGTERM })
			{
				SignalRegistrations.Add(System.Runtime.InteropServices.PosixSignalRegistration.Create(signal, context =>
				{
					context.Cancel = true;
					SignalCallback((int)context.Signal);
				}));
			}
#else
			Eddie.Platform.MacOS.NativeMethods.Signal((int)Eddie.Platform.MacOS.NativeMethods.Signum.SIGHUP, SignalCallback);
			Eddie.Platform.MacOS.NativeMethods.Signal((int)Eddie.Platform.MacOS.NativeMethods.Signum.SIGINT, SignalCallback);
			Eddie.Platform.MacOS.NativeMethods.Signal((int)Eddie.Platform.MacOS.NativeMethods.Signum.SIGTERM, SignalCallback);
			Eddie.Platform.MacOS.NativeMethods.Signal((int)Eddie.Platform.MacOS.NativeMethods.Signum.SIGUSR1, SignalCallback);
			Eddie.Platform.MacOS.NativeMethods.Signal((int)Eddie.Platform.MacOS.NativeMethods.Signum.SIGUSR2, SignalCallback);
#endif

			NSApplication.Init();

            NSApplication.Main(args);
		}

#if EDDIE_DOTNET
		// Registrations are unregistered when collected, so keep them alive for the process lifetime.
		private static readonly System.Collections.Generic.List<System.Runtime.InteropServices.PosixSignalRegistration> SignalRegistrations = new System.Collections.Generic.List<System.Runtime.InteropServices.PosixSignalRegistration>();
#endif

		private static void SignalCallback(int signum)
		{
			Engine.Instance.ExitStart();
		}
    }
}

